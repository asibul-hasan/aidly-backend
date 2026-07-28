using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Sys.Application.Services;

public interface ISys1107Service
{
    Task<List<Sys1107UserOptionDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1107OptionDto>> GetCompaniesAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1107MappingDto>> GetMappingsAsync(long userNo, CancellationToken cancellationToken = default);

    Task<List<Sys1107MappingDto>> SaveMappingsAsync(long userNo, List<Sys1107MappingDto>? rows,
                                                    CancellationToken cancellationToken = default);
}

/// <summary>
/// SYS1107 User-Company Mapping — grants a user access to additional companies
/// (<c>sys_user_company</c>) so owners of sister concerns / shared HO staff can switch companies.
/// The user selector is scoped to the active company; companies are group-wide.
/// </summary>
public class Sys1107Service : ISys1107Service
{
    private const short Deleted = 0;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<Sys1107Service> _logger;

    public Sys1107Service(ISysDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                          ILogger<Sys1107Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    // ─── Options ─────────────────────────────────────────────────────────────

    /// <summary>Scoped to the active company in SQL — never load every user and filter in memory.</summary>
    public async Task<List<Sys1107UserOptionDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = ActiveCompany();

        return await _db.Users
            .AsNoTracking()
            .Where(u => u.CompanyNo == companyNo && u.IsDeleted == Deleted)
            .OrderBy(u => u.UserId)
            .Select(u => new Sys1107UserOptionDto(u.UserNo, u.UserId, u.UserName, u.EmployeeNo, u.AccessScope))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Group-wide: every live company, not just the active one.</summary>
    public async Task<List<Sys1107OptionDto>> GetCompaniesAsync(CancellationToken cancellationToken = default) =>
        await _db.Companies
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.IsDeleted == Deleted)
            .OrderBy(c => c.CompanyNo)
            .Select(c => new Sys1107OptionDto(c.CompanyNo, c.CompanyName))
            .ToListAsync(cancellationToken);

    // ─── Mappings ────────────────────────────────────────────────────────────

    public async Task<List<Sys1107MappingDto>> GetMappingsAsync(long userNo,
                                                                CancellationToken cancellationToken = default)
    {
        await LoadUserAsync(userNo, cancellationToken);

        var companyNames = (await GetCompaniesAsync(cancellationToken))
            .ToDictionary(o => o.Value, o => o.Label);

        var rows = await _db.UserCompanies
            .AsNoTracking()
            .Where(uc => uc.UserNo == userNo && uc.IsDeleted == Deleted)
            .ToListAsync(cancellationToken);

        return rows.Select(uc => ToDto(uc, companyNames)).ToList();
    }

    public Task<List<Sys1107MappingDto>> SaveMappingsAsync(long userNo, List<Sys1107MappingDto>? rows,
                                                           CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            await LoadUserAsync(userNo, ct);

            if (rows == null)
            {
                return await GetMappingsAsync(userNo, ct);
            }

            var existing = await _db.UserCompanies
                .Where(uc => uc.UserNo == userNo && uc.IsDeleted == Deleted)
                .ToListAsync(ct);

            var byCompany = new Dictionary<long, UserCompany>();
            foreach (var uc in existing) byCompany.TryAdd(uc.CompanyNo, uc);

            var currentUser = _ctx.CurrentUserNo();
            var kept = new HashSet<long>();
            var defaultTaken = false;

            // Batch-validate every referenced company in ONE query rather than per row.
            var requestedCompanyNos = rows.Where(r => r.CompanyNo != null)
                .Select(r => r.CompanyNo!.Value).Distinct().ToList();

            var validCompanyNos = requestedCompanyNos.Count == 0
                ? new HashSet<long>()
                : (await _db.Companies
                        .AsNoTracking()
                        .IgnoreQueryFilters()
                        .Where(c => requestedCompanyNos.Contains(c.CompanyNo) && c.IsDeleted == Deleted)
                        .Select(c => c.CompanyNo)
                        .ToListAsync(ct))
                    .ToHashSet();

            foreach (var row in rows)
            {
                if (row.CompanyNo == null)
                {
                    throw new ValidationException("Each mapping needs a company");
                }

                if (!validCompanyNos.Contains(row.CompanyNo.Value))
                {
                    throw new NotFoundException("Company not found: companyNo=" + row.CompanyNo);
                }

                // Add returns false when the company is already in the list — reject rather than
                // silently collapsing the duplicate.
                if (!kept.Add(row.CompanyNo.Value))
                {
                    throw new ValidationException("Duplicate company in the mapping list");
                }

                if (!byCompany.TryGetValue(row.CompanyNo.Value, out var uc))
                {
                    uc = new UserCompany
                    {
                        UserNo = userNo,
                        CompanyNo = row.CompanyNo.Value
                    };
                    _db.UserCompanies.Add(uc);
                    byCompany[row.CompanyNo.Value] = uc;
                }

                // Only the first row asking to be default wins.
                var wantsDefault = row.IsDefault == 1 && !defaultTaken;
                uc.IsDefault = (short)(wantsDefault ? 1 : 0);
                if (wantsDefault) defaultTaken = true;

                uc.IsOwner = row.IsOwner ?? 0;
                uc.IsActive = row.IsActive ?? 1;
            }

            // Soft-delete mappings the user no longer has.
            foreach (var uc in existing)
            {
                if (!kept.Contains(uc.CompanyNo))
                {
                    uc.PerformSoftDelete(currentUser);
                }
            }

            await _db.SaveChangesAsync(ct);

            // Guarantee one default when at least one mapping exists.
            if (!defaultTaken && kept.Count > 0)
            {
                var live = await _db.UserCompanies
                    .Where(uc => uc.UserNo == userNo && uc.IsDeleted == Deleted)
                    .ToListAsync(ct);

                if (live.Count > 0)
                {
                    live[0].IsDefault = 1;
                    await _db.SaveChangesAsync(ct);
                }
            }

            _logger.LogInformation("Saved {Count} company mapping(s) for userNo={UserNo}", rows.Count, userNo);

            return await GetMappingsAsync(userNo, ct);
        }, cancellationToken);

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> LoadUserAsync(long userNo, CancellationToken ct) =>
        await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserNo == userNo && u.IsDeleted == Deleted, ct)
        ?? throw new NotFoundException("User not found: userNo=" + userNo);

    private long ActiveCompany() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    private static Sys1107MappingDto ToDto(UserCompany uc, IReadOnlyDictionary<long, string?> companyNames) => new()
    {
        UserCompanyNo = uc.UserCompanyNo,
        CompanyNo = uc.CompanyNo,
        CompanyName = companyNames.TryGetValue(uc.CompanyNo, out var name) ? name : null,
        IsDefault = uc.IsDefault,
        IsOwner = uc.IsOwner,
        IsActive = uc.IsActive
    };
}
