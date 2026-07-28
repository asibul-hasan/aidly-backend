using AidlyErp.Shared.Core;
using System.Text.Json;
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
/// HRM_1207 Final Settlement — full port of Java Hrm1207Service (589 lines).
/// The offboarding payout. Draft → Approved → Paid → (Cancelled).
/// Auto-loads outstanding payroll and bonus liabilities via preview system.
/// Emits FinalSettlementPosted to sys_event_outbox for FIN GL posting.
/// </summary>
public interface IHrm1207Service
{
    Task<List<Hrm1207SettlementDto>> GetListAsync(CancellationToken ct = default);
    Task<Hrm1207SettlementDto> GetDetailAsync(long no, CancellationToken ct = default);
    Task<Hrm1207SettlementDto> PreviewAsync(long employeeNo, long? branchNo, DateTime lastWorkingDay, CancellationToken ct = default);
    Task<Hrm1207SettlementDto> SaveAsync(Hrm1207SettlementDto dto, CancellationToken ct = default);
    Task<Hrm1207SettlementDto> SubmitAsync(long no, CancellationToken ct = default);
    Task<Hrm1207SettlementDto> ApproveAsync(long no, CancellationToken ct = default);
    Task<Hrm1207SettlementDto> RejectAsync(long no, CancellationToken ct = default);
    Task<Hrm1207SettlementDto> MarkPaidAsync(long no, CancellationToken ct = default);
    Task<Hrm1207SettlementDto> CancelAsync(long no, CancellationToken ct = default);
    Task DeleteAsync(long no, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long settlementNo, bool approved, CancellationToken ct = default);
}

