using AidlyErp.Shared.Core;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AidlyErp.Hrm.Domain;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Hrm.Application.Interfaces;

namespace AidlyErp.Hrm.Application.Services;

public interface IPayrollCalculationService
{
    Task<HrmPayrollRun> CreateRunAsync(long companyNo, long branchNo, string periodName, DateTime startDate, DateTime endDate, short runType, string? remarks, CancellationToken cancellationToken = default);
    Task<int> CalculateRunAsync(long payrollRunNo, CancellationToken cancellationToken = default);
    Task ApproveRunAsync(long payrollRunNo, long approvedBy, CancellationToken cancellationToken = default);
    Task PostRunAsync(long payrollRunNo, long postedBy, CancellationToken cancellationToken = default);
}

public class PayrollCalculationService : IPayrollCalculationService
{
    private readonly IHrmDbContext _db;
    private readonly ILogger<PayrollCalculationService> _logger;
    private const short Deleted = 0;
    private const short Active = 1;
    private const short Draft = 1;
    private const short Calculated = 2;
    private const short Approved = 3;

    /// <summary>Approved state on <c>hrm_overtime.status</c> (its own workflow, distinct from the payroll-run states above).</summary>
    private const short OvertimeApproved = 2;
    private const short Paid = 4;

    public PayrollCalculationService(IHrmDbContext db, ILogger<PayrollCalculationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HrmPayrollRun> CreateRunAsync(long companyNo, long branchNo, string periodName, DateTime startDate, DateTime endDate, short runType, string? remarks, CancellationToken cancellationToken = default)
    {
        bool exists = await _db.HrmPayrollRuns.AnyAsync(r =>
            r.CompanyNo == companyNo && r.BranchNo == branchNo &&
            r.PeriodName == periodName && r.RunType == runType &&
            r.IsDeleted == Deleted, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"A payroll run for period '{periodName}' (Type: {runType}) already exists for this branch.");
        }

        var run = new HrmPayrollRun
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            PeriodName = periodName,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            RunType = runType,
            Status = Draft,
            Remarks = remarks,
            RunDate = DateTime.UtcNow,
            IsDeleted = Deleted
        };

