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

/// <summary>
/// HRM_1301 Leave Application — full port of Java Hrm1301Service (705 lines).
/// Implements the leave state machine and balance-plus-immutable-ledger discipline:
/// Draft → Applied (submit), Applied → Approved/Rejected, Approved → Cancelled.
/// On approve: validate balance, increment consumed_days, append Consume(−) ledger row.
/// On cancel of approved: decrement consumed_days, append Reverse(+) ledger row.
/// </summary>
public interface IHrm1301Service
{
    Task<List<Hrm1301LeaveApplicationDto>> GetListAsync(CancellationToken ct = default);
    Task<List<Hrm1301LeaveApplicationDto>> GetListByEmployeeAsync(long employeeNo, CancellationToken ct = default);
    Task<List<Hrm1301LeaveApplicationDto>> GetFilteredHistoryAsync(long employeeNo, long? leaveTypeNo, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);
    Task<Hrm1301LeaveApplicationDto> GetDetailAsync(long leaveApplicationNo, CancellationToken ct = default);
    Task<List<Hrm1301LeaveApplicationDto>> GetApprovalListAsync(long? branchNo, CancellationToken ct = default);
    Task<List<Hrm1301BalanceDto>> GetBalanceAsync(long employeeNo, int? leaveYear, CancellationToken ct = default);
    Task<Hrm1301LeaveApplicationDto> SaveAsync(Hrm1301LeaveApplicationDto dto, CancellationToken ct = default);
    Task<Hrm1301LeaveApplicationDto> SubmitAsync(long leaveApplicationNo, CancellationToken ct = default);
    Task<Hrm1301LeaveApplicationDto> ApproveAsync(long leaveApplicationNo, string? remarks, CancellationToken ct = default);
    Task<Hrm1301LeaveApplicationDto> RejectAsync(long leaveApplicationNo, string? remarks, CancellationToken ct = default);
    Task<Hrm1301LeaveApplicationDto> CancelAsync(long leaveApplicationNo, CancellationToken ct = default);
    Task DeleteAsync(long leaveApplicationNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long leaveApplicationNo, bool approved, CancellationToken ct = default);
}

