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

public interface IHrmLeavePolicyService
{
    Task<List<HrmLeavePolicySetupDto>> GetPoliciesAsync(CancellationToken ct = default);
    Task<List<HrmLeavePolicySetupDto>> GetPoliciesByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<HrmLeavePolicySetupDto> GetDetailAsync(long policyNo, CancellationToken ct = default);
    Task<HrmLeavePolicySetupDto> SaveAsync(HrmLeavePolicySetupDto dto, CancellationToken ct = default);
    Task DeleteAsync(long policyNo, CancellationToken ct = default);
}

public class HrmLeavePolicyService : IHrmLeavePolicyService
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    public HrmLeavePolicyService(IHrmDbContext db, ICompanyBranchContext ctx) { _db = db; _ctx = ctx; }

    public async Task<List<HrmLeavePolicySetupDto>> GetPoliciesAsync(CancellationToken ct = default) =>
        await _db.HrmLeavePolicySetups.AsNoTracking()
            .Where(p => p.IsDeleted == 0)
            .Select(p => MapToDto(p))
            .ToListAsync(ct);

    public async Task<List<HrmLeavePolicySetupDto>> GetPoliciesByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await _db.HrmLeavePolicySetups.AsNoTracking()
            .Where(p => p.BranchNo == branchNo && p.IsDeleted == 0)
            .Select(p => MapToDto(p))
            .ToListAsync(ct);

    public async Task<HrmLeavePolicySetupDto> GetDetailAsync(long policyNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmLeavePolicySetups.AsNoTracking().FirstOrDefaultAsync(p => p.PolicyNo == policyNo && p.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Leave policy not found: policyNo={policyNo}");
        return MapToDto(entity);
    }

    public async Task<HrmLeavePolicySetupDto> SaveAsync(HrmLeavePolicySetupDto dto, CancellationToken ct = default)
    {
        if (dto.PolicyNo > 0)
        {
            var entity = await _db.HrmLeavePolicySetups.FirstOrDefaultAsync(p => p.PolicyNo == dto.PolicyNo && p.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Leave policy not found: policyNo={dto.PolicyNo}");
            if (dto.PolicyName != null) entity.PolicyName = dto.PolicyName;
            if (dto.LeaveTypeNo > 0) entity.LeaveTypeNo = dto.LeaveTypeNo;
            if (dto.EmployeeGroupNo.HasValue) entity.EmployeeGroupNo = dto.EmployeeGroupNo;
            if (dto.AccrualMethod.HasValue) entity.AccrualMethod = dto.AccrualMethod;
            if (dto.DefaultDays.HasValue) entity.DefaultDays = dto.DefaultDays.Value;
            if (dto.WorkDaysPerLeaveDay.HasValue) entity.WorkDaysPerLeaveDay = dto.WorkDaysPerLeaveDay;
            if (dto.ProrationMethod.HasValue) entity.ProrationMethod = dto.ProrationMethod;
            if (dto.EligibilityServiceMonths.HasValue) entity.EligibilityServiceMonths = dto.EligibilityServiceMonths;
            if (dto.IsCarryForwardAllowed.HasValue) entity.IsCarryForwardAllowed = dto.IsCarryForwardAllowed;
            if (dto.MaxCarryForwardDays.HasValue) entity.MaxCarryForwardDays = dto.MaxCarryForwardDays;
            if (dto.CarryForwardValidityMonths.HasValue) entity.CarryForwardValidityMonths = dto.CarryForwardValidityMonths;
            if (dto.IsEncashable.HasValue) entity.IsEncashable = dto.IsEncashable;
            if (dto.MaxEncashableDays.HasValue) entity.MaxEncashableDays = dto.MaxEncashableDays;
            if (dto.EncashmentFormula != null) entity.EncashmentFormula = dto.EncashmentFormula;
            if (dto.IsLapsable.HasValue) entity.IsLapsable = dto.IsLapsable;
            if (dto.MaxDeptLeavePercentage.HasValue) entity.MaxDeptLeavePercentage = dto.MaxDeptLeavePercentage;
            if (dto.SpecializedConfig != null) entity.SpecializedConfig = dto.SpecializedConfig;
            entity.UpdatedBy = _ctx.CurrentUserNo(); entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return MapToDto(entity);
        }

        long branchNo = _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch is required");
        if (dto.LeaveTypeNo <= 0) throw new ValidationException("Leave type is required");

        var newPolicy = new HrmLeavePolicySetup
        {
            PolicyName = dto.PolicyName ?? "Leave Policy",
            LeaveTypeNo = dto.LeaveTypeNo,
            EmployeeGroupNo = dto.EmployeeGroupNo,
            AccrualMethod = dto.AccrualMethod,
            DefaultDays = dto.DefaultDays ?? 0,
            WorkDaysPerLeaveDay = dto.WorkDaysPerLeaveDay,
            ProrationMethod = dto.ProrationMethod,
            EligibilityServiceMonths = dto.EligibilityServiceMonths,
            IsCarryForwardAllowed = dto.IsCarryForwardAllowed,
            MaxCarryForwardDays = dto.MaxCarryForwardDays,
            CarryForwardValidityMonths = dto.CarryForwardValidityMonths,
            IsEncashable = dto.IsEncashable,
            MaxEncashableDays = dto.MaxEncashableDays,
            EncashmentFormula = dto.EncashmentFormula,
            IsLapsable = dto.IsLapsable,
            MaxDeptLeavePercentage = dto.MaxDeptLeavePercentage,
            SpecializedConfig = dto.SpecializedConfig,
            CompanyNo = _ctx.CompanyNo ?? 0,
            BranchNo = branchNo,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        _db.HrmLeavePolicySetups.Add(newPolicy);
        await _db.SaveChangesAsync(ct);
        return MapToDto(newPolicy);
    }

    public async Task DeleteAsync(long policyNo, CancellationToken ct = default)
    {
        var entity = await _db.HrmLeavePolicySetups.FirstOrDefaultAsync(p => p.PolicyNo == policyNo && p.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Leave policy not found: policyNo={policyNo}");
        entity.IsDeleted = 1; entity.IsActive = 0;
        entity.DeletedBy = _ctx.CurrentUserNo(); entity.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static HrmLeavePolicySetupDto MapToDto(HrmLeavePolicySetup p) => new()
    {
        PolicyNo = p.PolicyNo,
        PolicyName = p.PolicyName,
        LeaveTypeNo = p.LeaveTypeNo,
        EmployeeGroupNo = p.EmployeeGroupNo,
        AccrualMethod = p.AccrualMethod,
        DefaultDays = p.DefaultDays,
        WorkDaysPerLeaveDay = p.WorkDaysPerLeaveDay,
        ProrationMethod = p.ProrationMethod,
        EligibilityServiceMonths = p.EligibilityServiceMonths,
        IsCarryForwardAllowed = p.IsCarryForwardAllowed,
        MaxCarryForwardDays = p.MaxCarryForwardDays,
        CarryForwardValidityMonths = p.CarryForwardValidityMonths,
        IsEncashable = p.IsEncashable,
        MaxEncashableDays = p.MaxEncashableDays,
        EncashmentFormula = p.EncashmentFormula,
        IsLapsable = p.IsLapsable,
        MaxDeptLeavePercentage = p.MaxDeptLeavePercentage,
        SpecializedConfig = p.SpecializedConfig,
        BranchNo = p.BranchNo,
        RowVersion = p.RowVersion
    };
}