        _db.HrmPayrollRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task<int> CalculateRunAsync(long payrollRunNo, CancellationToken cancellationToken = default)
    {
        var run = await _db.HrmPayrollRuns.FirstOrDefaultAsync(r => r.PayrollRunNo == payrollRunNo && r.IsDeleted == Deleted, cancellationToken);
        if (run == null) throw new InvalidOperationException($"Payroll run not found: {payrollRunNo}");
        if (run.Status != Draft && run.Status != Calculated)
        {
            throw new InvalidOperationException($"Payroll run {payrollRunNo} cannot be calculated in status {run.Status}. Must be Draft or Calculated.");
        }

        // Remove existing draft payslips if re-calculating
        var existingPayslips = await _db.HrmPayslips.Where(p => p.PayrollRunNo == payrollRunNo && p.IsDeleted == Deleted).ToListAsync(cancellationToken);
        foreach (var p in existingPayslips)
        {
            var lines = await _db.HrmPayslipDtls.Where(l => l.PayslipNo == p.PayslipNo).ToListAsync(cancellationToken);
            _db.HrmPayslipDtls.RemoveRange(lines);
            _db.HrmPayslips.Remove(p);
        }

        var employees = await _db.HrmEmployees
            .AsNoTracking()
            .Where(e => e.CompanyNo == run.CompanyNo &&
                        (e.BranchNo == run.BranchNo || e.BranchNo == null) &&
                        e.IsDeleted == Deleted &&
                        e.JoiningDate <= run.EndDate &&
                        (e.ContractEndDate == null || e.ContractEndDate >= run.StartDate))
            .ToListAsync(cancellationToken);

        int count = 0;
        decimal totalEarningsRun = 0m;
        decimal totalDeductionsRun = 0m;
        decimal totalNetRun = 0m;

        foreach (var emp in employees)
        {
            var structure = await _db.HrmSalaryStructures
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.EmployeeNo == emp.EmployeeNo && s.IsActive == Active && s.IsDeleted == Deleted, cancellationToken);

            if (structure == null)
            {
                _logger.LogWarning("Employee {EmployeeNo} ({Name}) skipped: No active salary structure.", emp.EmployeeNo, emp.FirstName);
                continue;
            }

            var structureLines = await _db.HrmSalaryStructureDtls
                .AsNoTracking()
                .Where(l => l.StructureNo == structure.StructureNo && l.IsDeleted == Deleted)
                .ToListAsync(cancellationToken);

            var components = await _db.HrmSalaryComponents
                .AsNoTracking()
                .Where(c => c.IsDeleted == Deleted)
                .ToDictionaryAsync(c => c.ComponentNo, cancellationToken);

            decimal totalDaysInPeriod = (decimal)(run.EndDate - run.StartDate).TotalDays + 1m;
            if (totalDaysInPeriod <= 0) totalDaysInPeriod = 30m;

            // Attendance proration check (Leave Without Pay / Unexcused Absences)
            long absentDays = await _db.HrmAttendances
                .AsNoTracking()
                .CountAsync(a => a.EmployeeNo == emp.EmployeeNo && a.AttendanceDate >= run.StartDate && a.AttendanceDate <= run.EndDate && (a.Status == 2 || a.Status == 8) && a.IsDeleted == Deleted, cancellationToken);

            decimal prorationFactor = Math.Max(0m, (totalDaysInPeriod - absentDays) / totalDaysInPeriod);

            var payslip = new HrmPayslip
            {
                PayrollRunNo = run.PayrollRunNo,
                EmployeeNo = emp.EmployeeNo,
                CompanyNo = run.CompanyNo,
                BranchNo = run.BranchNo,
                PayslipDate = DateTime.UtcNow.Date,
                TotalDays = totalDaysInPeriod,
                PresentDays = totalDaysInPeriod - absentDays,
                AbsentDays = absentDays,
                Status = Calculated,
                IsDeleted = Deleted
            };

            _db.HrmPayslips.Add(payslip);
            await _db.SaveChangesAsync(cancellationToken); // Save to generate PayslipNo

            decimal empEarnings = 0m;
            decimal empDeductions = 0m;

            foreach (var line in structureLines)
            {
                if (!components.TryGetValue(line.ComponentNo, out var comp)) continue;

                decimal amount = Math.Round(line.Amount * (comp.IsProrated == 1 ? prorationFactor : 1m), 2);
                short lineType = comp.ComponentType == 1 ? (short)1 : (short)2; // 1=Earning, 2=Deduction

                if (lineType == 1) empEarnings += amount;
                else empDeductions += amount;

                _db.HrmPayslipDtls.Add(new HrmPayslipDtl
                {
                    PayslipNo = payslip.PayslipNo,
                    ComponentNo = comp.ComponentNo,
                    ComponentName = comp.ComponentName,
                    LineType = lineType,
                    Amount = amount,
                    CalculationBase = line.CalculationBase,
                    Percentage = line.Percentage,
                    IsDeleted = Deleted
                });
            }

            // Overtime earnings
            // ot_date is a DATE column (DateOnly); status is the numeric workflow code (2 = approved).
            var otFrom = DateOnly.FromDateTime(run.StartDate);
            var otTo = DateOnly.FromDateTime(run.EndDate);

            var otHours = await _db.HrmOvertimes
                .AsNoTracking()
                .Where(o => o.EmployeeNo == emp.EmployeeNo && o.OtDate >= otFrom && o.OtDate <= otTo
                            && o.Status == OvertimeApproved && o.IsDeleted == Deleted)
                .SumAsync(o => (decimal?)o.Hours, cancellationToken) ?? 0m;

            if (otHours > 0)
            {
                decimal otRate = Math.Round((emp.Salary / 240m) * 1.5m, 2); // Standard 240 hours/month at 1.5x
                decimal otAmount = Math.Round(otHours * otRate, 2);
                empEarnings += otAmount;

                _db.HrmPayslipDtls.Add(new HrmPayslipDtl
                {
                    PayslipNo = payslip.PayslipNo,
                    ComponentNo = 0,
                    ComponentName = $"Overtime ({otHours:0.##} hrs)",
                    LineType = 1,
                    Amount = otAmount,
                    IsDeleted = Deleted
                });
            }

            // Loan / Advance deductions
            var activeLoans = await _db.HrmLoanAdvances
                .Where(l => l.EmployeeNo == emp.EmployeeNo && (l.Status == 2 || l.Status == 3) && l.RemainingAmount > 0 && l.IsDeleted == Deleted)
                .ToListAsync(cancellationToken);

            foreach (var loan in activeLoans)
            {
                decimal deductAmount = Math.Min(loan.InstallmentAmount, loan.RemainingAmount);
                empDeductions += deductAmount;

                _db.HrmPayslipDtls.Add(new HrmPayslipDtl
                {
                    PayslipNo = payslip.PayslipNo,
                    ComponentNo = 0,
                    ComponentName = $"Loan Repayment ({loan.LoanType})",
                    LineType = 2,
                    Amount = deductAmount,
                    IsDeleted = Deleted
                });

                loan.RemainingAmount -= deductAmount;
                loan.PaidAmount += deductAmount;
                if (loan.RemainingAmount <= 0) loan.Status = 4; // Closed
            }

            payslip.TotalEarnings = empEarnings;
            payslip.TotalDeductions = empDeductions;
            payslip.NetPayable = empEarnings - empDeductions;

            totalEarningsRun += empEarnings;
            totalDeductionsRun += empDeductions;
            totalNetRun += payslip.NetPayable;

            count++;
        }

