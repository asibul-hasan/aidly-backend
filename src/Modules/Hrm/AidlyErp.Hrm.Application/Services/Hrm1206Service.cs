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

public interface IHrm1206Service
{
    Task<List<Hrm1206LoanDto>> GetListAsync(CancellationToken cancellationToken = default);
    Task<Hrm1206LoanDto> GetDetailAsync(long no, CancellationToken cancellationToken = default);
    Task<Hrm1206LoanDto> SaveAsync(Hrm1206LoanDto dto, CancellationToken cancellationToken = default);
    Task<Hrm1206LoanDto> SubmitAsync(long no, CancellationToken cancellationToken = default);
    Task<Hrm1206LoanDto> ApproveAsync(long no, CancellationToken cancellationToken = default);
    Task<Hrm1206LoanDto> RejectAsync(long no, CancellationToken cancellationToken = default);
    Task<Hrm1206LoanDto> DisburseAsync(long no, CancellationToken cancellationToken = default);
    Task<Hrm1206LoanDto> RecordRecoveryAsync(long no, decimal amount, CancellationToken cancellationToken = default);
    Task<Hrm1206LoanDto> CancelAsync(long no, CancellationToken cancellationToken = default);
    Task DeleteAsync(long no, CancellationToken cancellationToken = default);
}

public class Hrm1206Service : IHrm1206Service
{
    private readonly IHrmDbContext _db;
    private readonly ICompanyBranchContext _tenantContext;
    private readonly IApprovalService _approvalService;

    public const string DocType = "HRM_LOAN";
    private const short StDraft = 1, StApproved = 2, StDisbursed = 3, StClosed = 4, StCancelled = 5;

    public Hrm1206Service(IHrmDbContext db, ICompanyBranchContext tenantContext, IApprovalService approvalService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _approvalService = approvalService;
    }

    public async Task<List<Hrm1206LoanDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.HrmLoanAdvances
            .AsNoTracking()
            .Where(x => x.IsDeleted == 0)
            .OrderByDescending(x => x.LoanNo)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<Hrm1206LoanDto> GetDetailAsync(long no, CancellationToken cancellationToken = default)
    {
        var e = await LoadLiveAsync(no, cancellationToken);
        return ToDto(e);
    }

    public async Task<Hrm1206LoanDto> SaveAsync(Hrm1206LoanDto dto, CancellationToken cancellationToken = default)
    {
        return dto.LoanNo.HasValue && dto.LoanNo.Value > 0
            ? await UpdateAsync(dto.LoanNo.Value, dto, cancellationToken)
            : await InsertAsync(dto, cancellationToken);
    }

    private async Task<Hrm1206LoanDto> InsertAsync(Hrm1206LoanDto dto, CancellationToken cancellationToken)
    {
        long branchNo = ResolveBranch(dto.BranchNo);
        string code = TrimRequired(dto.LoanId, "Loan ID").ToUpperInvariant();
        await RequireEmployeeAsync(dto.EmployeeNo, cancellationToken);

        if (await _db.HrmLoanAdvances.AnyAsync(x => x.LoanId == code && x.BranchNo == branchNo && x.IsDeleted == 0, cancellationToken))
        {
            throw new ValidationException($"Loan ID already exists in this branch: {code}");
        }

        var e = new HrmLoanAdvance
        {
            BranchNo = branchNo,
            LoanId = code,
            EmployeeNo = dto.EmployeeNo,
            LoanType = dto.LoanType,
            PrincipalAmount = Positive(dto.PrincipalAmount, "Principal amount"),
            InstallmentCount = PositiveCount(dto.InstallmentCount),
            DisbursementDate = dto.DisbursementDate,
            RecoveryStart = dto.RecoveryStart,
            Reason = dto.Reason,
            Status = StDraft,
            RecoveredAmount = 0m,
            IsActive = NormalizeFlag(dto.IsActive, 1, "is_active"),
            IsDeleted = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _tenantContext.CurrentUserNo()
        };

        Recompute(e);

        _db.HrmLoanAdvances.Add(e);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(e);
    }

