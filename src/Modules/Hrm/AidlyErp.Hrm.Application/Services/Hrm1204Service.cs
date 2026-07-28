using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Hrm.Application.Dto;

namespace AidlyErp.Hrm.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1204Service — Payslip Generator
// Port of Java Hrm1204Service (128 lines). Read-only printable payslip with
// full component lines, component name resolution fallback, run existence check.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1204Service
{
    Task<List<Hrm1204RunLiteDto>> GetRunsAsync(CancellationToken ct = default);
    Task<List<Hrm1204PayslipLiteDto>> GetPayslipsAsync(long runNo, CancellationToken ct = default);
    Task<Hrm1204PayslipDto> GetPayslipDetailAsync(long payslipNo, CancellationToken ct = default);
}

public class Hrm1204Service : IHrm1204Service
{
    private readonly IHrmDbContext _db;
    public Hrm1204Service(IHrmDbContext db) => _db = db;

    public async Task<List<Hrm1204RunLiteDto>> GetRunsAsync(CancellationToken ct = default) =>
        await _db.HrmPayrollRuns.AsNoTracking()
            .Where(r => r.IsDeleted == 0)
            .OrderByDescending(r => r.PayrollRunNo)
            .Select(r => new Hrm1204RunLiteDto
            {
                PayrollRunNo = r.PayrollRunNo,
                PayrollRunId = r.PayrollRunId,
                PayPeriod = r.PayPeriod,
                RunType = r.RunType,
                Status = r.Status,
                EmployeeCount = r.EmployeeCount,
                TotalNet = r.TotalNet
            })
            .ToListAsync(ct);

    public async Task<List<Hrm1204PayslipLiteDto>> GetPayslipsAsync(long runNo, CancellationToken ct = default)
    {
        // Validate run exists.
        var runExists = await _db.HrmPayrollRuns.AnyAsync(r => r.PayrollRunNo == runNo && r.IsDeleted == 0, ct);
        if (!runExists) throw new NotFoundException($"Payroll run not found: runNo={runNo}");

        return await _db.HrmPayslips.AsNoTracking()
            .Where(p => p.PayrollRunNo == runNo && p.IsDeleted == 0)
            .OrderBy(p => p.PayslipNo)
            .Select(p => new Hrm1204PayslipLiteDto
            {
                PayslipNo = p.PayslipNo,
                EmployeeNo = p.EmployeeNo,
                NetPay = p.NetPay,
                PaymentStatus = p.PaymentStatus
            })
            .ToListAsync(ct);
    }

    public async Task<Hrm1204PayslipDto> GetPayslipDetailAsync(long payslipNo, CancellationToken ct = default)
    {
        var slip = await _db.HrmPayslips.AsNoTracking().FirstOrDefaultAsync(p => p.PayslipNo == payslipNo && p.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Payslip not found: payslipNo={payslipNo}");

        // Load run info for period dates.
        var run = await _db.HrmPayrollRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.PayrollRunNo == slip.PayrollRunNo && r.IsDeleted == 0, ct);

        // Build component name fallback map.
        var componentNames = await _db.HrmSalaryComponents.AsNoTracking()
            .Where(c => c.IsDeleted == 0)
            .ToDictionaryAsync(c => c.ComponentNo, c => c.ComponentName, ct);

        // Load payslip detail lines with component name fallback.
        var lines = await _db.HrmPayslipDtls.AsNoTracking()
            .Where(l => l.PayslipNo == payslipNo && l.IsDeleted == 0)
            .OrderBy(l => l.DisplayOrder).ThenBy(l => l.PayslipDtlNo)
            .ToListAsync(ct);

        return new Hrm1204PayslipDto
        {
            PayslipNo = slip.PayslipNo,
            PayrollRunNo = slip.PayrollRunNo,
            PayPeriod = run?.PayPeriod,
            PeriodStart = run?.PeriodStart,
            PeriodEnd = run?.PeriodEnd,
            EmployeeNo = slip.EmployeeNo,
            SalaryStructureNo = slip.SalaryStructureNo,
            DesignationNo = slip.DesignationNo,
            GradeNo = slip.GradeNo,
            GradeStepNo = slip.GradeStepNo,
            BasicSalary = slip.BasicSalary,
            PresentDays = slip.PresentDays,
            AbsentDays = slip.AbsentDays,
            LeaveDays = slip.LeaveDays,
            LwpDays = slip.LwpDays,
            PayableDays = slip.PayableDays,
            OtHours = slip.OtHours,
            OtAmount = slip.OtAmount,
            GrossEarning = slip.GrossEarning,
            TotalDeduction = slip.TotalDeduction,
            TaxableIncome = slip.TaxableIncome,
            EmployerContribution = slip.EmployerContribution,
            NetPay = slip.NetPay,
            PayMethod = slip.PayMethod,
            PaymentStatus = slip.PaymentStatus,
            CalculationSnapshot = slip.CalculationSnapshot,
            Lines = lines.Select(l => new Hrm1204PayslipLineDto
            {
                ComponentNo = l.ComponentNo,
                ComponentName = l.ComponentName ?? componentNames.GetValueOrDefault(l.ComponentNo, $"#{l.ComponentNo}"),
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
            }).ToList()
        };
    }
}
