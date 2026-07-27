using System.Globalization;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Domain.Hrm;
using AidlyErp.Application.Common.Interfaces;

namespace AidlyErp.Application.Hrm.Services;

public record LeaveRules(HrmLeavePolicySetup? Policy, HrmLeaveApplicationRule? Rule)
{
    public bool HasRule => Rule != null;
    public bool HasPolicy => Policy != null;
}

public interface ILeaveRuleEngine
{
    Task<LeaveRules> ResolveRulesAsync(long leaveTypeNo, HrmEmployee employee, CancellationToken cancellationToken = default);
    Task<decimal> ComputeChargeableDaysAsync(DateTime fromDate, DateTime toDate, short? isHalfDay, LeaveRules rules, long? branchNo, CancellationToken cancellationToken = default);
    Task ValidateAsync(HrmEmployee employee, HrmLeaveType type, LeaveRules rules, DateTime fromDate, DateTime toDate, decimal totalDays, long? relieverEmployeeNo, string? attachmentPath, long? excludeApplicationNo, CancellationToken cancellationToken = default);
    void ValidateBalance(HrmLeaveType type, LeaveRules rules, decimal availableDays, decimal requestedDays);
}

public class LeaveRuleEngine : ILeaveRuleEngine
{
    private readonly IApplicationDbContext _db;
    private const short Deleted = 0;
    private static readonly List<short> BlockingStatuses = new() { 2, 3 }; // Applied, Approved

    public LeaveRuleEngine(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<LeaveRules> ResolveRulesAsync(long leaveTypeNo, HrmEmployee employee, CancellationToken cancellationToken = default)
    {
        HrmLeavePolicySetup? policy = null;
        if (employee != null && employee.DesignationNo > 0)
        {
            policy = await _db.HrmLeavePolicySetups
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.LeaveTypeNo == leaveTypeNo && p.EmployeeGroupNo == employee.DesignationNo && p.IsDeleted == Deleted, cancellationToken);
        }

        if (policy == null)
        {
            policy = await _db.HrmLeavePolicySetups
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.LeaveTypeNo == leaveTypeNo && p.EmployeeGroupNo == null && p.IsDeleted == Deleted, cancellationToken);
        }