    private async Task<Hrm1206LoanDto> UpdateAsync(long no, Hrm1206LoanDto dto, CancellationToken cancellationToken)
    {
        var e = await LoadLiveAsync(no, cancellationToken);
        if (e.Status != StDraft)
        {
            throw new ValidationException("Only a Draft loan can be edited");
        }

        if (dto.LoanType.HasValue) e.LoanType = dto.LoanType;
        if (dto.PrincipalAmount > 0) e.PrincipalAmount = Positive(dto.PrincipalAmount, "Principal amount");
        if (dto.InstallmentCount > 0) e.InstallmentCount = PositiveCount(dto.InstallmentCount);
        if (dto.DisbursementDate.HasValue) e.DisbursementDate = dto.DisbursementDate;
        if (dto.RecoveryStart.HasValue) e.RecoveryStart = dto.RecoveryStart;
        if (dto.Reason != null) e.Reason = dto.Reason;
        e.IsActive = NormalizeFlag(dto.IsActive, 1, "is_active");
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = _tenantContext.CurrentUserNo();

        Recompute(e);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(e);
    }

    private static void Recompute(HrmLoanAdvance e)
    {
        decimal principal = e.PrincipalAmount;
        int count = e.InstallmentCount < 1 ? 1 : e.InstallmentCount;
        e.InstallmentAmount = Math.Round(principal / count, 4, MidpointRounding.AwayFromZero);
        e.OutstandingAmount = principal - e.RecoveredAmount;
    }

    public async Task<Hrm1206LoanDto> SubmitAsync(long no, CancellationToken cancellationToken = default)
    {
        var e = await LoadLiveAsync(no, cancellationToken);
        if (e.Status != StDraft)
        {
            throw new ValidationException("Only a Draft loan can be submitted");
        }

        var outcome = await _approvalService.RaiseAsync(DocType, e.LoanNo, $"LN-{e.LoanNo}", e.PrincipalAmount, cancellationToken);
        if (outcome.AutoApproved)
        {
            await ApplyApprovalOutcomeAsync(e, true, cancellationToken);
        }
        else
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ToDto(e);
    }

    public async Task<Hrm1206LoanDto> ApproveAsync(long no, CancellationToken cancellationToken = default)
    {
        var e = await LoadLiveAsync(no, cancellationToken);
        if (e.Status != StDraft)
        {
            throw new ValidationException("Only a submitted (Draft) loan can be approved");
        }

        await _approvalService.ActAsync(0, true, null, cancellationToken);
        await ApplyApprovalOutcomeAsync(e, true, cancellationToken);
        return ToDto(e);
    }

    public async Task<Hrm1206LoanDto> RejectAsync(long no, CancellationToken cancellationToken = default)
    {
        var e = await LoadLiveAsync(no, cancellationToken);
        await _approvalService.ActAsync(0, false, null, cancellationToken);
        await ApplyApprovalOutcomeAsync(e, false, cancellationToken);
        return ToDto(e);
    }

