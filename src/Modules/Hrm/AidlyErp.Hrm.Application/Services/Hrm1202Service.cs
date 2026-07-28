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
/// HRM_1202 Salary Processing — full port of Java Hrm1202Service (1293 lines).
/// Consumes each employee's Active salary structure + attendance for the period
/// and materializes immutable payslips. State machine: Draft → Calculated → Approved → Paid.
/// Includes: per-component line-by-line calculation, formula evaluator, tax-by-slab,
/// overtime policy, LWP deduction, attendance locking, and GL event outbox emission.
/// </summary>
public interface IHrm1202Service
{
    Task<List<Hrm1202PayrollRunDto>> GetListAsync(CancellationToken ct = default);
    Task<Hrm1202PayrollRunDto> GetDetailAsync(long no, CancellationToken ct = default);
    Task<Hrm1202PayrollRunDto> CreateRunAsync(Hrm1202PayrollRunDto dto, CancellationToken ct = default);
    Task<Hrm1202PayrollRunDto> CalculateAsync(long no, CancellationToken ct = default);
    Task<Hrm1202PayrollRunDto> ApproveAsync(long no, CancellationToken ct = default);
    Task<Hrm1202PayrollRunDto> MarkPaidAsync(long no, CancellationToken ct = default);
    Task<Hrm1202PayrollRunDto> CancelAsync(long no, CancellationToken ct = default);
    Task DeleteAsync(long no, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long payrollRunNo, bool approved, CancellationToken ct = default);
}