        run.TotalEarnings = totalEarningsRun;
        run.TotalDeductions = totalDeductionsRun;
        run.TotalNetPayable = totalNetRun;
        run.Status = Calculated;
        await _db.SaveChangesAsync(cancellationToken);

        return count;
    }

    public async Task ApproveRunAsync(long payrollRunNo, long approvedBy, CancellationToken cancellationToken = default)
    {
        var run = await _db.HrmPayrollRuns.FirstOrDefaultAsync(r => r.PayrollRunNo == payrollRunNo && r.IsDeleted == Deleted, cancellationToken);
        if (run == null) throw new InvalidOperationException($"Payroll run not found: {payrollRunNo}");
        if (run.Status != Calculated) throw new InvalidOperationException("Only calculated payroll runs can be approved.");

        run.Status = Approved;
        run.ApprovedBy = approvedBy;
        run.ApprovedAt = DateTime.UtcNow;

        var payslips = await _db.HrmPayslips.Where(p => p.PayrollRunNo == payrollRunNo).ToListAsync(cancellationToken);
        foreach (var p in payslips) p.Status = Approved;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task PostRunAsync(long payrollRunNo, long postedBy, CancellationToken cancellationToken = default)
    {
        var run = await _db.HrmPayrollRuns.FirstOrDefaultAsync(r => r.PayrollRunNo == payrollRunNo && r.IsDeleted == Deleted, cancellationToken);
        if (run == null) throw new InvalidOperationException($"Payroll run not found: {payrollRunNo}");
        if (run.Status != Approved) throw new InvalidOperationException("Only approved payroll runs can be posted (paid).");

        run.Status = Paid;
        run.PaidBy = postedBy;
        run.PaidAt = DateTime.UtcNow;

        var payslips = await _db.HrmPayslips.Where(p => p.PayrollRunNo == payrollRunNo).ToListAsync(cancellationToken);
        foreach (var p in payslips) p.Status = Paid;

        // Emit outbox event for GL financial posting (FinPostingService background worker)
        var payloadObj = new
        {
            payrollRunNo = run.PayrollRunNo,
            periodName = run.PeriodName,
            branchNo = run.BranchNo,
            totalEarnings = run.TotalEarnings,
            totalDeductions = run.TotalDeductions,
            netPayable = run.TotalNetPayable,
            narration = $"Payroll Posted for {run.PeriodName} (Net: {run.TotalNetPayable:F2})"
        };

        var outbox = new EventOutbox
        {
            CompanyNo = run.CompanyNo ?? 0,
            BranchNo = run.BranchNo ?? 0,
            AggregateType = "HRM_PAYROLL",
            AggregateId = run.PayrollRunNo.ToString(),
            EventType = "PayrollPosted",
            Payload = JsonSerializer.Serialize(payloadObj),
            Status = 1, // Pending
            AvailableAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _db.EventOutboxes.Add(outbox);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