    private async Task ApplyApprovalOutcomeAsync(HrmLoanAdvance e, bool approved, CancellationToken cancellationToken)
    {
        if (approved)
        {
            e.Status = StApproved;
            e.ApprovedBy = _tenantContext.CurrentUserNo();
            e.ApprovedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Hrm1206LoanDto> DisburseAsync(long no, CancellationToken cancellationToken = default)
    {
        var e = await LoadLiveAsync(no, cancellationToken);
        if (e.Status != StApproved)
        {
            throw new ValidationException("Only an Approved loan can be disbursed");
        }

        e.Status = StDisbursed;
        e.DisbursementDate ??= DateTime.UtcNow;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = _tenantContext.CurrentUserNo();

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(e);
    }

    public async Task<Hrm1206LoanDto> RecordRecoveryAsync(long no, decimal amount, CancellationToken cancellationToken = default)
    {
        var e = await LoadLiveAsync(no, cancellationToken);
        if (e.Status != StDisbursed)
        {
            throw new ValidationException("Only a Disbursed loan can record recovery");
        }

        decimal amt = Positive(amount, "Recovery amount");
        decimal newRecovered = e.RecoveredAmount + amt;
        if (newRecovered > e.PrincipalAmount)
        {
            throw new ValidationException("Recovery exceeds the outstanding balance");
        }

        e.RecoveredAmount = newRecovered;
        Recompute(e);
        if (e.OutstandingAmount <= 0)
        {
            e.Status = StClosed;
        }

        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = _tenantContext.CurrentUserNo();

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(e);
    }

    public async Task<Hrm1206LoanDto> CancelAsync(long no, CancellationToken cancellationToken = default)
    {
        var e = await LoadLiveAsync(no, cancellationToken);
        if (e.Status == StClosed || e.Status == StCancelled)
        {
            throw new ValidationException("A closed/cancelled loan cannot be cancelled");
        }

        e.Status = StCancelled;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = _tenantContext.CurrentUserNo();

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(e);
    }

    public async Task DeleteAsync(long no, CancellationToken cancellationToken = default)
    {
        var e = await LoadLiveAsync(no, cancellationToken);
        if (e.Status != StDraft)
        {
            throw new ValidationException("Only a Draft loan can be deleted");
        }

        e.IsDeleted = 1;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = _tenantContext.CurrentUserNo();

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<HrmLoanAdvance> LoadLiveAsync(long no, CancellationToken cancellationToken)
    {
        return await _db.HrmLoanAdvances
            .FirstOrDefaultAsync(x => x.LoanNo == no && x.IsDeleted == 0, cancellationToken)
            ?? throw new NotFoundException($"Loan not found: no={no}");
    }

    private long ResolveBranch(long? branchNoFromDto)
    {
        long? branchNo = branchNoFromDto ?? _tenantContext.CurrentBranchNo();
        if (!branchNo.HasValue || branchNo.Value <= 0)
        {
            throw new ValidationException("Branch is required");
        }
        return branchNo.Value;
    }

    private async Task RequireEmployeeAsync(long employeeNo, CancellationToken cancellationToken)
    {
        if (employeeNo <= 0) throw new ValidationException("Employee is required");
        bool exists = await _db.HrmEmployees.AnyAsync(e => e.EmployeeNo == employeeNo && e.IsDeleted == 0, cancellationToken);
        if (!exists) throw new NotFoundException($"Employee not found: employeeNo={employeeNo}");
    }

    private static decimal Positive(decimal v, string field)
    {
        if (v <= 0) throw new ValidationException($"{field} must be greater than zero");
        return v;
    }

    private static int PositiveCount(int v)
    {
        if (v < 1) throw new ValidationException("Installment count must be at least 1");
        return v;
    }

    private static short NormalizeFlag(short value, short defaultValue, string fieldName)
    {
        if (value != 0 && value != 1) throw new ValidationException($"{fieldName} must be 0 or 1");
        return value;
    }

    private static string TrimRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{fieldName} is required");
        return value.Trim();
    }

    private static Hrm1206LoanDto ToDto(HrmLoanAdvance e) => new()
    {
        LoanNo = e.LoanNo,
        LoanId = e.LoanId,
        EmployeeNo = e.EmployeeNo,
        LoanType = e.LoanType,
        PrincipalAmount = e.PrincipalAmount,
        InstallmentCount = e.InstallmentCount,
        InstallmentAmount = e.InstallmentAmount,
        RecoveredAmount = e.RecoveredAmount,
        OutstandingAmount = e.OutstandingAmount,
        DisbursementDate = e.DisbursementDate,
        RecoveryStart = e.RecoveryStart,
        Reason = e.Reason,
        Status = e.Status,
        ApprovedBy = e.ApprovedBy,
        ApprovedAt = e.ApprovedAt,
        BranchNo = e.BranchNo,
        IsActive = e.IsActive ?? 0
    };
}
