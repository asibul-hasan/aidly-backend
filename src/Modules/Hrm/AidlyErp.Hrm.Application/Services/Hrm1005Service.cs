using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Hrm.Application.Dto;
using AidlyErp.Hrm.Domain;

namespace AidlyErp.Hrm.Application.Services;

public interface IHrm1005Service
{
    Task<List<Hrm1005LeaveTypeDto>> GetListAsync(CancellationToken ct = default);
    Task<List<Hrm1005LeaveTypeDto>> GetListByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<Hrm1005LeaveTypeDto> GetDetailAsync(long leaveTypeNo, CancellationToken ct = default);
    Task<Hrm1005LeaveTypeDto> SaveAsync(Hrm1005LeaveTypeDto dto, CancellationToken ct = default);
    Task<List<Hrm1005LeaveTypeDto>> SaveBulkAsync(List<Hrm1005LeaveTypeDto> dtoList, CancellationToken ct = default);
    Task DeleteAsync(long leaveTypeNo, CancellationToken ct = default);

    // Leave Policy Setup (child)
    Task<List<HrmLeavePolicySetupDto>> GetPolicyListAsync(CancellationToken ct = default);
    Task<HrmLeavePolicySetupDto> GetPolicyDetailAsync(long policyNo, CancellationToken ct = default);
    Task<HrmLeavePolicySetupDto> SavePolicyAsync(HrmLeavePolicySetupDto dto, CancellationToken ct = default);
    Task DeletePolicyAsync(long policyNo, CancellationToken ct = default);
}