        HrmLeaveApplicationRule? rule = null;
        if (policy != null)
        {
            rule = await _db.HrmLeaveApplicationRules
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.PolicyNo == policy.PolicyNo && r.IsDeleted == Deleted, cancellationToken);
        }

        return new LeaveRules(policy, rule);
    }

    public async Task<decimal> ComputeChargeableDaysAsync(DateTime fromDate, DateTime toDate, short? isHalfDay, LeaveRules rules, long? branchNo, CancellationToken cancellationToken = default)
    {
        bool half = isHalfDay != null && isHalfDay == 1;
        if (half && fromDate.Date == toDate.Date) return 0.5m;

        bool excludeWeekends = FlagOn(rules.HasRule ? rules.Rule?.ExcludeWeekends : null);
        bool excludeHolidays = FlagOn(rules.HasRule ? rules.Rule?.ExcludeHolidays : null);

        long days;
        if (!excludeWeekends && !excludeHolidays)
        {
            days = (long)(toDate.Date - fromDate.Date).TotalDays + 1;
        }
        else
        {
            var holidays = excludeHolidays ? await GetHolidayDatesAsync(branchNo, fromDate.Date, toDate.Date, cancellationToken) : new HashSet<DateTime>();
            days = 0;
            for (var d = fromDate.Date; d <= toDate.Date; d = d.AddDays(1))
            {
                if (excludeWeekends && IsWeekend(d)) continue;
                if (excludeHolidays && holidays.Contains(d)) continue;
                days++;
            }
        }

        if (days <= 0)
        {
            throw new InvalidOperationException("The selected range contains no working days after excluding weekends/holidays");
        }

        decimal total = days;
        return half ? total - 0.5m : total;
    }

    public async Task ValidateAsync(HrmEmployee employee, HrmLeaveType type, LeaveRules rules, DateTime fromDate, DateTime toDate, decimal totalDays, long? relieverEmployeeNo, string? attachmentPath, long? excludeApplicationNo, CancellationToken cancellationToken = default)
    {
        ValidateEligibility(employee, type, rules);
        ValidateDuration(type, rules, totalDays);
        ValidateNoticeAndBackdating(rules, fromDate.Date);
        await ValidateNoOverlapAsync(employee, fromDate.Date, toDate.Date, excludeApplicationNo, cancellationToken);
        await ValidateDepartmentCapAsync(employee, rules, fromDate.Date, toDate.Date, excludeApplicationNo, cancellationToken);
        ValidateReliever(rules, relieverEmployeeNo);
        ValidateDocumentation(type, rules, totalDays, attachmentPath);
        ValidateCriticalShift(rules);
    }

    public void ValidateBalance(HrmLeaveType type, LeaveRules rules, decimal availableDays, decimal requestedDays)
    {
        bool paid = type.IsPaid == null || type.IsPaid == 1;
        if (!paid) return;

        decimal shortfall = requestedDays - availableDays;
        if (shortfall <= 0) return;

        if (rules.HasRule && FlagOn(rules.Rule?.AllowNegativeBalance))
        {
            decimal? overdraft = rules.Rule?.MaxOverdraftDays;
            if (overdraft == null || shortfall <= overdraft) return;
            throw new InvalidOperationException($"Overdraft limit exceeded: short by {shortfall:0.##} day(s), maximum allowed overdraft is {overdraft:0.##}.");
        }

        throw new InvalidOperationException($"Insufficient leave balance: available {availableDays:0.##}, requested {requestedDays:0.##}.");
    }

    private async Task<HashSet<DateTime>> GetHolidayDatesAsync(long? branchNo, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
    {
        if (branchNo == null) return new HashSet<DateTime>();

        // holiday_date is a DATE column (DateOnly); narrow the bounds before comparing.
        var from = DateOnly.FromDateTime(fromDate);
        var to = DateOnly.FromDateTime(toDate);

        var holidays = await _db.HrmHolidays
            .AsNoTracking()
            .Where(h => h.BranchNo == branchNo && h.IsDeleted == Deleted && h.HolidayDate >= from && h.HolidayDate <= to)
            .Select(h => h.HolidayDate)
            .ToListAsync(cancellationToken);

        return holidays.Select(d => d.ToDateTime(TimeOnly.MinValue)).ToHashSet();
    }

    private static bool IsWeekend(DateTime date) => date.DayOfWeek == DayOfWeek.Friday || date.DayOfWeek == DayOfWeek.Saturday;

    private void ValidateEligibility(HrmEmployee employee, HrmLeaveType type, LeaveRules rules)
    {
        int? months = rules.HasPolicy && rules.Policy?.EligibilityServiceMonths != null
            ? rules.Policy.EligibilityServiceMonths
            : type.EligibilityServiceMonths;

        if (months == null || months <= 0 || employee == null) return;

        long served = (long)((DateTime.UtcNow.Date - employee.JoiningDate.Date).TotalDays / 30.4375);
        if (served < months)
        {
            throw new InvalidOperationException($"{type.LeaveTypeName} requires {months} month(s) of service; this employee has completed {Math.Max(served, 0)}.");
        }
    }

    private void ValidateDuration(HrmLeaveType type, LeaveRules rules, decimal totalDays)
    {
        decimal? min = rules.HasRule && rules.Rule?.MinDurationPerApp != null ? rules.Rule.MinDurationPerApp : type.MinDurationDays;
        decimal? max = rules.HasRule && rules.Rule?.MaxDurationPerApp != null ? rules.Rule.MaxDurationPerApp : type.MaxDurationDays;

        if (min != null && min > 0 && totalDays < min)
        {
            throw new InvalidOperationException($"{type.LeaveTypeName} must be at least {min:0.##} day(s) per application.");
        }
        if (max != null && max > 0 && totalDays > max)
        {
            throw new InvalidOperationException($"{type.LeaveTypeName} cannot exceed {max:0.##} day(s) per application.");
        }
    }

    private void ValidateNoticeAndBackdating(LeaveRules rules, DateTime fromDate)
    {
        if (!rules.HasRule) return;
        var today = DateTime.UtcNow.Date;

        int? notice = rules.Rule?.NoticePeriodDays;
        if (notice != null && notice > 0 && fromDate >= today)
        {
            long lead = (long)(fromDate - today).TotalDays;
            if (lead < notice)
            {
                throw new InvalidOperationException($"This leave type requires {notice} day(s) advance notice; only {lead} day(s) given.");
            }
        }

        int? maxBack = rules.Rule?.MaxRetroactiveDays;
        if (maxBack != null && fromDate < today)
        {
            long back = (long)(today - fromDate).TotalDays;
            if (back > maxBack)
            {
                throw new InvalidOperationException($"Back-dated leave is limited to {maxBack} day(s); this request is {back} day(s) in the past.");
            }
        }
    }

    private async Task ValidateNoOverlapAsync(HrmEmployee employee, DateTime fromDate, DateTime toDate, long? excludeNo, CancellationToken cancellationToken)
    {
        var query = _db.HrmLeaveApplications
            .AsNoTracking()
            .Where(a => a.EmployeeNo == employee.EmployeeNo &&
                        a.IsDeleted == Deleted &&
                        BlockingStatuses.Contains(a.Status) &&
                        a.FromDate <= toDate && a.ToDate >= fromDate);

        if (excludeNo.HasValue)
        {
            query = query.Where(a => a.LeaveApplicationNo != excludeNo.Value);
        }

        var clash = await query.FirstOrDefaultAsync(cancellationToken);
        if (clash != null)
        {
            throw new InvalidOperationException($"Overlapping leave: this employee already has a request from {clash.FromDate:yyyy-MM-dd} to {clash.ToDate:yyyy-MM-dd}.");
        }
    }

    private async Task ValidateDepartmentCapAsync(HrmEmployee employee, LeaveRules rules, DateTime fromDate, DateTime toDate, long? excludeNo, CancellationToken cancellationToken)
    {
        if (!rules.HasPolicy || rules.Policy?.MaxDeptLeavePercentage == null) return;
        decimal capPercent = rules.Policy.MaxDeptLeavePercentage.Value;
        if (capPercent <= 0 || employee.DepartmentNo <= 0) return;

        long headCount = await _db.HrmEmployees
            .AsNoTracking()
            .CountAsync(e => e.DepartmentNo == employee.DepartmentNo && e.IsDeleted == Deleted, cancellationToken);
        if (headCount <= 0) return;

        var leaveQuery = _db.HrmLeaveApplications
            .AsNoTracking()
            .Where(a => a.IsDeleted == Deleted &&
                        BlockingStatuses.Contains(a.Status) &&
                        a.FromDate <= toDate && a.ToDate >= fromDate &&
                        _db.HrmEmployees.Any(e => e.EmployeeNo == a.EmployeeNo && e.DepartmentNo == employee.DepartmentNo && e.IsDeleted == Deleted));

        if (excludeNo.HasValue)
        {
            leaveQuery = leaveQuery.Where(a => a.LeaveApplicationNo != excludeNo.Value);
        }

        long onLeave = await leaveQuery.Select(a => a.EmployeeNo).Distinct().CountAsync(cancellationToken);
        decimal projected = Math.Round((onLeave + 1m) * 100m / headCount, 2);

        if (projected > capPercent)
        {
            throw new InvalidOperationException($"Department leave limit reached: {projected:0.##}% would be on leave for this period (allowed {capPercent:0.##}%). {onLeave} of {headCount} staff are already on leave.");
        }
    }

    private void ValidateReliever(LeaveRules rules, long? relieverEmployeeNo)
    {
        if (rules.HasRule && FlagOn(rules.Rule?.IsRelieverMandatory) && relieverEmployeeNo == null)
        {
            throw new InvalidOperationException("A reliever is mandatory for this leave type.");
        }
    }

    private void ValidateDocumentation(HrmLeaveType type, LeaveRules rules, decimal totalDays, string? attachmentPath)
    {
        bool hasAttachment = !string.IsNullOrWhiteSpace(attachmentPath);
        decimal? afterDays = rules.HasRule ? rules.Rule?.AttachmentRequiredAfterDays : null;

        if (afterDays != null && afterDays > 0)
        {
            if (totalDays > afterDays && !hasAttachment)
            {
                throw new InvalidOperationException($"Supporting document is required for {type.LeaveTypeName} longer than {afterDays:0.##} day(s).");
            }
            return;
        }

        if (FlagOn(type.DocumentationRequired) && !hasAttachment)
        {
            throw new InvalidOperationException($"Supporting document is required for {type.LeaveTypeName}.");
        }
    }

    private void ValidateCriticalShift(LeaveRules rules)
    {
        if (rules.HasRule && FlagOn(rules.Rule?.IsShiftCritical))
        {
            throw new InvalidOperationException("This leave type is blocked for critical shifts; a management override is required.");
        }
    }

    private static bool FlagOn(short? flag) => flag != null && flag == 1;
}