public class Hrm1207Service : IHrm1207Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IApprovalService _approvalService;

    public const string DocType = "HRM_SETTLEMENT";
    private const string OutboxAggregate = "HRM_SETTLEMENT";
    private const string OutboxEvent = "FinalSettlementPosted";
    private const short StDraft = 1, StApproved = 2, StPaid = 3, StCancelled = 4;
    private const short PayrollStApproved = 3, PayrollStPaid = 4;
    private const short RunRegular = 1, RunSupplementary = 2, RunBonus = 3;
    private const short PayslipPaid = 2;

    public Hrm1207Service(IHrmDbContext db, ICompanyBranchContext ctx, IApprovalService approvalService)
    {
        _db = db;
        _ctx = ctx;
        _approvalService = approvalService;
    }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<Hrm1207SettlementDto>> GetListAsync(CancellationToken ct = default)
    {
        var settlements = await _db.HrmFinalSettlements.AsNoTracking()
            .Where(s => s.IsDeleted == 0)
            .OrderByDescending(s => s.SettlementNo)
            .ToListAsync(ct);

        return settlements.Select(s => MapToDto(s)).ToList();
    }

    public async Task<Hrm1207SettlementDto> GetDetailAsync(long no, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(no, ct);
        return await MapToDtoWithPreviewAsync(e, ct);
    }

    public async Task<Hrm1207SettlementDto> PreviewAsync(long employeeNo, long? branchNo, DateTime lastWorkingDay, CancellationToken ct)
    {
        var employee = await RequireEmployeeAsync(employeeNo, ct);
        var resolvedBranchNo = await ResolveSettlementBranchAsync(branchNo, employee, ct);
        var preview = await BuildPreviewAsync(employee, resolvedBranchNo, lastWorkingDay, ct);

        return new Hrm1207SettlementDto
        {
            EmployeeNo = employee.EmployeeNo,
            EmployeeId = employee.EmployeeId,
            EmployeeName = BuildEmployeeName(employee),
            BranchNo = resolvedBranchNo,
            LastWorkingDay = lastWorkingDay,
            LastSalary = preview.UnpaidSalaryAmount,
            BonusPayable = preview.UnpaidBonusAmount,
            LoanRecovery = 0,
            NetPayable = preview.PendingPaymentAmount,
            CurrentPayPeriod = preview.CurrentPayPeriod,
            CurrentPayrollStatus = preview.CurrentPayrollStatus,
            UnpaidPayPeriods = preview.UnpaidPayPeriods,
            UnpaidSalaryAmount = preview.UnpaidSalaryAmount,
            UnpaidBonusAmount = preview.UnpaidBonusAmount,
            PendingPaymentAmount = preview.PendingPaymentAmount,
            PendingItemCount = preview.PendingItemCount
        };
    }

    // ── Save ───────────────────────────────────────────────────────────────

    public async Task<Hrm1207SettlementDto> SaveAsync(Hrm1207SettlementDto dto, CancellationToken ct = default)
    {
        return dto.SettlementNo.HasValue
            ? await UpdateAsync(dto.SettlementNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    private async Task<Hrm1207SettlementDto> InsertAsync(Hrm1207SettlementDto dto, CancellationToken ct)
    {
        if (!dto.LastWorkingDay.HasValue) throw new ValidationException("Last working day is required");

        var employee = await RequireEmployeeAsync(dto.EmployeeNo, ct);
        var branchNo = await ResolveSettlementBranchAsync(dto.BranchNo, employee, ct);
        var preview = await BuildPreviewAsync(employee, branchNo, dto.LastWorkingDay.Value, ct);

        var e = new HrmFinalSettlement
        {
            BranchNo = branchNo,
            SettlementId = await NextSettlementIdAsync(branchNo, dto.LastWorkingDay.Value, ct),
            EmployeeNo = employee.EmployeeNo,
            SeparationType = dto.SeparationType,
            LastWorkingDay = DateOnly.FromDateTime(dto.LastWorkingDay.Value),
            Remarks = dto.Remarks,
            Status = StDraft,
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };

        ApplyManualAmounts(e, dto);
        ApplyPreviewAmounts(e, preview);
        e.NetPayable = ComputeNet(e);

        _db.HrmFinalSettlements.Add(e);
        await _db.SaveChangesAsync(ct);

        return await MapToDtoWithPreviewAsync(e, ct);
    }

    private async Task<Hrm1207SettlementDto> UpdateAsync(long no, Hrm1207SettlementDto dto, CancellationToken ct)
    {
        var e = await LoadLiveAsync(no, ct);
        if (e.Status != StDraft) throw new ValidationException("Only a Draft settlement can be edited");

        var employee = await RequireEmployeeAsync(dto.EmployeeNo > 0 ? dto.EmployeeNo : e.EmployeeNo, ct);
        var branchNo = await ResolveSettlementBranchAsync(dto.BranchNo ?? e.BranchNo, employee, ct);

        e.EmployeeNo = employee.EmployeeNo;
        e.BranchNo = branchNo;
        if (dto.SeparationType.HasValue) e.SeparationType = dto.SeparationType;
        if (dto.LastWorkingDay.HasValue) e.LastWorkingDay = DateOnly.FromDateTime(dto.LastWorkingDay.Value);

        ApplyManualAmounts(e, dto);
        var preview = await BuildPreviewAsync(employee, branchNo, e.LastWorkingDay?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow, ct);
        ApplyPreviewAmounts(e, preview);

        if (dto.Remarks != null) e.Remarks = dto.Remarks;
        if (dto.IsActive != 0) e.IsActive = dto.IsActive;
        e.NetPayable = ComputeNet(e);
        e.UpdatedBy = _ctx.CurrentUserNo(); e.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await MapToDtoWithPreviewAsync(e, ct);
    }

    // ── Workflow ───────────────────────────────────────────────────────────

    public async Task<Hrm1207SettlementDto> SubmitAsync(long no, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(no, ct);
        if (e.Status != StDraft) throw new ValidationException("Only a Draft settlement can be submitted");

        var outcome = await _approvalService.RaiseAsync(DocType, e.SettlementNo, e.SettlementId ?? "", e.NetPayable, ct);
        if (outcome.AutoApproved)
        {
            await ApplyApprovalOutcomeAsync(e.SettlementNo, true, ct);
        }
        else
        {
            e.ApprovalRequestNo = outcome.ApprovalRequestNo;
            await _db.SaveChangesAsync(ct);
        }

        return await MapToDtoWithPreviewAsync(await LoadLiveAsync(no, ct), ct);
    }

    public async Task<Hrm1207SettlementDto> ApproveAsync(long no, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(no, ct);
        if (e.Status != StDraft) throw new ValidationException("Only a submitted (Draft) settlement can be approved");
        if (!e.ApprovalRequestNo.HasValue) throw new ValidationException("No approval request — submit first");
        await _approvalService.ActAsync(e.ApprovalRequestNo.Value, true, null, ct);
        return await MapToDtoWithPreviewAsync(await LoadLiveAsync(no, ct), ct);
    }

    public async Task<Hrm1207SettlementDto> RejectAsync(long no, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(no, ct);
        if (!e.ApprovalRequestNo.HasValue) throw new ValidationException("No approval request — submit first");
        await _approvalService.ActAsync(e.ApprovalRequestNo.Value, false, null, ct);
        return await MapToDtoWithPreviewAsync(await LoadLiveAsync(no, ct), ct);
    }

    public async Task ApplyApprovalOutcomeAsync(long settlementNo, bool approved, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(settlementNo, ct);
        if (approved)
        {
            e.Status = StApproved;
            e.ApprovedBy = _ctx.CurrentUserNo();
            e.ApprovedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await EmitSettlementPostedAsync(e, ct);
        }
        else
        {
            e.ApprovalRequestNo = null;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<Hrm1207SettlementDto> MarkPaidAsync(long no, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(no, ct);
        if (e.Status != StApproved) throw new ValidationException("Only an Approved settlement can be marked Paid");

        e.Status = StPaid;
        e.PaidAt = DateTime.UtcNow;
        e.PaidBy = _ctx.CurrentUserNo();
        await _db.SaveChangesAsync(ct);

        // Deactivate employee.
        var emp = await _db.HrmEmployees.FirstOrDefaultAsync(emp => emp.EmployeeNo == e.EmployeeNo && emp.IsDeleted == 0, ct);
        if (emp != null)
        {
            emp.IsActive = 0;
            await _db.SaveChangesAsync(ct);
        }

        return await MapToDtoWithPreviewAsync(e, ct);
    }

    public async Task<Hrm1207SettlementDto> CancelAsync(long no, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(no, ct);
        if (e.Status is StPaid or StCancelled)
            throw new ValidationException("A paid/cancelled settlement cannot be cancelled");
        e.Status = StCancelled;
        e.UpdatedBy = _ctx.CurrentUserNo(); e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await MapToDtoWithPreviewAsync(e, ct);
    }

    public async Task DeleteAsync(long no, CancellationToken ct = default)
    {
        var e = await LoadLiveAsync(no, ct);
        if (e.Status != StDraft) throw new ValidationException("Only a Draft settlement can be deleted");
        e.PerformSoftDelete(_ctx.CurrentUserNo());
        await _db.SaveChangesAsync(ct);
    }

    // ── Preview ────────────────────────────────────────────────────────────

    private async Task<SettlementPreview> BuildPreviewAsync(HrmEmployee employee, long branchNo, DateTime lastWorkingDay, CancellationToken ct)
    {
        var currentPayPeriod = $"{lastWorkingDay:yyyy-MM}";
        var allRows = await _db.HrmPayslips
            .Where(p => p.EmployeeNo == employee.EmployeeNo && p.BranchNo == branchNo && p.IsDeleted == 0)
            .Join(_db.HrmPayrollRuns, p => p.PayrollRunNo, r => r.PayrollRunNo, (p, r) => new { Payslip = p, Run = r })
            .Where(x => x.Run.IsDeleted == 0 && x.Run.PeriodEnd <= lastWorkingDay)
            .Select(x => new SettlementDueRow
            {
                PayslipNo = x.Payslip.PayslipNo,
                PayrollRunNo = x.Payslip.PayrollRunNo,
                PayPeriod = x.Run.PayPeriod,
                RunType = x.Run.RunType,
                RunStatus = x.Run.Status,
                PeriodEnd = x.Run.PeriodEnd,
                NetPay = x.Payslip.NetPay,
                PaymentStatus = x.Payslip.PaymentStatus
            })
            .ToListAsync(ct);

        var dueRows = allRows.Where(IsDueForSettlement).ToList();
        var unpaidSalaryAmount = dueRows.Where(IsSalaryRun).Sum(r => r.NetPay);
        var unpaidBonusAmount = dueRows.Where(IsBonusRun).Sum(r => r.NetPay);
        var unpaidPayPeriods = dueRows.Where(IsSalaryRun).Select(r => r.PayPeriod).Where(p => p != null).Distinct().ToList();

        var currentPeriodRows = allRows.Where(r => IsSalaryRun(r) && r.PayPeriod == currentPayPeriod).ToList();

        return new SettlementPreview
        {
            EmployeeId = employee.EmployeeId,
            EmployeeName = BuildEmployeeName(employee),
            CurrentPayPeriod = currentPayPeriod,
            CurrentPayrollStatus = DeriveCurrentPayrollStatus(currentPeriodRows),
            UnpaidPayPeriods = unpaidPayPeriods,
            UnpaidSalaryAmount = unpaidSalaryAmount,
            UnpaidBonusAmount = unpaidBonusAmount,
            PendingPaymentAmount = unpaidSalaryAmount + unpaidBonusAmount,
            PendingItemCount = dueRows.Count
        };
    }

    private static bool IsDueForSettlement(SettlementDueRow row) =>
        (row.PaymentStatus == null || row.PaymentStatus != PayslipPaid)
        && (row.RunStatus == PayrollStApproved || row.RunStatus == PayrollStPaid);

    private static bool IsSalaryRun(SettlementDueRow row) =>
        row.RunType == RunRegular || row.RunType == RunSupplementary;

    private static bool IsBonusRun(SettlementDueRow row) =>
        row.RunType == RunBonus;

    private static string DeriveCurrentPayrollStatus(List<SettlementDueRow> rows)
    {
        if (rows.Count == 0) return "Not processed";
        bool hasUnpaid = rows.Any(r => r.PaymentStatus == null || r.PaymentStatus != PayslipPaid);
        if (hasUnpaid && rows.Any(r => r.RunStatus == PayrollStApproved)) return "Approved / pending payment";
        if (hasUnpaid && rows.Any(r => r.RunStatus == PayrollStPaid)) return "Pending payment";
        if (rows.Any(r => r.RunStatus == 2)) return "Calculated";
        if (rows.Any(r => r.RunStatus == 1)) return "Draft";
        return "Paid";
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<HrmFinalSettlement> LoadLiveAsync(long no, CancellationToken ct) =>
        await _db.HrmFinalSettlements.FirstOrDefaultAsync(s => s.SettlementNo == no && s.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Settlement not found: no={no}");

    private async Task<HrmEmployee> RequireEmployeeAsync(long employeeNo, CancellationToken ct) =>
        await _db.HrmEmployees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeNo == employeeNo && e.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Employee not found: employeeNo={employeeNo}");

    private async Task<long> ResolveSettlementBranchAsync(long? branchNoFromDto, HrmEmployee employee, CancellationToken ct)
    {
        long? branchNo = branchNoFromDto ?? employee.BranchNo ?? _ctx.BranchNo;
        if (!branchNo.HasValue) throw new ValidationException("Branch is required");
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchNo == branchNo.Value && b.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Branch not found: branchNo={branchNo}");
        if (employee.BranchNo.HasValue && employee.BranchNo.Value != branchNo.Value)
            throw new ValidationException("Employee belongs to another branch");
        return branch.BranchNo;
    }

    private static void ApplyManualAmounts(HrmFinalSettlement e, Hrm1207SettlementDto d)
    {
        if (d.LeaveEncashment.HasValue) e.LeaveEncashment = NonNeg(d.LeaveEncashment.Value, "Leave encashment");
        if (d.Gratuity.HasValue) e.Gratuity = NonNeg(d.Gratuity.Value, "Gratuity");
        if (d.OtherDeduction.HasValue) e.OtherDeduction = NonNeg(d.OtherDeduction.Value, "Other deduction");
    }

    private static void ApplyPreviewAmounts(HrmFinalSettlement e, SettlementPreview preview)
    {
        e.LastSalary = preview.UnpaidSalaryAmount;
        e.BonusPayable = preview.UnpaidBonusAmount;
        e.LoanRecovery = 0;
    }

    private static decimal ComputeNet(HrmFinalSettlement e) =>
        (e.LastSalary ?? 0) + (e.LeaveEncashment ?? 0) + (e.Gratuity ?? 0) + (e.BonusPayable ?? 0)
        - (e.LoanRecovery ?? 0) - (e.OtherDeduction ?? 0);

    private async Task<string> NextSettlementIdAsync(long branchNo, DateTime lastWorkingDay, CancellationToken ct)
    {
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchNo == branchNo && b.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Branch not found: branchNo={branchNo}");

        string stamp = lastWorkingDay.ToString("MMyy");
        string docType = $"{DocType}_{stamp}";
        string prefix = $"FS{stamp}";

        var seq = await _db.DocSequences.FirstOrDefaultAsync(
            s => s.CompanyNo == branch.CompanyNo && s.BranchNo == branchNo && s.DocType == docType, ct);

        if (seq == null)
        {
            seq = new DocSequence
            {
                CompanyNo = branch.CompanyNo ?? 0,
                BranchNo = branchNo,
                DocType = docType,
                Prefix = prefix,
                NextVal = 2,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(),
                CreatedAt = DateTime.UtcNow
            };
            _db.DocSequences.Add(seq);
            await _db.SaveChangesAsync(ct);
            return $"{prefix}0001";
        }

        long current = seq.NextVal;
        seq.NextVal = current + 1;
        await _db.SaveChangesAsync(ct);
        return $"{seq.Prefix ?? prefix}{current:D4}";
    }

    private static decimal NonNeg(decimal value, string field) =>
        value < 0 ? throw new ValidationException($"{field} cannot be negative") : value;

    private static string BuildEmployeeName(HrmEmployee e) =>
        string.Join(" ", new[] { e.FirstName, e.MiddleName, e.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    // ── GL outbox ──────────────────────────────────────────────────────────

    private async Task EmitSettlementPostedAsync(HrmFinalSettlement settlement, CancellationToken ct)
    {
        try
        {
            decimal expense = (settlement.LastSalary ?? 0) + (settlement.LeaveEncashment ?? 0)
                            + (settlement.Gratuity ?? 0) + (settlement.BonusPayable ?? 0);
            decimal deductions = settlement.OtherDeduction ?? 0;
            decimal net = settlement.NetPayable;
            if (expense <= 0) return;

            var legs = new List<object>
            {
                new { legKey = "EXPENSE", amount = expense, drCr = "dr", partyType = (short)3, partyNo = settlement.EmployeeNo }
            };
            if (net > 0) legs.Add(new { legKey = "PAYABLE", amount = net, drCr = "cr", partyType = (short)3, partyNo = settlement.EmployeeNo });
            if (deductions > 0) legs.Add(new { legKey = "DEDUCTION", amount = deductions, drCr = "cr", partyType = (short)3, partyNo = settlement.EmployeeNo });

            var payload = new
            {
                voucherDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                narration = $"Final settlement {settlement.SettlementId}",
                branchNo = settlement.BranchNo,
                legs
            };

            long companyNo = _ctx.CurrentCompanyNo() ?? 0;
            if (companyNo == 0)
            {
                var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.BranchNo == settlement.BranchNo && b.IsDeleted == 0, ct);
                companyNo = branch?.CompanyNo ?? 0;
            }
            if (companyNo == 0) return;

            var ev = new EventOutbox
            {
                CompanyNo = companyNo,
                BranchNo = settlement.BranchNo,
                AggregateType = OutboxAggregate,
                AggregateId = settlement.SettlementNo.ToString(),
                EventType = OutboxEvent,
                Payload = JsonSerializer.Serialize(payload),
                Status = 1,
                IsProcessed = 0,
                CreatedAt = DateTime.UtcNow
            };
            _db.EventOutboxes.Add(ev);
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Never block settlement approval on the GL hand-off.
        }
    }

    // ── DTO mapping ────────────────────────────────────────────────────────

    private async Task<Hrm1207SettlementDto> MapToDtoWithPreviewAsync(HrmFinalSettlement e, CancellationToken ct)
    {
        var dto = MapToDto(e);

        // Build preview for enrichment.
        var employee = await _db.HrmEmployees.AsNoTracking().FirstOrDefaultAsync(emp => emp.EmployeeNo == e.EmployeeNo && emp.IsDeleted == 0, ct);
        if (employee != null)
        {
            var lastWorkingDay = e.LastWorkingDay?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow;
            var preview = await BuildPreviewAsync(employee, e.BranchNo ?? 0, lastWorkingDay, ct);
            dto.EmployeeId = employee.EmployeeId;
            dto.EmployeeName = BuildEmployeeName(employee);
            dto.CurrentPayPeriod = preview.CurrentPayPeriod;
            dto.CurrentPayrollStatus = preview.CurrentPayrollStatus;
            dto.UnpaidPayPeriods = preview.UnpaidPayPeriods;
            dto.UnpaidSalaryAmount = preview.UnpaidSalaryAmount;
            dto.UnpaidBonusAmount = preview.UnpaidBonusAmount;
            dto.PendingPaymentAmount = preview.PendingPaymentAmount;
            dto.PendingItemCount = preview.PendingItemCount;
        }

        return dto;
    }

    private static Hrm1207SettlementDto MapToDto(HrmFinalSettlement e) => new()
    {
        SettlementNo = e.SettlementNo,
        SettlementId = e.SettlementId,
        EmployeeNo = e.EmployeeNo,
        SeparationType = e.SeparationType,
        LastWorkingDay = e.LastWorkingDay?.ToDateTime(TimeOnly.MinValue),
        LastSalary = e.LastSalary,
        LeaveEncashment = e.LeaveEncashment,
        Gratuity = e.Gratuity,
        BonusPayable = e.BonusPayable,
        LoanRecovery = e.LoanRecovery,
        OtherDeduction = e.OtherDeduction,
        NetPayable = e.NetPayable,
        Remarks = e.Remarks,
        Status = e.Status,
        ApprovedBy = e.ApprovedBy,
        ApprovedAt = e.ApprovedAt,
        BranchNo = e.BranchNo,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };

    // ── Records ────────────────────────────────────────────────────────────

    private class SettlementPreview
    {
        public string? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string CurrentPayPeriod { get; set; } = "";
        public string CurrentPayrollStatus { get; set; } = "Not processed";
        public List<string> UnpaidPayPeriods { get; set; } = new();
        public decimal UnpaidSalaryAmount { get; set; }
        public decimal UnpaidBonusAmount { get; set; }
        public decimal PendingPaymentAmount { get; set; }
        public int PendingItemCount { get; set; }
    }

    private class SettlementDueRow
    {
        public long PayslipNo { get; set; }
        public long PayrollRunNo { get; set; }
        public string? PayPeriod { get; set; }
        public short RunType { get; set; }
        public short RunStatus { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal NetPay { get; set; }
        public short PaymentStatus { get; set; }
    }
}