public class Hrm1202Service : IHrm1202Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IApprovalService _approvalService;
    private readonly IHrm1008Service _settingsService;

    private const short StDraft = 1, StCalculated = 2, StApproved = 3, StPaid = 4, StCancelled = 5;
    private const short RunRegular = 1, RunBonus = 3;
    private const short BonusApproved = 3, BonusPaid = 4;
    private const short CtEarning = 1, CtDeduction = 2, CtEmployer = 3, CtStatutory = 4;
    private const short AttPresent = 1, AttAbsent = 2, AttLeave = 7, AttLwp = 8;
    private const int DivScale = 8;
    private const string BonusComponentId = "FEST_BONUS";
    private const string OutboxAggregate = "HRM_PAYROLL";
    private const string OutboxEvent = "PayrollPosted";

    public Hrm1202Service(IHrmDbContext db, ICompanyBranchContext ctx, IApprovalService approvalService, IHrm1008Service settingsService)
    {
        _db = db;
        _ctx = ctx;
        _approvalService = approvalService;
        _settingsService = settingsService;
    }

    // ── Reads ──────────────────────────────────────────────────────────────

    public async Task<List<Hrm1202PayrollRunDto>> GetListAsync(CancellationToken ct = default) =>
        await _db.HrmPayrollRuns.AsNoTracking()
            .Where(r => r.IsDeleted == 0)
            .OrderByDescending(r => r.PayrollRunNo)
            .Select(r => ToDto(r, false))
            .ToListAsync(ct);

    public async Task<Hrm1202PayrollRunDto> GetDetailAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        return await ToDtoAsync(run, true, ct);
    }

    // ── Create (Draft) ─────────────────────────────────────────────────────

    public async Task<Hrm1202PayrollRunDto> CreateRunAsync(Hrm1202PayrollRunDto dto, CancellationToken ct = default)
    {
        long branchNo = dto.BranchNo ?? _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch is required");
        string period = RequireText(dto.PayPeriod, "Pay period");
        short runType = dto.RunType ?? 1;
        var start = dto.PeriodStart;
        var end = dto.PeriodEnd;
        if (end < start) throw new ValidationException("Period end cannot be before period start");

        long? bonusRunNo = null;
        if (runType == RunBonus)
        {
            bonusRunNo = dto.BonusRunNo ?? throw new ValidationException("Bonus run is required for bonus payroll");
            var bonusRun = await _db.HrmBonusRuns.FirstOrDefaultAsync(b => b.BonusRunNo == bonusRunNo.Value && b.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Bonus run not found: bonusRunNo={bonusRunNo}");
            if (bonusRun.BranchNo != branchNo) throw new ValidationException("Selected bonus run belongs to a different branch");
            if (bonusRun.Status != BonusApproved) throw new ValidationException("Selected bonus run must be Approved before payroll processing");
            await EnsureBonusRunAvailableAsync(bonusRunNo.Value, ct);
        }

        bool duplicate = runType == RunBonus
            ? await _db.HrmPayrollRuns.AnyAsync(r => r.BranchNo == branchNo && r.PayPeriod == period && r.RunType == runType && r.BonusRunNo == bonusRunNo && r.IsDeleted == 0, ct)
            : await _db.HrmPayrollRuns.AnyAsync(r => r.BranchNo == branchNo && r.PayPeriod == period && r.RunType == runType && r.IsDeleted == 0, ct);
        if (duplicate) throw new ValidationException($"A payroll run already exists for {period} (type {runType}) in this branch");

        var run = new HrmPayrollRun
        {
            BranchNo = branchNo,
            PayPeriod = period,
            RunType = runType,
            PeriodStart = start,
            PeriodEnd = end,
            BonusRunNo = runType == RunBonus ? bonusRunNo : null,
            Status = StDraft,
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(),
            CreatedAt = DateTime.UtcNow
        };
        _db.HrmPayrollRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        run.PayrollRunId = $"PR{period.Replace("-", "")}{run.PayrollRunNo:D4}";
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(run, true, ct);
    }

    // ── Calculate ──────────────────────────────────────────────────────────

    public async Task<Hrm1202PayrollRunDto> CalculateAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        if (run.Status is not (StDraft or StCalculated))
            throw new ValidationException("Only a Draft or Calculated run can be (re)calculated");

        var userNo = _ctx.CurrentUserNo();

        // Clear previous payslips (recalc).
        var oldSlips = await _db.HrmPayslips.Where(p => p.PayrollRunNo == no && p.IsDeleted == 0).ToListAsync(ct);
        foreach (var old in oldSlips)
        {
            var oldLines = await _db.HrmPayslipDtls.Where(l => l.PayslipNo == old.PayslipNo && l.IsDeleted == 0).ToListAsync(ct);
            foreach (var l in oldLines) l.PerformSoftDelete(userNo);
            old.PerformSoftDelete(userNo);
        }

        run.CalculationStartedAt = DateTime.UtcNow;
        run.CalculationCompletedAt = null;
        await _db.SaveChangesAsync(ct);

        PayrollTotals totals = run.RunType == RunBonus
            ? await CalculateBonusPayrollAsync(run, userNo, ct)
            : await CalculateRegularPayrollAsync(run, userNo, ct);

        run.TotalGross = totals.TotalGross;
        run.TotalDeduction = totals.TotalDeduction;
        run.TotalNet = totals.TotalNet;
        run.EmployeeCount = totals.EmployeeCount;
        run.Status = StCalculated;
        run.CalculationCompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(run, true, ct);
    }

    private async Task<PayrollTotals> CalculateRegularPayrollAsync(HrmPayrollRun run, long userNo, CancellationToken ct)
    {
        int periodDays = (int)(run.PeriodEnd - run.PeriodStart).TotalDays + 1;
        decimal totalGross = 0, totalDed = 0, totalNet = 0;
        int count = 0;

        var employees = await _db.HrmEmployees.Where(e => e.BranchNo == run.BranchNo && e.IsDeleted == 0 && e.IsActive == 1).ToListAsync(ct);
        var settings = await GetSettingsAsync(ct);
        var policy = await ResolvePayrollPolicyAsync(run, settings, ct);

        foreach (var emp in employees)
        {
            var structure = await ActiveStructureForAsync(emp.EmployeeNo, run.PeriodEnd, ct);
            if (structure == null) continue;

            var slip = await ComputePayslipAsync(run, emp.EmployeeNo, structure, periodDays, userNo, settings, policy, ct);
            totalGross += slip.GrossEarning;
            totalDed += slip.TotalDeduction;
            totalNet += slip.NetPay;
            count++;
        }

        return new PayrollTotals(totalGross, totalDed, totalNet, count);
    }

    private async Task<PayrollTotals> CalculateBonusPayrollAsync(HrmPayrollRun run, long userNo, CancellationToken ct)
    {
        long bonusRunNo = run.BonusRunNo ?? throw new ValidationException("Bonus run is required for bonus payroll");
        var bonusRun = await _db.HrmBonusRuns.FirstOrDefaultAsync(b => b.BonusRunNo == bonusRunNo && b.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Bonus run not found: bonusRunNo={bonusRunNo}");
        if (bonusRun.Status is not (BonusApproved or BonusPaid))
            throw new ValidationException("Linked bonus run is not available for payroll calculation");

        decimal totalGross = 0;
        int count = 0;

        var bonusLines = await _db.HrmBonusLines.Where(l => l.BonusRunNo == bonusRunNo && l.IsDeleted == 0).OrderBy(l => l.BonusLineNo).ToListAsync(ct);
        var bonusComponent = await LoadBonusComponentAsync(run.BranchNo ?? 0, ct);

        foreach (var bonusLine in bonusLines)
        {
            if (bonusLine.Amount <= 0) continue;
            var slip = await ComputeBonusPayslipAsync(run, bonusRun, bonusLine, bonusComponent, ct);
            totalGross += slip.GrossEarning;
            count++;
        }

        return new PayrollTotals(totalGross, 0, totalGross, count);
    }

    private async Task<HrmPayslip> ComputeBonusPayslipAsync(HrmPayrollRun run, HrmBonusRun bonusRun, HrmBonusLine bonusLine, HrmSalaryComponent bonusComponent, CancellationToken ct)
    {
        var employee = await _db.HrmEmployees.FirstOrDefaultAsync(e => e.EmployeeNo == bonusLine.EmployeeNo && e.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Employee not found: employeeNo={bonusLine.EmployeeNo}");
        var structure = await ActiveStructureForAsync(employee.EmployeeNo, run.PeriodEnd, ct);
        decimal basicSalary = bonusLine.BaseSalary > 0 ? bonusLine.BaseSalary : structure?.BasicSalary ?? 0;
        decimal bonusAmount = RoundMoney(bonusLine.Amount, bonusComponent.RoundingScale, bonusComponent.RoundingMode);

        var slip = new HrmPayslip
        {
            PayrollRunNo = run.PayrollRunNo,
            EmployeeNo = employee.EmployeeNo,
            SalaryStructureNo = structure?.SalaryStructureNo,
            DesignationNo = structure?.DesignationNo ?? employee.DesignationNo,
            GradeNo = structure?.GradeNo ?? employee.GradeNo,
            GradeStepNo = structure?.GradeStepNo,
            BasicSalary = basicSalary,
            PayableDays = 1,
            GrossEarning = bonusAmount,
            TotalDeduction = 0,
            TaxableIncome = bonusComponent.IsTaxable == 1 ? bonusAmount : 0,
            EmployerContribution = 0,
            NetPay = bonusAmount,
            BranchNo = run.BranchNo,
            PaymentStatus = 1,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        _db.HrmPayslips.Add(slip);
        await _db.SaveChangesAsync(ct);

        var dtl = new HrmPayslipDtl
        {
            PayslipNo = slip.PayslipNo,
            ComponentNo = bonusComponent.ComponentNo,
            ComponentName = bonusRun.BonusName,
            ComponentType = CtEarning,
            CalcType = bonusComponent.CalcType,
            CalcValue = bonusComponent.CalcValue,
            FormulaExpression = $"BONUS_RUN:{bonusRun.BonusRunNo}",
            BaseAmount = basicSalary,
            Amount = bonusAmount,
            DisplayOrder = bonusComponent.DisplayOrder,
            AffectsNet = bonusComponent.AffectsNet,
            IsTaxable = bonusComponent.IsTaxable,
            IsStatutory = bonusComponent.IsStatutory,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };
        _db.HrmPayslipDtls.Add(dtl);
        await _db.SaveChangesAsync(ct);

        return slip;
    }

    private async Task<HrmPayslip> ComputePayslipAsync(HrmPayrollRun run, long employeeNo, HrmSalaryStructure structure,
        int periodDays, long userNo, Hrm1008SettingsDto settings, PayrollPolicyContext policy, CancellationToken ct)
    {
        var employee = await _db.HrmEmployees.FirstOrDefaultAsync(e => e.EmployeeNo == employeeNo && e.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Employee not found: employeeNo={employeeNo}");

        var dc = await CountDaysAsync(employeeNo, run.PeriodStart, run.PeriodEnd, ct);
        decimal payableDays = Math.Max(periodDays - dc.Lwp, 0);
        decimal overtimeRate = EvaluatePolicyExpression(policy.OvertimeFormula, structure.BasicSalary, 0, periodDays, dc, 0, settings);
        decimal otAmount = RoundMoney(overtimeRate * dc.OtHours * DefaultOvertimeMultiplier(settings));

        var slip = new HrmPayslip
        {
            PayrollRunNo = run.PayrollRunNo,
            EmployeeNo = employeeNo,
            SalaryStructureNo = structure.SalaryStructureNo,
            DesignationNo = structure.DesignationNo,
            GradeNo = structure.GradeNo,
            GradeStepNo = structure.GradeStepNo,
            BasicSalary = structure.BasicSalary,
            BranchNo = run.BranchNo,
            PaymentStatus = 1,
            IsActive = 1, IsDeleted = 0,
            CreatedBy = userNo, CreatedAt = DateTime.UtcNow
        };
        _db.HrmPayslips.Add(slip);
        await _db.SaveChangesAsync(ct);

        decimal gross = 0, deduction = 0, taxableIncome = 0, employerContribution = 0;
        var structureLines = await BuildPayrollLinesAsync(structure, run.BranchNo ?? 0, settings, ct);

        foreach (var line in structureLines)
        {
            var component = await _db.HrmSalaryComponents.FirstOrDefaultAsync(c => c.ComponentNo == line.ComponentNo && c.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Salary component not found: componentNo={line.ComponentNo}");

            short type = component.ComponentType;
            short calcType = line.CalcType ?? component.CalcType;
            decimal calcValue = line.CalcValue ?? component.CalcValue ?? 0;
            decimal baseAmount = ResolveBaseAmount(calcType, calcValue, structure, gross);
            decimal amount = ResolveLineAmount(line, component, structure, employee, gross, taxableIncome, periodDays, dc, otAmount, 0, settings, run.PeriodEnd);

            if (type == CtEarning)
            {
                gross += amount;
                if (component.IsTaxable == 1) taxableIncome += amount;
            }
            else if (type == CtEmployer)
            {
                employerContribution += amount;
            }
            else if (component.AffectsNet == 1)
            {
                deduction += amount;
            }

            var dtl = new HrmPayslipDtl
            {
                PayslipNo = slip.PayslipNo,
                ComponentNo = line.ComponentNo,
                ComponentName = component.ComponentName,
                ComponentType = type,
                CalcType = calcType,
                CalcValue = calcValue,
                FormulaExpression = line.FormulaExpression,
                BaseAmount = baseAmount,
                Amount = amount,
                DisplayOrder = line.DisplayOrder ?? component.DisplayOrder,
                AffectsNet = component.AffectsNet,
                IsTaxable = component.IsTaxable,
                IsStatutory = component.IsStatutory,
                IsDeleted = 0,
                CreatedBy = userNo, CreatedAt = DateTime.UtcNow
            };
            _db.HrmPayslipDtls.Add(dtl);
        }

        // LWP deduction.
        decimal dailyRate = RoundMoney(EvaluatePolicyExpression(policy.DailyRateFormula, structure.BasicSalary, gross, periodDays, dc, otAmount, settings));
        decimal lwpDeduction = 0;
        if (dc.Lwp > 0)
        {
            lwpDeduction = RoundMoney(EvaluatePolicyExpression(policy.LwpFormula, structure.BasicSalary, gross, periodDays, dc, otAmount, settings));
            deduction += lwpDeduction;
        }

        slip.PresentDays = dc.Present;
        slip.AbsentDays = dc.Absent;
        slip.LeaveDays = dc.Leave;
        slip.LwpDays = dc.Lwp;
        slip.PayableDays = payableDays;
        slip.OtHours = dc.OtHours;
        slip.OtAmount = otAmount;
        slip.GrossEarning = gross;
        slip.TotalDeduction = deduction;
        slip.TaxableIncome = taxableIncome;
        slip.EmployerContribution = employerContribution;
        slip.NetPay = gross - deduction;
        slip.CalculationSnapshot = BuildPayslipSnapshot(run, structure, employee, dc, periodDays, gross, deduction, lwpDeduction, taxableIncome, employerContribution, policy, overtimeRate, dailyRate, settings);
        await _db.SaveChangesAsync(ct);

        return slip;
    }

    // ── Workflow transitions ───────────────────────────────────────────────

    public async Task<Hrm1202PayrollRunDto> ApproveAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        if (run.Status != StCalculated) throw new ValidationException("Only a Calculated run can be approved");
        await EnsurePayslipsExistAsync(no, "approved", ct);

        if (!run.ApprovalRequestNo.HasValue)
        {
            var outcome = await _approvalService.RaiseAsync("HRM_PAYROLL", run.PayrollRunNo, $"PAY-{run.PayrollRunNo}", run.TotalNet, ct);
            if (outcome.AutoApproved)
            {
                await ApplyApprovalOutcomeAsync(run.PayrollRunNo, true, ct);
            }
            else
            {
                run.ApprovalRequestNo = outcome.ApprovalRequestNo;
                await _db.SaveChangesAsync(ct);
            }
        }
        else
        {
            await _approvalService.ActAsync(run.ApprovalRequestNo.Value, true, null, ct);
        }

        return await ToDtoAsync(await LoadLiveAsync(no, ct), true, ct);
    }

    public async Task ApplyApprovalOutcomeAsync(long payrollRunNo, bool approved, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(payrollRunNo, ct);
        if (!approved)
        {
            run.ApprovalRequestNo = null;
            run.Status = StCalculated;
            await _db.SaveChangesAsync(ct);
            return;
        }

        await EnsurePayslipsExistAsync(payrollRunNo, "approved", ct);
        if (run.RunType != RunBonus) await LockAttendanceAsync(run, ct);

        run.Status = StApproved;
        run.ApprovedBy = _ctx.CurrentUserNo();
        run.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await EmitPayrollPostedAsync(run, ct);
    }

    private async Task EmitPayrollPostedAsync(HrmPayrollRun run, CancellationToken ct)
    {
        try
        {
            decimal gross = run.TotalGross, net = run.TotalNet, deduction = run.TotalDeduction;
            if (gross <= 0) return;

            // Check for existing event.
            var existing = await _db.EventOutboxes.AsNoTracking()
                .Where(e => e.AggregateType == OutboxAggregate && e.AggregateId == run.PayrollRunNo.ToString() && e.EventType == OutboxEvent)
                .OrderByDescending(e => e.EventNo)
                .FirstOrDefaultAsync(ct);
            if (existing != null)
            {
                if (existing.EventNo != run.PostedEventNo)
                {
                    run.PostedEventNo = existing.EventNo;
                    await _db.SaveChangesAsync(ct);
                }
                return;
            }

            var legs = new List<object>
            {
                new { legKey = "EXPENSE", amount = gross, drCr = "dr" },
                new { legKey = "PAYABLE", amount = net, drCr = "cr" }
            };
            if (deduction > 0) legs.Add(new { legKey = "STATUTORY", amount = deduction, drCr = "cr" });

            var payload = new
            {
                voucherDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                narration = $"Payroll {run.PayPeriod}",
                branchNo = run.BranchNo,
                legs
            };

            long companyNo = _ctx.CurrentCompanyNo() ?? 0;
            if (companyNo == 0) return;

            var ev = new EventOutbox
            {
                CompanyNo = companyNo,
                BranchNo = run.BranchNo,
                AggregateType = OutboxAggregate,
                AggregateId = run.PayrollRunNo.ToString(),
                EventType = OutboxEvent,
                Payload = JsonSerializer.Serialize(payload),
                Status = 1,
                IsProcessed = 0,
                CreatedAt = DateTime.UtcNow
            };
            _db.EventOutboxes.Add(ev);
            await _db.SaveChangesAsync(ct);
            run.PostedEventNo = ev.EventNo;
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Never block payroll approval on the GL hand-off.
        }
    }

    public async Task<Hrm1202PayrollRunDto> MarkPaidAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        if (run.Status != StApproved) throw new ValidationException("Only an Approved run can be marked Paid");
        await EnsurePayslipsExistAsync(no, "paid", ct);

        var slips = await _db.HrmPayslips.Where(p => p.PayrollRunNo == no && p.IsDeleted == 0).ToListAsync(ct);
        foreach (var s in slips) s.PaymentStatus = 2;

        run.Status = StPaid;
        run.PaidAt = DateTime.UtcNow;
        run.PaidBy = _ctx.CurrentUserNo();

        // Mark linked bonus run as paid.
        if (run.RunType == RunBonus && run.BonusRunNo.HasValue)
        {
            var bonusRun = await _db.HrmBonusRuns.FirstOrDefaultAsync(b => b.BonusRunNo == run.BonusRunNo.Value && b.IsDeleted == 0, ct);
            if (bonusRun != null && bonusRun.Status == BonusApproved)
            {
                bonusRun.Status = BonusPaid;
            }
        }

        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(run, true, ct);
    }

    public async Task<Hrm1202PayrollRunDto> CancelAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        if (run.Status is StApproved or StPaid)
            throw new ValidationException("An approved/paid run is immutable — process a reversal/supplementary run instead");
        if (run.Status == StCancelled) throw new ValidationException("Run is already cancelled");

        var userNo = _ctx.CurrentUserNo();
        var slips = await _db.HrmPayslips.Where(p => p.PayrollRunNo == no && p.IsDeleted == 0).ToListAsync(ct);
        foreach (var s in slips) s.PerformSoftDelete(userNo);

        run.Status = StCancelled;
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(run, true, ct);
    }

    public async Task DeleteAsync(long no, CancellationToken ct = default)
    {
        var run = await LoadLiveAsync(no, ct);
        if (run.Status is not (StDraft or StCancelled))
            throw new ValidationException("Only a Draft or Cancelled run can be deleted");

        var userNo = _ctx.CurrentUserNo();
        var slips = await _db.HrmPayslips.Where(p => p.PayrollRunNo == no && p.IsDeleted == 0).ToListAsync(ct);
        foreach (var s in slips) s.PerformSoftDelete(userNo);

        run.PerformSoftDelete(userNo);
        await _db.SaveChangesAsync(ct);
    }

    // ── Attendance helpers ─────────────────────────────────────────────────

    private async Task<DayCounts> CountDaysAsync(long employeeNo, DateTime from, DateTime to, CancellationToken ct)
    {
        decimal present = 0, absent = 0, leave = 0, lwp = 0, otHours = 0;
        var attendance = await _db.HrmAttendances.AsNoTracking()
            .Where(a => a.EmployeeNo == employeeNo && a.IsDeleted == 0)
            .ToListAsync(ct);

        foreach (var a in attendance)
        {
            var d = a.AttDate;
            if (d < from || d > to) continue;
            otHours += a.OtHours;
            switch (a.Status)
            {
                case 1: case 3: case 4: present++; break; // present/late/half
                case AttAbsent: absent++; break;
                case AttLeave: leave++; break;
                case AttLwp: lwp++; break;
            }
        }

        return new DayCounts(present, absent, leave, lwp, otHours);
    }

    private async Task LockAttendanceAsync(HrmPayrollRun run, CancellationToken ct)
    {
        var attendance = await _db.HrmAttendances
            .Where(a => a.BranchNo == run.BranchNo && a.IsDeleted == 0 && a.AttDate >= run.PeriodStart && a.AttDate <= run.PeriodEnd)
            .ToListAsync(ct);
        foreach (var a in attendance) a.IsLocked = 1;
    }

    // ── Structure / component helpers ──────────────────────────────────────

    private async Task<HrmSalaryStructure?> ActiveStructureForAsync(long employeeNo, DateTime asOf, CancellationToken ct)
    {
        var structures = await _db.HrmSalaryStructures.AsNoTracking()
            .Where(s => s.EmployeeNo == employeeNo && s.Status == 1 && s.IsDeleted == 0 && s.EffectiveFrom <= asOf)
            .OrderByDescending(s => s.EffectiveFrom)
            .ToListAsync(ct);
        return structures.FirstOrDefault();
    }

    private async Task<List<HrmSalaryStructureDtl>> BuildPayrollLinesAsync(HrmSalaryStructure structure, long branchNo, Hrm1008SettingsDto settings, CancellationToken ct)
    {
        var lines = await _db.HrmSalaryStructureDtls.AsNoTracking()
            .Where(l => l.SalaryStructureNo == structure.SalaryStructureNo && l.IsDeleted == 0)
            .OrderBy(l => l.DisplayOrder).ThenBy(l => l.StructureDtlNo)
            .ToListAsync(ct);

        var existingComponentNos = lines.Select(l => l.ComponentNo).ToHashSet();

        // Auto-include components (OVERTIME, PF_EMP, AIT).
        var allComponents = await _db.HrmSalaryComponents.AsNoTracking()
            .Where(c => c.IsDeleted == 0)
            .OrderBy(c => c.ComponentNo)
            .ToListAsync(ct);

        foreach (var component in allComponents)
        {
            if (component.BranchNo != null && component.BranchNo != branchNo) continue;
            if (!ShouldAutoIncludeComponent(component, settings)) continue;
            if (existingComponentNos.Contains(component.ComponentNo)) continue;

            lines.Add(new HrmSalaryStructureDtl
            {
                SalaryStructureNo = structure.SalaryStructureNo,
                ComponentNo = component.ComponentNo,
                CalcType = component.CalcType,
                CalcValue = component.CalcValue,
                FormulaExpression = component.FormulaExpression,
                IsProratable = component.IsProratable,
                DisplayOrder = component.DisplayOrder,
                Amount = 0,
                BranchNo = branchNo
            });
        }

        return lines;
    }

    private static bool ShouldAutoIncludeComponent(HrmSalaryComponent component, Hrm1008SettingsDto settings)
    {
        var id = component.ComponentId?.Trim().ToUpperInvariant() ?? "";
        if (id == "OVERTIME") return true;
        if (id == "PF_EMP") return settings.EnableProvidentFund is null or 1;
        if (id == "AIT") return true;
        return false;
    }

    private async Task<HrmSalaryComponent> LoadBonusComponentAsync(long branchNo, CancellationToken ct)
    {
        return await _db.HrmSalaryComponents.AsNoTracking()
            .Where(c => c.ComponentId == BonusComponentId && (c.BranchNo == branchNo || c.BranchNo == null) && c.IsDeleted == 0)
            .OrderBy(c => c.BranchNo == branchNo ? 0 : 1).ThenBy(c => c.ComponentNo)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Bonus salary component not found: componentId={BonusComponentId}");
    }

    // ── Amount resolution ──────────────────────────────────────────────────

    private static decimal ResolveBaseAmount(short calcType, decimal calcValue, HrmSalaryStructure structure, decimal currentGross) => calcType switch
    {
        2 => structure.BasicSalary,
        3 => currentGross,
        _ => calcValue
    };

    private decimal ResolveLineAmount(HrmSalaryStructureDtl line, HrmSalaryComponent component,
        HrmSalaryStructure structure, HrmEmployee employee, decimal currentGross, decimal currentTaxableIncome,
        int periodDays, DayCounts dc, decimal otAmount, decimal dailyRate, Hrm1008SettingsDto settings, DateTime periodEnd)
    {
        if (!IsComponentEnabled(component, settings)) return 0;

        short calcType = line.CalcType ?? component.CalcType;
        decimal calcValue = line.CalcValue ?? component.CalcValue ?? 0;
        decimal fixedAmount = line.Amount;

        decimal amount = calcType switch
        {
            2 => PercentOf(structure.BasicSalary, calcValue),
            3 => PercentOf(currentGross, calcValue),
            4 => EvaluateExpression(line.FormulaExpression, structure, employee, currentGross, currentTaxableIncome, periodDays, dc, otAmount, dailyRate, settings, periodEnd),
            _ => fixedAmount != 0 ? fixedAmount : calcValue
        };

        return RoundMoney(amount, component.RoundingScale, component.RoundingMode);
    }

    private static bool IsComponentEnabled(HrmSalaryComponent component, Hrm1008SettingsDto settings)
    {
        var id = component.ComponentId?.Trim().ToUpperInvariant() ?? "";
        if (id.StartsWith("PF") && settings.EnableProvidentFund == 0) return false;
        if (id.Contains("GRAT") && settings.EnableGratuity == 0) return false;
        if (id.Contains("FEST") && settings.EnableFestivalBonus == 0) return false;
        return true;
    }

    // ── Policy / formula helpers ───────────────────────────────────────────

    private async Task<Hrm1008SettingsDto> GetSettingsAsync(CancellationToken ct)
    {
        return await _settingsService.GetSettingsAsync(ct);
    }

    private async Task<PayrollPolicyContext> ResolvePayrollPolicyAsync(HrmPayrollRun run, Hrm1008SettingsDto settings, CancellationToken ct)
    {
        string countryCode = settings.CountryCode?.Trim().ToUpperInvariant() ?? "BD";

        // Try to find an active policy for the country
        var policy = await _db.HrmPayrollPolicies.AsNoTracking()
            .Where(p => p.IsDeleted == 0 && p.IsActive == 1)
            .OrderByDescending(p => p.PolicyNo)
            .FirstOrDefaultAsync(ct);

        if (policy != null)
        {
            run.CalculationPolicyNo = policy.PolicyNo;
            return new PayrollPolicyContext(
                policy.PolicyNo,
                policy.PolicyName ?? "POLICY",
                countryCode,
                settings.OvertimeFormula ?? "BASIC_SALARY / 104",
                settings.DailyRateFormula ?? "GROSS_SALARY / CALENDAR_DAYS",
                settings.LwpFormula ?? "DAILY_RATE * LWP_DAYS"
            );
        }

        // Fallback to settings formulas or defaults
        return new PayrollPolicyContext(
            null,
            "BD_DEFAULT",
            countryCode,
            settings.OvertimeFormula ?? "BASIC_SALARY / 104",
            settings.DailyRateFormula ?? "GROSS_SALARY / CALENDAR_DAYS",
            settings.LwpFormula ?? "DAILY_RATE * LWP_DAYS"
        );
    }

    private decimal EvaluatePolicyExpression(string expression, decimal basicSalary, decimal grossSalary,
        int periodDays, DayCounts dc, decimal otAmount, Hrm1008SettingsDto settings)
    {
        return EvaluateExpression(expression, basicSalary, null, grossSalary, grossSalary, periodDays, dc, otAmount, 0, settings, DateTime.UtcNow);
    }

    private decimal EvaluateExpression(string expression, HrmSalaryStructure structure, HrmEmployee? employee,
        decimal grossSalary, decimal taxableIncome, int periodDays, DayCounts dc,
        decimal otAmount, decimal dailyRate, Hrm1008SettingsDto settings, DateTime periodEnd)
    {
        return EvaluateExpression(expression, structure.BasicSalary, employee, grossSalary, taxableIncome, periodDays, dc, otAmount, dailyRate, settings, periodEnd);
    }

    private decimal EvaluateExpression(string expression, decimal basicSalary, HrmEmployee? employee,
        decimal grossSalary, decimal taxableIncome, int periodDays, DayCounts dc,
        decimal otAmount, decimal dailyRate, Hrm1008SettingsDto settings, DateTime periodEnd)
    {
        var expr = string.IsNullOrWhiteSpace(expression) ? "0" : expression.Trim();
        if (expr.Equals("TAXABLE_INCOME_BY_SLAB", StringComparison.OrdinalIgnoreCase))
            return CalculateIncomeTaxBySlab(taxableIncome, employee, settings, periodEnd);

        var vars = new Dictionary<string, decimal>
        {
            ["BASIC_SALARY"] = basicSalary,
            ["GROSS_SALARY"] = grossSalary,
            ["GROSS_EARNING"] = grossSalary,
            ["TAXABLE_INCOME"] = taxableIncome,
            ["CALENDAR_DAYS"] = Math.Max(periodDays, 0),
            ["PRESENT_DAYS"] = dc.Present,
            ["ABSENT_DAYS"] = dc.Absent,
            ["LEAVE_DAYS"] = dc.Leave,
            ["LWP_DAYS"] = dc.Lwp,
            ["PAYABLE_DAYS"] = Math.Max(Math.Max(periodDays, 0) - dc.Lwp, 0),
            ["OT_HOURS"] = dc.OtHours,
            ["OT_AMOUNT"] = otAmount,
            ["DAILY_RATE"] = dailyRate,
            ["OVERTIME_MULTIPLIER"] = DefaultOvertimeMultiplier(settings)
        };

        return RoundMoney(ExpressionEvaluator.Evaluate(expr, vars));
    }

    private decimal CalculateIncomeTaxBySlab(decimal monthlyTaxableIncome, HrmEmployee? employee, Hrm1008SettingsDto settings, DateTime periodEnd)
    {
        if (monthlyTaxableIncome <= 0) return 0;

        string countryCode = settings.CountryCode?.Trim().ToUpperInvariant() ?? "BD";
        string fiscalYear = ResolveFiscalYear(periodEnd, settings.FiscalYearStart);
        string taxpayerClass = NormalizeTaxpayerClass(employee?.TaxpayerClass);

        var slabs = _db.HrmTaxSlabs.AsNoTracking()
            .Where(s => s.CountryCode == countryCode && s.FiscalYear == fiscalYear && s.TaxpayerClass == taxpayerClass && s.IsDeleted == 0)
            .OrderBy(s => s.SlabOrder)
            .ToList();

        if (slabs.Count == 0 && taxpayerClass != "GENERAL")
        {
            slabs = _db.HrmTaxSlabs.AsNoTracking()
                .Where(s => s.CountryCode == countryCode && s.FiscalYear == fiscalYear && s.TaxpayerClass == "GENERAL" && s.IsDeleted == 0)
                .OrderBy(s => s.SlabOrder)
                .ToList();
        }
        if (slabs.Count == 0) return 0;

        decimal annualTaxable = monthlyTaxableIncome * 12;
        decimal annualTax = 0;

        foreach (var slab in slabs)
        {
            decimal from = slab.FromAmount;
            decimal? to = slab.ToAmount;
            bool within = annualTaxable >= from && (to == null || annualTaxable <= to);
            if (!within) continue;

            decimal fixedAmt = slab.FixedAmount;
            decimal rate = slab.RatePercent;
            decimal taxablePortion = Math.Max(annualTaxable - from, 0);
            annualTax = fixedAmt + PercentOf(taxablePortion, rate);
            break;
        }

        if (annualTax == 0 && slabs.Count > 0)
        {
            var top = slabs[^1];
            decimal taxablePortion = Math.Max(annualTaxable - top.FromAmount, 0);
            annualTax = top.FixedAmount + PercentOf(taxablePortion, top.RatePercent);
        }

        return RoundMoney(annualTax / 12);
    }

    private static string ResolveFiscalYear(DateTime date, string? fiscalYearStart)
    {
        var start = string.IsNullOrWhiteSpace(fiscalYearStart) ? "07-01" : fiscalYearStart.Trim();
        var parts = start.Split('-');
        int startMonth = parts.Length > 0 ? int.Parse(parts[0]) : 7;
        int startDay = parts.Length > 1 ? int.Parse(parts[1]) : 1;
        var currentYearStart = new DateTime(date.Year, startMonth, Math.Min(startDay, DateTime.DaysInMonth(date.Year, startMonth)));
        int fiscalStartYear = date < currentYearStart ? date.Year - 1 : date.Year;
        return $"{fiscalStartYear}-{fiscalStartYear + 1}";
    }

    private static decimal DefaultOvertimeMultiplier(Hrm1008SettingsDto settings) => settings.OvertimeMultiplier ?? 1m;

    // ── Snapshot builder ───────────────────────────────────────────────────

    private static string? BuildPayslipSnapshot(HrmPayrollRun run, HrmSalaryStructure structure, HrmEmployee? employee,
        DayCounts dc, int periodDays, decimal gross, decimal deduction, decimal lwpDeduction,
        decimal taxableIncome, decimal employerContribution, PayrollPolicyContext policy,
        decimal overtimeRate, decimal dailyRate, Hrm1008SettingsDto settings)
    {
        try
        {
            var snapshot = new Dictionary<string, object?>
            {
                ["payPeriod"] = run.PayPeriod,
                ["periodStart"] = run.PeriodStart,
                ["periodEnd"] = run.PeriodEnd,
                ["salaryStructureNo"] = structure.SalaryStructureNo,
                ["taxpayerClass"] = NormalizeTaxpayerClass(employee?.TaxpayerClass),
                ["designationNo"] = structure.DesignationNo,
                ["gradeNo"] = structure.GradeNo,
                ["gradeStepNo"] = structure.GradeStepNo,
                ["basicSalary"] = structure.BasicSalary,
                ["periodDays"] = periodDays,
                ["presentDays"] = dc.Present,
                ["absentDays"] = dc.Absent,
                ["leaveDays"] = dc.Leave,
                ["lwpDays"] = dc.Lwp,
                ["otHours"] = dc.OtHours,
                ["grossEarning"] = gross,
                ["totalDeduction"] = deduction,
                ["lwpDeduction"] = lwpDeduction,
                ["taxableIncome"] = taxableIncome,
                ["employerContribution"] = employerContribution,
                ["countryCode"] = policy.CountryCode,
                ["policyNo"] = policy.PolicyNo,
                ["policyId"] = policy.PolicyId,
                ["dailyRateFormula"] = policy.DailyRateFormula,
                ["lwpFormula"] = policy.LwpFormula,
                ["otRateFormula"] = policy.OvertimeFormula,
                ["dailyRate"] = dailyRate,
                ["overtimeRate"] = overtimeRate,
                ["overtimeMultiplier"] = DefaultOvertimeMultiplier(settings)
            };
            return JsonSerializer.Serialize(snapshot);
        }
        catch
        {
            return null;
        }
    }

    // ── Bonus run helpers ──────────────────────────────────────────────────

    private async Task EnsureBonusRunAvailableAsync(long bonusRunNo, CancellationToken ct)
    {
        var existing = await _db.HrmPayrollRuns.FirstOrDefaultAsync(r => r.BonusRunNo == bonusRunNo && r.IsDeleted == 0 && r.Status != StCancelled, ct);
        if (existing != null)
            throw new ValidationException($"Selected bonus run is already linked to payroll run {existing.PayrollRunId}");
    }

    private async Task EnsurePayslipsExistAsync(long payrollRunNo, string action, CancellationToken ct)
    {
        var hasPayslips = await _db.HrmPayslips.AnyAsync(p => p.PayrollRunNo == payrollRunNo && p.IsDeleted == 0, ct);
        if (!hasPayslips) throw new ValidationException($"Payroll run cannot be {action} because no payslips were calculated");
    }

    // ── Load helpers ───────────────────────────────────────────────────────

    private async Task<HrmPayrollRun> LoadLiveAsync(long no, CancellationToken ct) =>
        await _db.HrmPayrollRuns.FirstOrDefaultAsync(r => r.PayrollRunNo == no && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Payroll run not found: no={no}");

    // ── Math helpers ───────────────────────────────────────────────────────

    private static decimal PercentOf(decimal baseVal, decimal percent) =>
        baseVal * percent / 100m;

    private static decimal RoundMoney(decimal amount, short? roundingScale = null, short? roundingMode = null)
    {
        int scale = roundingScale ?? 2;
        var mode = (roundingMode ?? 2) switch
        {
            1 => MidpointRounding.ToZero,
            3 => MidpointRounding.ToPositiveInfinity,
            _ => MidpointRounding.AwayFromZero
        };
        return Math.Round(amount, scale, mode);
    }

    private static string NormalizeTaxpayerClass(string? taxpayerClass) =>
        string.IsNullOrWhiteSpace(taxpayerClass) ? "GENERAL" : taxpayerClass.Trim().ToUpperInvariant();

    private static string RequireText(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw new ValidationException($"{field} is required") : value.Trim();

    // ── DTO mapping ────────────────────────────────────────────────────────

    private static Hrm1202PayrollRunDto ToDto(HrmPayrollRun r, bool withPayslips) => new()
    {
        PayrollRunNo = r.PayrollRunNo,
        PayrollRunId = r.PayrollRunId,
        PayPeriod = r.PayPeriod,
        RunType = r.RunType,
        PeriodStart = r.PeriodStart,
        PeriodEnd = r.PeriodEnd,
        BonusRunNo = r.BonusRunNo,
        Status = r.Status,
        TotalGross = r.TotalGross,
        TotalDeduction = r.TotalDeduction,
        TotalNet = r.TotalNet,
        CalculationPolicyNo = r.CalculationPolicyNo,
        EmployeeCount = r.EmployeeCount,
        CalculationStartedAt = r.CalculationStartedAt,
        CalculationCompletedAt = r.CalculationCompletedAt,
        ApprovedBy = r.ApprovedBy,
        ApprovedAt = r.ApprovedAt,
        PostedEventNo = r.PostedEventNo,
        PaidAt = r.PaidAt,
        PaidBy = r.PaidBy,
        BranchNo = r.BranchNo,
        Remarks = r.Remarks,
        IsActive = r.IsActive,
        RowVersion = r.RowVersion
    };

    private async Task<Hrm1202PayrollRunDto> ToDtoAsync(HrmPayrollRun r, bool withPayslips, CancellationToken ct)
    {
        var dto = ToDto(r, false);

        // Resolve bonus run name.
        if (r.BonusRunNo.HasValue)
        {
            var bonusRun = await _db.HrmBonusRuns.AsNoTracking()
                .FirstOrDefaultAsync(b => b.BonusRunNo == r.BonusRunNo.Value && b.IsDeleted == 0, ct);
            dto.BonusRunName = bonusRun?.BonusName;
            dto.BonusPeriod = bonusRun?.PayPeriod;
        }

        if (withPayslips)
        {
            var slips = await _db.HrmPayslips.AsNoTracking()
                .Where(p => p.PayrollRunNo == r.PayrollRunNo && p.IsDeleted == 0)
                .OrderBy(p => p.PayslipNo)
                .ToListAsync(ct);

            dto.Payslips = new List<Hrm1202PayslipDto>();
            foreach (var s in slips)
            {
                var slipDto = new Hrm1202PayslipDto
                {
                    PayslipNo = s.PayslipNo,
                    PayrollRunNo = s.PayrollRunNo,
                    EmployeeNo = s.EmployeeNo,
                    SalaryStructureNo = s.SalaryStructureNo,
                    DesignationNo = s.DesignationNo,
                    GradeNo = s.GradeNo,
                    GradeStepNo = s.GradeStepNo,
                    BasicSalary = s.BasicSalary,
                    PresentDays = s.PresentDays,
                    AbsentDays = s.AbsentDays,
                    LeaveDays = s.LeaveDays,
                    LwpDays = s.LwpDays,
                    PayableDays = s.PayableDays,
                    OtHours = s.OtHours,
                    OtAmount = s.OtAmount,
                    GrossEarning = s.GrossEarning,
                    TotalDeduction = s.TotalDeduction,
                    TaxableIncome = s.TaxableIncome,
                    EmployerContribution = s.EmployerContribution,
                    NetPay = s.NetPay,
                    PayMethod = s.PayMethod,
                    PaymentStatus = s.PaymentStatus,
                    CalculationSnapshot = s.CalculationSnapshot
                };

                // Load payslip detail lines.
                var lines = await _db.HrmPayslipDtls.AsNoTracking()
                    .Where(l => l.PayslipNo == s.PayslipNo && l.IsDeleted == 0)
                    .OrderBy(l => l.DisplayOrder).ThenBy(l => l.PayslipDtlNo)
                    .ToListAsync(ct);

                slipDto.Lines = lines.Select(l => new Hrm1202PayslipLineDto
                {
                    PayslipDtlNo = l.PayslipDtlNo,
                    ComponentNo = l.ComponentNo,
                    ComponentName = l.ComponentName,
                    ComponentType = l.ComponentType,
                    CalcType = l.CalcType,
                    CalcValue = l.CalcValue,
                    FormulaExpression = l.FormulaExpression,
                    BaseAmount = l.BaseAmount,
                    Amount = l.Amount,
                    DisplayOrder = l.DisplayOrder,
                    AffectsNet = l.AffectsNet,
                    IsTaxable = l.IsTaxable,
                    IsStatutory = l.IsStatutory
                }).ToList();

                dto.Payslips.Add(slipDto);
            }
        }

        return dto;
    }

    // ── Records ────────────────────────────────────────────────────────────

    private record DayCounts(decimal Present, decimal Absent, decimal Leave, decimal Lwp, decimal OtHours);
    private record PayrollTotals(decimal TotalGross, decimal TotalDeduction, decimal TotalNet, int EmployeeCount);
    private record PayrollPolicyContext(long? PolicyNo, string PolicyId, string CountryCode, string OvertimeFormula, string DailyRateFormula, string LwpFormula);

    // ── Formula evaluator ──────────────────────────────────────────────────

    private static class ExpressionEvaluator
    {
        public static decimal Evaluate(string expression, Dictionary<string, decimal> variables)
        {
            var evaluator = new Parser(expression, variables);
            return evaluator.Parse();
        }

        private sealed class Parser
        {
            private readonly string _expr;
            private readonly Dictionary<string, decimal> _vars;
            private int _pos;

            public Parser(string expression, Dictionary<string, decimal> variables)
            {
                _expr = expression.Replace(" ", "");
                _vars = variables;
            }

            public decimal Parse()
            {
                var value = ParseExpression();
                if (_pos != _expr.Length) throw new ValidationException($"Invalid formula expression: {_expr}");
                return value;
            }

            private decimal ParseExpression()
            {
                var value = ParseTerm();
                while (_pos < _expr.Length)
                {
                    char ch = _expr[_pos];
                    if (ch == '+') { _pos++; value += ParseTerm(); }
                    else if (ch == '-') { _pos++; value -= ParseTerm(); }
                    else break;
                }
                return value;
            }

            private decimal ParseTerm()
            {
                var value = ParseFactor();
                while (_pos < _expr.Length)
                {
                    char ch = _expr[_pos];
                    if (ch == '*') { _pos++; value *= ParseFactor(); }
                    else if (ch == '/') { _pos++; value /= ParseFactor(); }
                    else break;
                }
                return value;
            }

            private decimal ParseFactor()
            {
                if (_pos >= _expr.Length) throw new ValidationException($"Unexpected end of formula: {_expr}");
                char ch = _expr[_pos];
                if (ch == '+') { _pos++; return ParseFactor(); }
                if (ch == '-') { _pos++; return -ParseFactor(); }
                if (ch == '(') { _pos++; var v = ParseExpression(); Expect(')'); return v; }
                if (char.IsDigit(ch) || ch == '.') return ParseNumber();
                if (char.IsLetter(ch) || ch == '_') return ParseVariable();
                throw new ValidationException($"Unexpected token in formula: {ch}");
            }

            private decimal ParseNumber()
            {
                int start = _pos;
                while (_pos < _expr.Length && (char.IsDigit(_expr[_pos]) || _expr[_pos] == '.')) _pos++;
                return decimal.Parse(_expr[start.._pos]);
            }

            private decimal ParseVariable()
            {
                int start = _pos;
                while (_pos < _expr.Length && (char.IsLetterOrDigit(_expr[_pos]) || _expr[_pos] == '_')) _pos++;
                string name = _expr[start.._pos].ToUpperInvariant();
                return _vars.GetValueOrDefault(name, 0m);
            }

            private void Expect(char expected)
            {
                if (_pos >= _expr.Length || _expr[_pos] != expected)
                    throw new ValidationException($"Invalid formula expression: {_expr}");
                _pos++;
            }
        }
    }
}