public class Hrm1005Service : IHrm1005Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private const short Active = 1;
    private const short Deleted = 0;

    public Hrm1005Service(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<Hrm1005LeaveTypeDto>> GetListAsync(CancellationToken ct = default)
    {
        long? branchNo = _ctx.BranchNo;
        return await _db.HrmLeaveTypes.AsNoTracking()
            .Where(x => x.IsDeleted == Deleted && (branchNo == null || x.BranchNo == branchNo))
            .OrderBy(x => x.LeaveTypeNo)
            .Select(x => ToDto(x))
            .ToListAsync(ct);
    }

    public async Task<List<Hrm1005LeaveTypeDto>> GetListByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmLeaveTypes.AsNoTracking()
            .Where(x => x.BranchNo == branchNo && x.IsDeleted == Deleted)
            .OrderBy(x => x.LeaveTypeNo)
            .Select(x => ToDto(x))
            .ToListAsync(ct);

    public async Task<Hrm1005LeaveTypeDto> GetDetailAsync(long leaveTypeNo, CancellationToken ct = default) =>
        ToDto(await LoadLiveAsync(leaveTypeNo, ct));

    // ── Write ─────────────────────────────────────────────────────────────

    public async Task<Hrm1005LeaveTypeDto> SaveAsync(Hrm1005LeaveTypeDto dto, CancellationToken ct = default)
    {
        return dto.LeaveTypeNo.HasValue && dto.LeaveTypeNo.Value > 0
            ? await UpdateAsync(dto.LeaveTypeNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    public async Task<List<Hrm1005LeaveTypeDto>> SaveBulkAsync(List<Hrm1005LeaveTypeDto> dtoList, CancellationToken ct = default)
    {
        var results = new List<Hrm1005LeaveTypeDto>();
        foreach (var dto in dtoList)
            results.Add(await SaveAsync(dto, ct));
        return results;
    }

    public async Task DeleteAsync(long leaveTypeNo, CancellationToken ct = default)
    {
        var entity = await LoadLiveAsync(leaveTypeNo, ct);
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Hrm1005LeaveTypeDto> InsertAsync(Hrm1005LeaveTypeDto dto, CancellationToken ct)
    {
        long branchNo = await ResolveBranchAsync(dto.BranchNo, ct);
        string name = TrimRequired(dto.LeaveTypeName, "Leave name");

        string leaveTypeId = string.IsNullOrWhiteSpace(dto.LeaveTypeId)
            ? await NextLeaveTypeIdAsync(ct)
            : dto.LeaveTypeId.Trim().ToUpperInvariant();

        // Branch-scoped uniqueness
        if (await _db.HrmLeaveTypes.AnyAsync(x => x.LeaveTypeId == leaveTypeId && x.BranchNo == branchNo && x.IsDeleted == Deleted, ct))
            throw new ValidationException($"Leave code already exists in this branch: {leaveTypeId}");

        var entity = new HrmLeaveType
        {
            BranchNo = branchNo,
            CompanyNo = _ctx.CompanyNo,
            LeaveTypeId = leaveTypeId,
            LeaveTypeName = name,
            IsPaid = NormalizeFlag(dto.IsPaid, Active, "is_paid"),
            IsActive = NormalizeFlag(dto.IsActive, Active, "is_active"),
            IsDeleted = Deleted,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        if (dto.StatutoryCategory != null) entity.StatutoryCategory = dto.StatutoryCategory;
        if (dto.AccrualMethod.HasValue) entity.AccrualMethod = dto.AccrualMethod;
        if (dto.RequiresApproval.HasValue) entity.RequiresApproval = dto.RequiresApproval;
        if (dto.IsEncashable.HasValue) entity.IsEncashable = dto.IsEncashable;
        if (dto.DefaultDaysPerYear.HasValue) entity.DefaultDaysPerYear = dto.DefaultDaysPerYear;
        if (dto.MaxCarryForwardDays.HasValue) entity.MaxCarryForwardDays = dto.MaxCarryForwardDays;
        if (dto.IsCarryForwardAllowed.HasValue) entity.IsCarryForwardAllowed = dto.IsCarryForwardAllowed;
        if (dto.EligibilityServiceMonths.HasValue) entity.EligibilityServiceMonths = dto.EligibilityServiceMonths;
        if (dto.DocumentationRequired.HasValue) entity.DocumentationRequired = dto.DocumentationRequired;
        if (dto.CarryForwardValidityMonths.HasValue) entity.CarryForwardValidityMonths = dto.CarryForwardValidityMonths;
        if (dto.IsLapsable.HasValue) entity.IsLapsable = dto.IsLapsable;
        if (dto.MinDurationDays.HasValue) entity.MinDurationDays = dto.MinDurationDays;
        if (dto.MaxDurationDays.HasValue) entity.MaxDurationDays = dto.MaxDurationDays;
        if (dto.Remarks != null) entity.Remarks = dto.Remarks;

        _db.HrmLeaveTypes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private async Task<Hrm1005LeaveTypeDto> UpdateAsync(long leaveTypeNo, Hrm1005LeaveTypeDto dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(leaveTypeNo, ct);

        if (dto.LeaveTypeId != null)
        {
            string newId = string.IsNullOrWhiteSpace(dto.LeaveTypeId)
                ? entity.LeaveTypeId
                : dto.LeaveTypeId.Trim().ToUpperInvariant();
            bool dup = await _db.HrmLeaveTypes.AnyAsync(x =>
                x.LeaveTypeId == newId && x.BranchNo == entity.BranchNo && x.IsDeleted == Deleted && x.LeaveTypeNo != leaveTypeNo, ct);
            if (dup) throw new ValidationException($"Leave code already in use in this branch: {newId}");
            entity.LeaveTypeId = newId;
        }

        if (dto.LeaveTypeName != null) entity.LeaveTypeName = TrimRequired(dto.LeaveTypeName, "Leave name");
        if (dto.IsPaid.HasValue) entity.IsPaid = NormalizeFlag(dto.IsPaid.Value, Active, "is_paid");
        if (dto.StatutoryCategory != null) entity.StatutoryCategory = dto.StatutoryCategory;
        if (dto.AccrualMethod.HasValue) entity.AccrualMethod = dto.AccrualMethod;
        if (dto.RequiresApproval.HasValue) entity.RequiresApproval = dto.RequiresApproval;
        if (dto.IsEncashable.HasValue) entity.IsEncashable = dto.IsEncashable;
        if (dto.DefaultDaysPerYear.HasValue) entity.DefaultDaysPerYear = dto.DefaultDaysPerYear;
        if (dto.MaxCarryForwardDays.HasValue) entity.MaxCarryForwardDays = dto.MaxCarryForwardDays;
        if (dto.IsCarryForwardAllowed.HasValue) entity.IsCarryForwardAllowed = dto.IsCarryForwardAllowed;
        if (dto.EligibilityServiceMonths.HasValue) entity.EligibilityServiceMonths = dto.EligibilityServiceMonths;
        if (dto.DocumentationRequired.HasValue) entity.DocumentationRequired = dto.DocumentationRequired;
        if (dto.CarryForwardValidityMonths.HasValue) entity.CarryForwardValidityMonths = dto.CarryForwardValidityMonths;
        if (dto.IsLapsable.HasValue) entity.IsLapsable = dto.IsLapsable;
        if (dto.MinDurationDays.HasValue) entity.MinDurationDays = dto.MinDurationDays;
        if (dto.MaxDurationDays.HasValue) entity.MaxDurationDays = dto.MaxDurationDays;
        if (dto.Remarks != null) entity.Remarks = dto.Remarks;
        if (dto.IsActive.HasValue) entity.IsActive = NormalizeFlag(dto.IsActive.Value, Active, "is_active");

        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    // ── Policy (child) ────────────────────────────────────────────────────

    public async Task<List<HrmLeavePolicySetupDto>> GetPolicyListAsync(CancellationToken ct = default)
    {
        long? branchNo = _ctx.BranchNo;
        return await _db.HrmLeavePolicySetups.AsNoTracking()
            .Where(p => p.IsDeleted == Deleted && (branchNo == null || p.BranchNo == branchNo))
            .Select(p => ToPolicyDto(p))
            .ToListAsync(ct);
    }

    public async Task<HrmLeavePolicySetupDto> GetPolicyDetailAsync(long policyNo, CancellationToken ct = default) =>
        ToPolicyDto(await LoadLivePolicyAsync(policyNo, ct));

    public async Task<HrmLeavePolicySetupDto> SavePolicyAsync(HrmLeavePolicySetupDto dto, CancellationToken ct = default)
    {
        if (dto.PolicyNo.HasValue && dto.PolicyNo.Value > 0)
            return await UpdatePolicyAsync(dto.PolicyNo.Value, dto, ct);
        return await InsertPolicyAsync(dto, ct);
    }

    public async Task DeletePolicyAsync(long policyNo, CancellationToken ct = default)
    {
        var entity = await LoadLivePolicyAsync(policyNo, ct);
        entity.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    private async Task<HrmLeavePolicySetupDto> InsertPolicyAsync(HrmLeavePolicySetupDto dto, CancellationToken ct)
    {
        long branchNo = dto.BranchNo.HasValue && dto.BranchNo.Value > 0
            ? dto.BranchNo.Value
            : _ctx.BranchNo ?? throw new ValidationException("Branch is required");

        var entity = new HrmLeavePolicySetup
        {
            PolicyName = dto.PolicyName ?? "Leave Policy",
            LeaveTypeNo = dto.LeaveTypeNo,
            CompanyNo = _ctx.CompanyNo ?? 0,
            BranchNo = branchNo,
            DefaultDays = dto.DefaultDays ?? 0,
            IsDeleted = Deleted,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        ApplyPolicyFields(dto, entity);
        _db.HrmLeavePolicySetups.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToPolicyDto(entity);
    }

    private async Task<HrmLeavePolicySetupDto> UpdatePolicyAsync(long policyNo, HrmLeavePolicySetupDto dto, CancellationToken ct)
    {
        var entity = await LoadLivePolicyAsync(policyNo, ct);
        if (dto.PolicyName != null) entity.PolicyName = dto.PolicyName;
        if (dto.LeaveTypeNo > 0) entity.LeaveTypeNo = dto.LeaveTypeNo;
        ApplyPolicyFields(dto, entity);
        entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToPolicyDto(entity);
    }

    private static void ApplyPolicyFields(HrmLeavePolicySetupDto dto, HrmLeavePolicySetup e)
    {
        if (dto.EmployeeGroupNo.HasValue) e.EmployeeGroupNo = dto.EmployeeGroupNo;
        if (dto.AccrualMethod.HasValue) e.AccrualMethod = dto.AccrualMethod;
        if (dto.DefaultDays.HasValue) e.DefaultDays = dto.DefaultDays.Value;
        if (dto.WorkDaysPerLeaveDay.HasValue) e.WorkDaysPerLeaveDay = dto.WorkDaysPerLeaveDay;
        if (dto.ProrationMethod.HasValue) e.ProrationMethod = dto.ProrationMethod;
        if (dto.EligibilityServiceMonths.HasValue) e.EligibilityServiceMonths = dto.EligibilityServiceMonths;
        if (dto.IsCarryForwardAllowed.HasValue) e.IsCarryForwardAllowed = dto.IsCarryForwardAllowed;
        if (dto.MaxCarryForwardDays.HasValue) e.MaxCarryForwardDays = dto.MaxCarryForwardDays;
        if (dto.CarryForwardValidityMonths.HasValue) e.CarryForwardValidityMonths = dto.CarryForwardValidityMonths;
        if (dto.IsEncashable.HasValue) e.IsEncashable = dto.IsEncashable;
        if (dto.MaxEncashableDays.HasValue) e.MaxEncashableDays = dto.MaxEncashableDays;
        if (dto.EncashmentFormula != null) e.EncashmentFormula = dto.EncashmentFormula;
        if (dto.IsLapsable.HasValue) e.IsLapsable = dto.IsLapsable;
        if (dto.MaxDeptLeavePercentage.HasValue) e.MaxDeptLeavePercentage = dto.MaxDeptLeavePercentage;
        if (dto.SpecializedConfig != null) e.SpecializedConfig = dto.SpecializedConfig;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<HrmLeaveType> LoadLiveAsync(long leaveTypeNo, CancellationToken ct) =>
        await _db.HrmLeaveTypes.FirstOrDefaultAsync(x => x.LeaveTypeNo == leaveTypeNo && x.IsDeleted == Deleted, ct)
            ?? throw new NotFoundException($"Leave type not found: leaveTypeNo={leaveTypeNo}");

    private async Task<HrmLeavePolicySetup> LoadLivePolicyAsync(long policyNo, CancellationToken ct) =>
        await _db.HrmLeavePolicySetups.FirstOrDefaultAsync(x => x.PolicyNo == policyNo && x.IsDeleted == Deleted, ct)
            ?? throw new NotFoundException($"Policy not found: policyNo={policyNo}");

    private async Task<long> ResolveBranchAsync(long? branchNoFromDto, CancellationToken ct)
    {
        long? branchNo = branchNoFromDto ?? _ctx.BranchNo;
        if (!branchNo.HasValue) throw new ValidationException("Branch is required");
        var exists = await _db.Branches.AnyAsync(b => b.BranchNo == branchNo.Value && b.IsDeleted == Deleted, ct);
        if (!exists) throw new NotFoundException($"Branch not found: branchNo={branchNo}");
        return branchNo.Value;
    }

    private async Task<string> NextLeaveTypeIdAsync(CancellationToken ct)
    {
        long next = await _db.HrmLeaveTypes.CountAsync(ct) + 1;
        string id;
        do { id = $"LVT{next++:D4}"; }
        while (await _db.HrmLeaveTypes.AnyAsync(x => x.LeaveTypeId == id && x.IsDeleted == Deleted, ct));
        return id;
    }

    private static string TrimRequired(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{label} is required");
        var trimmed = value.Trim();
        if (trimmed.Length == 0) throw new ValidationException($"{label} cannot be blank");
        return trimmed;
    }

    private static short NormalizeFlag(short? value, short defaultValue, string fieldName)
    {
        if (!value.HasValue) return defaultValue;
        if (value.Value is 0 or 1) return value.Value;
        throw new ValidationException($"{fieldName} must be 0 or 1");
    }

    private static Hrm1005LeaveTypeDto ToDto(HrmLeaveType e) => new()
    {
        LeaveTypeNo = e.LeaveTypeNo,
        LeaveTypeId = e.LeaveTypeId,
        LeaveTypeName = e.LeaveTypeName,
        IsPaid = e.IsPaid,
        StatutoryCategory = e.StatutoryCategory,
        AccrualMethod = e.AccrualMethod,
        RequiresApproval = e.RequiresApproval,
        IsEncashable = e.IsEncashable,
        DefaultDaysPerYear = e.DefaultDaysPerYear,
        MaxCarryForwardDays = e.MaxCarryForwardDays,
        IsCarryForwardAllowed = e.IsCarryForwardAllowed,
        EligibilityServiceMonths = e.EligibilityServiceMonths,
        DocumentationRequired = e.DocumentationRequired,
        CarryForwardValidityMonths = e.CarryForwardValidityMonths,
        IsLapsable = e.IsLapsable,
        MinDurationDays = e.MinDurationDays,
        MaxDurationDays = e.MaxDurationDays,
        BranchNo = e.BranchNo,
        Remarks = e.Remarks,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };

    private static HrmLeavePolicySetupDto ToPolicyDto(HrmLeavePolicySetup e) => new()
    {
        PolicyNo = e.PolicyNo,
        PolicyName = e.PolicyName,
        LeaveTypeNo = e.LeaveTypeNo,
        EmployeeGroupNo = e.EmployeeGroupNo,
        AccrualMethod = e.AccrualMethod,
        DefaultDays = e.DefaultDays,
        WorkDaysPerLeaveDay = e.WorkDaysPerLeaveDay,
        ProrationMethod = e.ProrationMethod,
        EligibilityServiceMonths = e.EligibilityServiceMonths,
        IsCarryForwardAllowed = e.IsCarryForwardAllowed,
        MaxCarryForwardDays = e.MaxCarryForwardDays,
        CarryForwardValidityMonths = e.CarryForwardValidityMonths,
        IsEncashable = e.IsEncashable,
        MaxEncashableDays = e.MaxEncashableDays,
        EncashmentFormula = e.EncashmentFormula,
        IsLapsable = e.IsLapsable,
        MaxDeptLeavePercentage = e.MaxDeptLeavePercentage,
        SpecializedConfig = e.SpecializedConfig,
        BranchNo = e.BranchNo,
        RowVersion = e.RowVersion
    };
}
