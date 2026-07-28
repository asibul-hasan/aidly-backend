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

namespace AidlyErp.Sys.Application.Services;

public interface ISys1008Service
{
    Task<List<Sys1008SettingDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<Sys1008SettingDto?> SaveAsync(Sys1008SettingDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(long settingNo, CancellationToken cancellationToken = default);
}

/// <summary>SYS1008 System Settings (<c>sys_setting</c>) — company key/value configuration.</summary>
public class Sys1008Service : ISys1008Service
{
    private const short Deleted = 0;
    private const short Active = 1;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;

    public Sys1008Service(ISysDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
    }

    /// <summary>
    /// Branch-scoped read: the current branch's settings PLUS company-wide ones
    /// (<c>branch_no IS NULL</c>).
    /// </summary>
    public async Task<List<Sys1008SettingDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = Company();
        var branchNo = _ctx.BranchNo;

        var rows = await _db.Settings
            .AsNoTracking()
            .Where(s => s.CompanyNo == companyNo && s.IsDeleted == Deleted
                        && (s.BranchNo == null || (branchNo != null && s.BranchNo == branchNo)))
            .OrderBy(s => s.SettingNo)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public Task<Sys1008SettingDto?> SaveAsync(Sys1008SettingDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct => dto.SettingNo != null
            ? await UpdateAsync(dto, ct)
            : await InsertAsync(dto, ct), cancellationToken);

    /// <summary>
    /// Insert. Multi-branch fan-out: one record per selected branch, or one company-wide record
    /// (<c>branch_no = NULL</c>) when no branch is selected.
    /// </summary>
    private async Task<Sys1008SettingDto?> InsertAsync(Sys1008SettingDto dto, CancellationToken ct)
    {
        var companyNo = Company();
        Setting? saved = null;

        foreach (var branchNo in ResolveBranches(dto.BranchNos))
        {
            // Per-(company, branch) uniqueness — the same key may exist in another branch.
            var duplicate = await _db.Settings
                .AnyAsync(s => s.CompanyNo == companyNo && s.BranchNo == branchNo
                               && s.SettingKey == dto.SettingKey && s.IsDeleted == Deleted, ct);

            if (duplicate)
            {
                throw new ValidationException("Setting key already exists for this branch: " + dto.SettingKey);
            }

            var e = new Setting
            {
                CompanyNo = companyNo,
                BranchNo = branchNo
            };

            ApplyFields(e, dto);

            _db.Settings.Add(e);
            await _db.SaveChangesAsync(ct);
            saved = e;
        }

        return saved == null ? null : ToDto(saved);
    }

    private async Task<Sys1008SettingDto> UpdateAsync(Sys1008SettingDto dto, CancellationToken ct)
    {
        var e = await _db.Settings
                    .FirstOrDefaultAsync(s => s.SettingNo == dto.SettingNo && s.IsDeleted == Deleted, ct)
                ?? throw new NotFoundException("Setting not found");

        if (e.CompanyNo != Company())
        {
            throw new NotFoundException("Setting not found");
        }

        ApplyFields(e, dto); // branch_no is left unchanged on update

        await _db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    private static void ApplyFields(Setting e, Sys1008SettingDto dto)
    {
        e.SettingKey = dto.SettingKey ?? string.Empty;
        e.SettingValue = dto.SettingValue;
        e.ValueType = dto.ValueType ?? 1;
        e.SettingGroup = dto.SettingGroup;
        e.Description = dto.Description;
        e.IsActive = dto.IsActive ?? Active;
    }

    public Task DeleteAsync(long settingNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var e = await _db.Settings
                        .FirstOrDefaultAsync(s => s.SettingNo == settingNo && s.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Setting not found");

            e.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    // ─── Private ─────────────────────────────────────────────────────────────

    private long Company() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    /// <summary>
    /// <c>branch_nos</c> from the DTO: null/empty = company-wide (NULL <c>branch_no</c>);
    /// otherwise one record per branch.
    /// </summary>
    private static List<long?> ResolveBranches(List<long>? branchNos)
    {
        if (branchNos == null) return new List<long?> { null };

        var cleaned = branchNos.Distinct().Select(b => (long?)b).ToList();
        return cleaned.Count == 0 ? new List<long?> { null } : cleaned;
    }

    private static Sys1008SettingDto ToDto(Setting e) => new()
    {
        SettingNo = e.SettingNo,
        BranchNo = e.BranchNo,
        SettingKey = e.SettingKey,
        SettingValue = e.SettingValue,
        ValueType = e.ValueType,
        SettingGroup = e.SettingGroup,
        Description = e.Description,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };
}
