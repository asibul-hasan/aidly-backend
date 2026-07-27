using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Sys.Dto;
using AidlyErp.Domain.Hrm;
using AidlyErp.Domain.Sys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Application.Sys.Services;

public interface ISys1002Service
{
    Task<List<Sys1002BranchDto>> GetBranchListAsync(CancellationToken cancellationToken = default);

    Task<Sys1002BranchDto> GetBranchDetailAsync(long branchNo, CancellationToken cancellationToken = default);

    Task<Sys1002BranchDto> SaveAsync(Sys1002BranchDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(long branchNo, CancellationToken cancellationToken = default);

    Task<List<HrmEmployee>> GetEmployeesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Dedicated service for the SYS1002 Branch Setup form.</summary>
public class Sys1002Service : ISys1002Service
{
    private const short Active = 1;
    private const short Deleted = 0;

    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<Sys1002Service> _logger;

    public Sys1002Service(IApplicationDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                          ILogger<Sys1002Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    // ─── Reads ───────────────────────────────────────────────────────────────

    public async Task<List<Sys1002BranchDto>> GetBranchListAsync(CancellationToken cancellationToken = default)
    {
        // All branches for the current company, ascending by branch_no.
        var branches = await _db.Branches
            .AsNoTracking()
            .Where(b => b.IsDeleted == Deleted)
            .OrderBy(b => b.BranchNo)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(branches, cancellationToken);
    }

    public async Task<Sys1002BranchDto> GetBranchDetailAsync(long branchNo, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Branches
                         .AsNoTracking()
                         .FirstOrDefaultAsync(b => b.BranchNo == branchNo && b.IsDeleted == Deleted, cancellationToken)
                     ?? throw new NotFoundException("Branch not found: branchNo=" + branchNo);

        return (await ToDtosAsync(new[] { entity }, cancellationToken))[0];
    }

    public async Task<List<HrmEmployee>> GetEmployeesAsync(CancellationToken cancellationToken = default) =>
        await _db.HrmEmployees
            .AsNoTracking()
            .Where(e => e.IsActive == Active && e.IsDeleted == Deleted)
            .OrderBy(e => e.EmployeeId)
            .ToListAsync(cancellationToken);

    // ─── Writes ──────────────────────────────────────────────────────────────

    public Task<Sys1002BranchDto> SaveAsync(Sys1002BranchDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct => dto.BranchNo != null
            ? await UpdateAsync(dto.BranchNo.Value, dto, ct)
            : await InsertAsync(dto, ct), cancellationToken);

    public Task DeleteAsync(long branchNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await _db.Branches
                             .FirstOrDefaultAsync(b => b.BranchNo == branchNo && b.IsDeleted == Deleted, ct)
                         ?? throw new NotFoundException("Branch not found: branchNo=" + branchNo);

            if (IsMain(entity.IsMainBranch))
            {
                throw new ValidationException("Main branch cannot be deleted.");
            }

            entity.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Branch deleted: branchNo={BranchNo}", branchNo);
        }, cancellationToken);

    // ─── Private ─────────────────────────────────────────────────────────────

    private async Task<Sys1002BranchDto> InsertAsync(Sys1002BranchDto dto, CancellationToken ct)
    {
        var companyExists = await _db.Companies
            .AnyAsync(c => c.CompanyNo == dto.CompanyNo && c.IsDeleted == Deleted, ct);

        if (!companyExists)
        {
            throw new NotFoundException("Company not found: companyNo=" + dto.CompanyNo);
        }

        if (string.IsNullOrWhiteSpace(dto.BranchId))
        {
            dto.BranchId = await GenerateBranchIdAsync(dto.BranchName, dto.City, ct);
        }
        else
        {
            var duplicate = await _db.Branches
                .AnyAsync(b => b.CompanyNo == dto.CompanyNo && b.BranchId == dto.BranchId && b.IsDeleted == Deleted, ct);

            if (duplicate)
            {
                throw new ValidationException("Branch ID already exists in this company: " + dto.BranchId);
            }
        }

        if (IsMain(dto.IsMainBranch))
        {
            var existingMain = await _db.Branches
                .FirstOrDefaultAsync(b => b.CompanyNo == dto.CompanyNo && b.IsMainBranch == Active
                                          && b.IsDeleted == Deleted, ct);

            if (existingMain != null)
            {
                throw new ValidationException(
                    "A main branch already exists for this company: " + existingMain.BranchName);
            }
        }

        var entity = new Branch();
        ApplyDtoToEntity(dto, entity);

        _db.Branches.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Branch inserted: branchNo={BranchNo}, branchId={BranchId}",
            entity.BranchNo, entity.BranchId);

        return (await ToDtosAsync(new[] { entity }, ct))[0];
    }

    private async Task<Sys1002BranchDto> UpdateAsync(long branchNo, Sys1002BranchDto dto, CancellationToken ct)
    {
        var entity = await _db.Branches
                         .FirstOrDefaultAsync(b => b.BranchNo == branchNo && b.IsDeleted == Deleted, ct)
                     ?? throw new NotFoundException("Branch not found: branchNo=" + branchNo);

        // is_main_branch cannot be changed after creation
        dto.IsMainBranch = entity.IsMainBranch;

        // Check duplicate branch_id on a different record
        if (dto.BranchId != null)
        {
            var existing = await _db.Branches
                .FirstOrDefaultAsync(b => b.CompanyNo == dto.CompanyNo && b.BranchId == dto.BranchId
                                          && b.IsDeleted == Deleted, ct);

            if (existing != null && existing.BranchNo != branchNo)
            {
                throw new ValidationException("Branch ID already in use: " + dto.BranchId);
            }
        }

        ApplyDtoToEntity(dto, entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Branch updated: branchNo={BranchNo}", entity.BranchNo);

        return (await ToDtosAsync(new[] { entity }, ct))[0];
    }

    /// <summary>
    /// Maps branches to DTOs, resolving manager names in a single batched query.
    /// The Java version resolves the manager per row (an N+1); the result is identical.
    /// </summary>
    private async Task<List<Sys1002BranchDto>> ToDtosAsync(IReadOnlyCollection<Branch> branches, CancellationToken ct)
    {
        var managerNos = branches
            .Where(b => b.ManagerEmployeeNo != null)
            .Select(b => b.ManagerEmployeeNo!.Value)
            .Distinct()
            .ToList();

        var managerNames = managerNos.Count == 0
            ? new Dictionary<long, string>()
            : await _db.HrmEmployees
                .AsNoTracking()
                .Where(e => managerNos.Contains(e.EmployeeNo) && e.IsDeleted == Deleted)
                .Select(e => new { e.EmployeeNo, e.FirstName, e.LastName })
                .ToDictionaryAsync(
                    e => e.EmployeeNo,
                    e => (e.FirstName + " " + (e.LastName ?? string.Empty)).Trim(),
                    ct);

        return branches.Select(e =>
        {
            var dto = new Sys1002BranchDto
            {
                BranchNo = e.BranchNo,
                CompanyNo = e.CompanyNo,
                BranchId = e.BranchId,
                BranchName = e.BranchName,
                BranchNameNls = e.BranchNameNls,
                BranchType = e.BranchType,
                BranchAddr1 = e.BranchAddr1,
                BranchAddr2 = e.BranchAddr2,
                City = e.City,
                PostCode = e.PostCode,
                MobileNo = e.MobileNo,
                ContactNo = e.ContactNo,
                Email = e.Email,
                ManagerEmployeeNo = e.ManagerEmployeeNo,
                IsMainBranch = e.IsMainBranch,
                IsActive = e.IsActive,
                RowVersion = e.RowVersion
            };

            if (e.ManagerEmployeeNo != null && managerNames.TryGetValue(e.ManagerEmployeeNo.Value, out var name))
            {
                dto.ManagerName = name;
            }

            return dto;
        }).ToList();
    }

    private static void ApplyDtoToEntity(Sys1002BranchDto dto, Branch e)
    {
        if (dto.CompanyNo != null) e.CompanyNo = dto.CompanyNo;
        if (dto.BranchId != null) e.BranchId = dto.BranchId;
        if (dto.BranchName != null) e.BranchName = dto.BranchName;
        if (dto.BranchNameNls != null) e.BranchNameNls = dto.BranchNameNls;
        if (dto.BranchType != null) e.BranchType = dto.BranchType;
        if (dto.BranchAddr1 != null) e.BranchAddr1 = dto.BranchAddr1;
        if (dto.BranchAddr2 != null) e.BranchAddr2 = dto.BranchAddr2;
        if (dto.City != null) e.City = dto.City;
        if (dto.PostCode != null) e.PostCode = dto.PostCode;
        if (dto.MobileNo != null) e.MobileNo = dto.MobileNo;
        if (dto.ContactNo != null) e.ContactNo = dto.ContactNo;
        if (dto.Email != null) e.Email = dto.Email;
        if (dto.ManagerEmployeeNo != null) e.ManagerEmployeeNo = dto.ManagerEmployeeNo;
        if (dto.IsMainBranch != null) e.IsMainBranch = dto.IsMainBranch.Value;
        if (dto.IsActive != null) e.IsActive = dto.IsActive.Value;
    }

    private static bool IsMain(short? flag) => flag is 1;

    /// <summary>
    /// Auto-generates a branch code: first word of the branch name, the city, and a sequence
    /// number — e.g. <c>DHAKA-DHAKA-003</c>.
    /// </summary>
    private async Task<string> GenerateBranchIdAsync(string? branchName, string? city, CancellationToken ct)
    {
        var prefix = !string.IsNullOrWhiteSpace(branchName)
            ? branchName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToUpperInvariant()
            : "BR";

        var cityPart = !string.IsNullOrWhiteSpace(city) ? city.Trim().ToUpperInvariant() : "NA";

        var count = await _db.Branches.CountAsync(b => b.IsDeleted == Deleted, ct) + 1;

        return $"{prefix}-{cityPart}-{count:D3}";
    }
}