public class Hrm1301Service : IHrm1301Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IApprovalRequestReader _approvals;
    private readonly ILeaveRuleEngine _ruleEngine;
    private readonly IApprovalService _approvalService;

    public const string DocType = "HRM_1301";
    private const short StDraft = 1, StApplied = 2, StApproved = 3, StRejected = 4, StCancelled = 5;

    public Hrm1301Service(IHrmDbContext db, ICompanyBranchContext ctx,
        ILeaveRuleEngine ruleEngine, IApprovalService approvalService, IApprovalRequestReader approvals)
    {
        _db = db;
        _ctx = ctx;
        _ruleEngine = ruleEngine;
        _approvals = approvals;
        _approvalService = approvalService;
    }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<Hrm1301LeaveApplicationDto>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmLeaveApplications.AsNoTracking()
            .Where(x => x.IsDeleted == 0)
            .OrderByDescending(x => x.LeaveApplicationNo)
            .Select(x => ToDto(x))
            .ToListAsync(ct);

    public async Task<List<Hrm1301LeaveApplicationDto>> GetListByEmployeeAsync(long employeeNo, CancellationToken ct = default) =>
        await _db.HrmLeaveApplications.AsNoTracking()
            .Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0)
            .OrderByDescending(x => x.FromDate)
            .Select(x => ToDto(x))
            .ToListAsync(ct);

    public async Task<List<Hrm1301LeaveApplicationDto>> GetFilteredHistoryAsync(
        long employeeNo, long? leaveTypeNo, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default)
    {
        if (employeeNo <= 0) return new();
        var query = _db.HrmLeaveApplications.AsNoTracking()
            .Where(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0);
        if (leaveTypeNo.HasValue) query = query.Where(x => x.LeaveTypeNo == leaveTypeNo.Value);
        if (fromDate.HasValue) query = query.Where(x => x.FromDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.FromDate <= toDate.Value);
        return await query.OrderByDescending(x => x.FromDate).Select(x => ToDto(x)).ToListAsync(ct);
    }

    public async Task<Hrm1301LeaveApplicationDto> GetDetailAsync(long leaveApplicationNo, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(leaveApplicationNo, ct);
        var dto = ToDto(e);
        // Precise edit gate — check if approval is untouched.
        dto.CanEdit = e.Status == StDraft
            || (e.Status == StApplied && await IsApprovalUntouchedAsync(e.ApprovalRequestNo, ct));
        // Resolve reliever name.
        if (e.RelieverEmployeeNo.HasValue)
        {
            var reliever = await _db.HrmEmployees.AsNoTracking()
                .FirstOrDefaultAsync(emp => emp.EmployeeNo == e.RelieverEmployeeNo.Value && emp.IsDeleted == 0, ct);
            if (reliever != null)
                dto.RelieverName = $"{reliever.FirstName}{(string.IsNullOrWhiteSpace(reliever.LastName) ? "" : " " + reliever.LastName)}";
        }
        return dto;
    }

    public async Task<List<Hrm1301LeaveApplicationDto>> GetApprovalListAsync(long? branchNo, CancellationToken ct = default)
    {
        var query = _db.HrmLeaveApplications.AsNoTracking()
            .Where(x => x.IsDeleted == 0 && x.Status == StApplied);
        if (branchNo.HasValue) query = query.Where(x => x.BranchNo == branchNo.Value);

        var leaves = await query.OrderByDescending(x => x.LeaveApplicationNo).ToListAsync(ct);
        if (leaves.Count == 0) return new();

        // Batch-load employee/department/designation/leave-type names.
        var employeeNos = leaves.Select(x => x.EmployeeNo).Distinct().ToList();
        var employees = await _db.HrmEmployees.AsNoTracking()
            .Where(e => employeeNos.Contains(e.EmployeeNo) && e.IsDeleted == 0)
            .ToDictionaryAsync(e => e.EmployeeNo, ct);

        var deptNos = employees.Values.Where(e => e.DepartmentNo > 0).Select(e => e.DepartmentNo).Distinct().ToList();
        var departments = await _db.HrmDepartments.AsNoTracking()
            .Where(d => deptNos.Contains(d.DepartmentNo) && d.IsDeleted == 0)
            .ToDictionaryAsync(d => d.DepartmentNo, d => d.DepartmentName, ct);

        var desigNos = employees.Values.Where(e => e.DesignationNo > 0).Select(e => e.DesignationNo).Distinct().ToList();
        var designations = await _db.HrmDesignations.AsNoTracking()
            .Where(d => desigNos.Contains(d.DesignationNo) && d.IsDeleted == 0)
            .ToDictionaryAsync(d => d.DesignationNo, d => d.DesignationName, ct);

        var typeNos = leaves.Select(x => x.LeaveTypeNo).Distinct().ToList();
        var leaveTypes = await _db.HrmLeaveTypes.AsNoTracking()
            .Where(t => typeNos.Contains(t.LeaveTypeNo) && t.IsDeleted == 0)
            .ToDictionaryAsync(t => t.LeaveTypeNo, t => t.LeaveTypeName, ct);

        return leaves.Select(e =>
        {
            var dto = ToDto(e);
            dto.LeaveTypeName = leaveTypes.GetValueOrDefault(e.LeaveTypeNo);
            if (employees.TryGetValue(e.EmployeeNo, out var emp))
            {
                dto.EmployeeId = emp.EmployeeId;
                dto.EmployeeName = $"{emp.FirstName}{(string.IsNullOrWhiteSpace(emp.LastName) ? "" : " " + emp.LastName)}";
                if (emp.DepartmentNo > 0) dto.DepartmentName = departments.GetValueOrDefault(emp.DepartmentNo);
                if (emp.DesignationNo > 0) dto.DesignationName = designations.GetValueOrDefault(emp.DesignationNo);
            }
            dto.ApprovalState = "PENDING";
            dto.CanAct = true;
            return dto;
        }).ToList();
    }

    public async Task<List<Hrm1301BalanceDto>> GetBalanceAsync(long employeeNo, int? leaveYear, CancellationToken ct = default)
    {
        int year = leaveYear ?? DateTime.UtcNow.Year;
        var balances = await _db.HrmLeaveBalances.AsNoTracking()
            .Where(b => b.EmployeeNo == employeeNo && b.LeaveYear == year && b.IsDeleted == 0)
            .ToListAsync(ct);

        return balances.Select(b => new Hrm1301BalanceDto
        {
            EmployeeNo = b.EmployeeNo,
            LeaveTypeNo = b.LeaveTypeNo,
            LeaveYear = b.LeaveYear,
            EntitledDays = b.EntitledDays,
            AccruedDays = b.AccruedDays,
            ConsumedDays = b.ConsumedDays,
            CarriedForward = b.CarriedForward,
            AvailableDays = b.AvailableDays
        }).ToList();
    }

    // ── Save (Draft/Applied only) ──────────────────────────────────────────

    public async Task<Hrm1301LeaveApplicationDto> SaveAsync(Hrm1301LeaveApplicationDto dto, CancellationToken ct = default)
    {
        return dto.LeaveApplicationNo.HasValue
            ? await UpdateAsync(dto.LeaveApplicationNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    private async Task<Hrm1301LeaveApplicationDto> InsertAsync(Hrm1301LeaveApplicationDto dto, CancellationToken ct)
    {
        var branchNo = ResolveBranch(dto.BranchNo);
        var employeeNo = RequireEmployee(dto.EmployeeNo);
        var leaveTypeNo = RequireLeaveType(dto.LeaveTypeNo);
        var from = RequireDate(dto.FromDate, "From date");
        var to = RequireDate(dto.ToDate, "To date");
        if (to < from) throw new ValidationException("To-date cannot be before from-date");

        // Rule engine validation.
        var applicant = await LoadEmployeeAsync(employeeNo, ct);
        var leaveType = await LoadLeaveTypeAsync(leaveTypeNo, ct);
        var rules = await _ruleEngine.ResolveRulesAsync(leaveTypeNo, applicant, ct);
        var totalDays = await _ruleEngine.ComputeChargeableDaysAsync(from, to, dto.IsHalfDay, rules, branchNo, ct);
        await _ruleEngine.ValidateAsync(applicant, leaveType, rules, from, to, totalDays,
            dto.RelieverEmployeeNo, dto.AttachmentPath, null, ct);
        _ruleEngine.ValidateBalance(leaveType, rules,
            await CurrentAvailableAsync(employeeNo, leaveTypeNo, from.Year, ct), totalDays);

        // Validate reliever.
        if (dto.RelieverEmployeeNo.HasValue)
        {
            await ValidateRelieverAsync(employeeNo, dto.RelieverEmployeeNo.Value, ct);
        }

        var e = new HrmLeaveApplication
        {
            BranchNo = branchNo,
            EmployeeNo = employeeNo,
            LeaveTypeNo = leaveTypeNo,
            FromDate = from,
            ToDate = to,
            IsHalfDay = dto.IsHalfDay,
            TotalDays = totalDays,
            LeaveYear = from.Year,
            Reason = dto.Reason,
            RelieverEmployeeNo = dto.RelieverEmployeeNo,
            AttachmentPath = dto.AttachmentPath,
            Status = StApplied, // saving IS applying
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmLeaveApplications.Add(e);
        await _db.SaveChangesAsync(ct);

        // Route for approval.
        await RouteForApprovalAsync(e, ct);
        return ToDto(await LoadLiveAsync(e.LeaveApplicationNo, ct));
    }

    private async Task<Hrm1301LeaveApplicationDto> UpdateAsync(long no, Hrm1301LeaveApplicationDto dto, CancellationToken ct)
    {
        var e = await LoadLiveAsync(no, ct);
        if (e.Status != StDraft && e.Status != StApplied)
            throw new ValidationException($"This leave is {StatusName(e.Status)} and can no longer be edited");
        if (e.Status == StApplied && !await IsApprovalUntouchedAsync(e.ApprovalRequestNo, ct))
            throw new ValidationException("Approval has already started on this leave — it can no longer be edited. Cancel it and apply again if a change is needed.");

        if (dto.LeaveTypeNo > 0) e.LeaveTypeNo = RequireLeaveType(dto.LeaveTypeNo);
        if (dto.FromDate != default) e.FromDate = dto.FromDate;
        if (dto.ToDate != default) e.ToDate = dto.ToDate;
        if (e.ToDate < e.FromDate) throw new ValidationException("To-date cannot be before from-date");
        if (dto.IsHalfDay != 0) e.IsHalfDay = dto.IsHalfDay;
        if (dto.Reason != null) e.Reason = dto.Reason;
        if (dto.RelieverEmployeeNo.HasValue)
        {
            await ValidateRelieverAsync(e.EmployeeNo, dto.RelieverEmployeeNo.Value, ct);
            e.RelieverEmployeeNo = dto.RelieverEmployeeNo;
        }
        if (dto.AttachmentPath != null) e.AttachmentPath = dto.AttachmentPath;

        // Re-run rule engine against edited values.
        var applicant = await LoadEmployeeAsync(e.EmployeeNo, ct);
        var leaveType = await LoadLeaveTypeAsync(e.LeaveTypeNo, ct);
        var rules = await _ruleEngine.ResolveRulesAsync(e.LeaveTypeNo, applicant, ct);
        var totalDays = await _ruleEngine.ComputeChargeableDaysAsync(e.FromDate, e.ToDate, e.IsHalfDay, rules, e.BranchNo ?? 0, ct);
        await _ruleEngine.ValidateAsync(applicant, leaveType, rules, e.FromDate, e.ToDate, totalDays,
            e.RelieverEmployeeNo, e.AttachmentPath, e.LeaveApplicationNo, ct);
        _ruleEngine.ValidateBalance(leaveType, rules,
            await CurrentAvailableAsync(e.EmployeeNo, e.LeaveTypeNo, e.FromDate.Year, ct), totalDays);

        e.TotalDays = totalDays;
        e.LeaveYear = e.FromDate.Year;
        e.UpdatedBy = _ctx.CurrentUserNo(); e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Legacy Draft rows enter workflow on first edit.
        if (e.Status == StDraft)
        {
            e.Status = StApplied;
            await _db.SaveChangesAsync(ct);
            await RouteForApprovalAsync(e, ct);
        }

        return ToDto(await LoadLiveAsync(no, ct));
    }

    // ── Workflow transitions ───────────────────────────────────────────────

    private async Task RouteForApprovalAsync(HrmLeaveApplication e, CancellationToken ct)
    {
        var outcome = await _approvalService.RaiseAsync(DocType, e.LeaveApplicationNo,
            $"LV-{e.LeaveApplicationNo}", e.TotalDays, ct);

        if (outcome.AutoApproved)
        {
            await ApplyApprovalOutcomeAsync(e.LeaveApplicationNo, true, ct);
        }
        else
        {
            e.ApprovalRequestNo = outcome.ApprovalRequestNo;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<Hrm1301LeaveApplicationDto> SubmitAsync(long leaveApplicationNo, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(leaveApplicationNo, ct);
        if (e.Status == StDraft)
        {
            e.Status = StApplied;
            await _db.SaveChangesAsync(ct);
            await RouteForApprovalAsync(e, ct);
        }
        return ToDto(await LoadLiveAsync(leaveApplicationNo, ct));
    }

    public async Task<Hrm1301LeaveApplicationDto> ApproveAsync(long leaveApplicationNo, string? remarks, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(leaveApplicationNo, ct);
        if (e.Status != StApplied) throw new ValidationException("Only an Applied leave can be approved");
        if (!e.ApprovalRequestNo.HasValue) throw new ValidationException("No approval request — submit the leave first");
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            e.DecisionRemarks = remarks;
            await _db.SaveChangesAsync(ct);
        }
        await _approvalService.ActAsync(e.ApprovalRequestNo.Value, true, remarks, ct);
        return ToDto(await LoadLiveAsync(leaveApplicationNo, ct));
    }

    public async Task<Hrm1301LeaveApplicationDto> RejectAsync(long leaveApplicationNo, string? remarks, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(leaveApplicationNo, ct);
        if (e.Status != StApplied) throw new ValidationException("Only an Applied leave can be rejected");
        if (!e.ApprovalRequestNo.HasValue) throw new ValidationException("No approval request — submit the leave first");
        if (string.IsNullOrWhiteSpace(remarks))
            throw new ValidationException("A reason is required to reject a leave request");
        e.DecisionRemarks = remarks;
        await _db.SaveChangesAsync(ct);
        await _approvalService.ActAsync(e.ApprovalRequestNo.Value, false, remarks, ct);
        return ToDto(await LoadLiveAsync(leaveApplicationNo, ct));
    }

    public async Task ApplyApprovalOutcomeAsync(long leaveApplicationNo, bool approved, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(leaveApplicationNo, ct);
        if (!approved)
        {
            e.Status = StRejected;
            await _db.SaveChangesAsync(ct);
            return;
        }

        var leaveType = await _db.HrmLeaveTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.LeaveTypeNo == e.LeaveTypeNo && t.IsDeleted == 0, ct)
            ?? throw new NotFoundException("Leave type not found");

        var balance = await GetOrCreateBalanceAsync(e.EmployeeNo, e.LeaveTypeNo, e.LeaveYear, e.BranchNo ?? 0, leaveType, ct);

        // Re-validate balance at approval time (may have changed since application).
        var applicant = await _db.HrmEmployees.AsNoTracking()
            .FirstOrDefaultAsync(emp => emp.EmployeeNo == e.EmployeeNo && emp.IsDeleted == 0, ct);
        var rules = await _ruleEngine.ResolveRulesAsync(e.LeaveTypeNo, applicant, ct);
        _ruleEngine.ValidateBalance(leaveType, rules, balance.AvailableDays, e.TotalDays);

        // Consume balance and write ledger.
        balance.ConsumedDays += e.TotalDays;
        await _db.SaveChangesAsync(ct);
        WriteLedger(e, "Consume", -e.TotalDays, balance.AvailableDays);

        e.Status = StApproved;
        e.ApprovedBy = _ctx.CurrentUserNo();
        e.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Hrm1301LeaveApplicationDto> CancelAsync(long leaveApplicationNo, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(leaveApplicationNo, ct);
        if (e.Status is StRejected or StCancelled)
            throw new ValidationException($"This leave is already {(e.Status == StRejected ? "rejected" : "cancelled")}");

        if (e.Status == StApproved)
        {
            // Reverse the consumption with a compensating ledger row.
            var balance = await _db.HrmLeaveBalances
                .FirstOrDefaultAsync(b => b.EmployeeNo == e.EmployeeNo
                    && b.LeaveTypeNo == e.LeaveTypeNo && b.LeaveYear == e.LeaveYear && b.IsDeleted == 0, ct);
            if (balance != null)
            {
                var next = balance.ConsumedDays - e.TotalDays;
                balance.ConsumedDays = next < 0 ? 0 : next;
                await _db.SaveChangesAsync(ct);
                WriteLedger(e, "Reverse", e.TotalDays, balance.AvailableDays);
            }
        }

        // Withdraw the approval request.
        if (e.ApprovalRequestNo.HasValue)
            await _approvalService.CancelRequestAsync(e.ApprovalRequestNo.Value, ct);

        e.Status = StCancelled;
        e.UpdatedBy = _ctx.CurrentUserNo(); e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    public async Task DeleteAsync(long leaveApplicationNo, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(leaveApplicationNo, ct);
        bool ownedByApplicant = e.Status == StDraft
            || (e.Status == StApplied && await IsApprovalUntouchedAsync(e.ApprovalRequestNo, ct));
        if (!ownedByApplicant)
            throw new ValidationException("Approval has already started on this leave — cancel it instead of deleting");

        if (e.ApprovalRequestNo.HasValue)
            await _approvalService.CancelRequestAsync(e.ApprovalRequestNo.Value, ct);

        e.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── Balance / ledger helpers ───────────────────────────────────────────

    private async Task<HrmLeaveBalance> GetOrCreateBalanceAsync(
        long employeeNo, long leaveTypeNo, int year, long branchNo, HrmLeaveType type, CancellationToken ct)
    {
        var existing = await _db.HrmLeaveBalances
            .FirstOrDefaultAsync(b => b.EmployeeNo == employeeNo && b.LeaveTypeNo == leaveTypeNo
                && b.LeaveYear == year && b.IsDeleted == 0, ct);
        if (existing != null) return existing;

        // Create new balance with entitled days from policy or type default.
        var entitled = await ResolveEntitledDaysAsync(employeeNo, type, ct);
        var balance = new HrmLeaveBalance
        {
            EmployeeNo = employeeNo,
            LeaveTypeNo = leaveTypeNo,
            LeaveYear = year,
            EntitledDays = entitled,
            AccruedDays = entitled, // annual-upfront seeding
            ConsumedDays = 0,
            CarriedForward = 0,
            BranchNo = branchNo,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmLeaveBalances.Add(balance);
        await _db.SaveChangesAsync(ct);

        // Write opening ledger entry.
        if (entitled > 0)
        {
            WriteLedgerCore(employeeNo, leaveTypeNo, year, "Opening", entitled, balance.AvailableDays, 0, branchNo);
            await _db.SaveChangesAsync(ct);
        }

        return balance;
    }

    private void WriteLedger(HrmLeaveApplication ctx, string movementType, decimal days, decimal balanceAfter)
    {
        WriteLedgerCore(ctx.EmployeeNo, ctx.LeaveTypeNo, ctx.LeaveYear, movementType, days, balanceAfter,
            ctx.LeaveApplicationNo, ctx.BranchNo ?? 0);
    }

    private void WriteLedgerCore(long employeeNo, long leaveTypeNo, int leaveYear,
        string movementType, decimal days, decimal balanceAfter, long refDocNo, long branchNo)
    {
        var ledger = new HrmLeaveLedger
        {
            EmployeeNo = employeeNo,
            LeaveTypeNo = leaveTypeNo,
            TransType = movementType,
            Days = days,
            TransDate = DateTime.UtcNow,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmLeaveLedgers.Add(ledger);
    }

    private async Task<decimal> ResolveEntitledDaysAsync(long employeeNo, HrmLeaveType type, CancellationToken ct)
    {
        var emp = await _db.HrmEmployees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeNo == employeeNo && e.IsDeleted == 0, ct);
        if (emp?.DesignationNo != null)
        {
            var policy = await _db.HrmLeavePolicySetups.AsNoTracking()
                .FirstOrDefaultAsync(p => p.LeaveTypeNo == type.LeaveTypeNo
                    && p.EmployeeGroupNo == emp.DesignationNo && p.IsDeleted == 0, ct);
            if (policy?.DefaultDays > 0) return policy.DefaultDays.Value;
        }
        return type.DefaultDaysPerYear ?? 0;
    }

    private async Task<decimal> CurrentAvailableAsync(long employeeNo, long leaveTypeNo, int year, CancellationToken ct)
    {
        var balance = await _db.HrmLeaveBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.EmployeeNo == employeeNo && b.LeaveTypeNo == leaveTypeNo
                && b.LeaveYear == year && b.IsDeleted == 0, ct);
        if (balance != null) return balance.AvailableDays;
        // No balance row yet — use entitled days as if it would be seeded.
        var type = await _db.HrmLeaveTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.LeaveTypeNo == leaveTypeNo && t.IsDeleted == 0, ct);
        return type != null ? await ResolveEntitledDaysAsync(employeeNo, type, ct) : 0;
    }

    // ── Validation helpers ─────────────────────────────────────────────────

    private async Task<HrmLeaveApplication> LoadLiveAsync(long no, CancellationToken ct) =>
        await _db.HrmLeaveApplications.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LeaveApplicationNo == no && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Leave application not found: no={no}");

    private async Task<HrmEmployee> LoadEmployeeAsync(long employeeNo, CancellationToken ct) =>
        await _db.HrmEmployees.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Employee not found: employeeNo={employeeNo}");

    private async Task<HrmLeaveType> LoadLeaveTypeAsync(long leaveTypeNo, CancellationToken ct) =>
        await _db.HrmLeaveTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LeaveTypeNo == leaveTypeNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Leave type not found: leaveTypeNo={leaveTypeNo}");

    private async Task<bool> IsApprovalUntouchedAsync(long? approvalRequestNo, CancellationToken ct)
    {
        if (!approvalRequestNo.HasValue) return true;
        var status = await _approvals.GetStatusAsync(approvalRequestNo.Value, ct);
        return status == null || status == 1; // Pending = untouched
    }

    private long ResolveBranch(long? branchNoFromDto)
    {
        var branchNo = branchNoFromDto ?? _ctx.BranchNo;
        if (branchNo == null) throw new ValidationException("Branch is required");
        return branchNo.Value;
    }

    private static long RequireEmployee(long employeeNo) =>
        employeeNo > 0 ? employeeNo : throw new ValidationException("Employee is required");

    private static long RequireLeaveType(long leaveTypeNo) =>
        leaveTypeNo > 0 ? leaveTypeNo : throw new ValidationException("Leave type is required");

    private static DateTime RequireDate(DateTime date, string field) =>
        date == default ? throw new ValidationException($"{field} is required") : date;

    private static string StatusName(short status) => status switch
    {
        StDraft => "a draft",
        StApplied => "pending approval",
        StApproved => "approved",
        StRejected => "rejected",
        StCancelled => "cancelled",
        _ => $"in state {status}"
    };

    private async Task ValidateRelieverAsync(long applicantNo, long relieverNo, CancellationToken ct)
    {
        if (applicantNo == relieverNo)
            throw new ValidationException("Reliever cannot be the applicant");
        var applicant = await _db.HrmEmployees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeNo == applicantNo && e.IsDeleted == 0, ct)
            ?? throw new NotFoundException("Applicant employee not found");
        var reliever = await _db.HrmEmployees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeNo == relieverNo && e.IsDeleted == 0, ct)
            ?? throw new NotFoundException("Reliever employee not found");
        if (applicant.DepartmentNo != reliever.DepartmentNo)
            throw new ValidationException("Reliever must be in the same department as the applicant");
    }

    // ── DTO mapping ────────────────────────────────────────────────────────

    private static Hrm1301LeaveApplicationDto ToDto(HrmLeaveApplication e) => new()
    {
        LeaveApplicationNo = e.LeaveApplicationNo,
        EmployeeNo = e.EmployeeNo,
        LeaveTypeNo = e.LeaveTypeNo,
        FromDate = e.FromDate,
        ToDate = e.ToDate,
        TotalDays = e.TotalDays,
        IsHalfDay = e.IsHalfDay,
        LeaveYear = e.LeaveYear,
        Reason = e.Reason,
        Status = e.Status,
        ApprovedBy = e.ApprovedBy,
        ApprovedAt = e.ApprovedAt,
        BranchNo = e.BranchNo,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion,
        RelieverEmployeeNo = e.RelieverEmployeeNo,
        AttachmentPath = e.AttachmentPath,
        DecisionRemarks = e.DecisionRemarks,
        CanEdit = e.Status == StDraft || e.Status == StApplied
    };
}
