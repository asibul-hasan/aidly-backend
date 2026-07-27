using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Exceptions;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Hrm.Dto;

namespace AidlyErp.Application.Hrm.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Hrm1203Service — Salary Sheet (read-only)
// Full port of Java Hrm1203Service (79 lines). Consolidated salary sheet
// for a chosen payroll run. Reads immutable payslip rows from HRM_1202.
// ═══════════════════════════════════════════════════════════════════════════

public interface IHrm1203Service
{
    Task<List<Hrm1203RunLiteDto>> GetRunsAsync(CancellationToken ct = default);
    Task<List<Hrm1203SheetRowDto>> GetSheetAsync(long runNo, CancellationToken ct = default);
}

public class Hrm1203Service : IHrm1203Service
{
    private readonly IApplicationDbContext _db;
    public Hrm1203Service(IApplicationDbContext db) => _db = db;

    public async Task<List<Hrm1203RunLiteDto>> GetRunsAsync(CancellationToken ct = default) =>
        await _db.HrmPayrollRuns.AsNoTracking()
            .Where(r => r.IsDeleted == 0)
            .OrderByDescending(r => r.PayrollRunNo)
            .Select(r => new Hrm1203RunLiteDto
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

    public async Task<List<Hrm1203SheetRowDto>> GetSheetAsync(long runNo, CancellationToken ct = default)
    {
        var run = await _db.HrmPayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PayrollRunNo == runNo && r.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Payroll run not found: runNo={runNo}");

        return await _db.HrmPayslips.AsNoTracking()
            .Where(p => p.PayrollRunNo == run.PayrollRunNo && p.IsDeleted == 0)
            .OrderBy(p => p.PayslipNo)
            .Select(s => new Hrm1203SheetRowDto
            {
                PayslipNo = s.PayslipNo,
                EmployeeNo = s.EmployeeNo,
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
                PaymentStatus = s.PaymentStatus
            })
            .ToListAsync(ct);
    }
}
