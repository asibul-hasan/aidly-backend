using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Sys.Application.Services;

public interface ISys1005Service
{
    Task<List<Sys1005VatTaxDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1005VatTaxDto>> GetListByBranchAsync(long branchNo, CancellationToken cancellationToken = default);

    Task<Sys1005VatTaxDto> GetDetailAsync(long vatTaxNo, CancellationToken cancellationToken = default);

    Task<Sys1005VatTaxDto?> SaveAsync(Sys1005VatTaxDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(long vatTaxNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dedicated service for the SYS_1005 VAT / Tax Setup form.
///
/// <para>Owns all business rules for <c>sys_vat_tax</c>: company-scoped with optional branch
/// override, per-company tax-code uniqueness, tax-type and 0–100% rate validation, and the
/// effective-from / effective-to date window check. Every write runs in a single transaction.</para>
///
/// <para><b>All Branches pattern:</b> <c>branch_no = NULL</c> means company-wide (applies to all
/// branches). When saving, specific branches create one record per branch; "All Branches" creates
/// one record with <c>branch_no = NULL</c>.</para>
/// </summary>
public class Sys1005Service : ISys1005Service
{
    private const short Active = 1;
    private const short Deleted = 0;
    private const decimal MaxRate = 100m;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<Sys1005Service> _logger;

    public Sys1005Service(ISysDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                          ILogger<Sys1005Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    // ─── Reads ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Branch-scoped read: rows for the current login branch PLUS company-wide rows
    /// (<c>branch_no IS NULL</c>, which apply to every branch including future ones).
    /// </summary>
    public async Task<List<Sys1005VatTaxDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = Company();
        var branchNo = _ctx.BranchNo;

        var rows = await _db.VatTaxes
            .AsNoTracking()
            .Where(t => t.CompanyNo == companyNo && t.IsDeleted == Deleted
                        && (t.BranchNo == null || (branchNo != null && t.BranchNo == branchNo)))
            .OrderBy(t => t.VatTaxNo)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public Task<List<Sys1005VatTaxDto>> GetListByBranchAsync(long branchNo,
                                                             CancellationToken cancellationToken = default) =>
        GetListAsync(cancellationToken);

    public async Task<Sys1005VatTaxDto> GetDetailAsync(long vatTaxNo, CancellationToken cancellationToken = default) =>
        ToDto(await LoadLiveAsync(vatTaxNo, cancellationToken));

    // ─── Save (insert or update) ─────────────────────────────────────────────

    public Task<Sys1005VatTaxDto?> SaveAsync(Sys1005VatTaxDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct => dto.VatTaxNo != null
            ? await UpdateAsync(dto.VatTaxNo.Value, dto, ct)
            : await InsertAsync(dto, ct), cancellationToken);

    public Task DeleteAsync(long vatTaxNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadLiveAsync(vatTaxNo, ct);

            entity.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("VatTax soft-deleted: vatTaxNo={VatTaxNo}", vatTaxNo);
        }, cancellationToken);

    // ─── Private: write paths ────────────────────────────────────────────────

    /// <summary>
    /// Insert VAT/Tax. Supports multi-branch: if <c>branch_nos</c> has values, creates one record
    /// per branch; if empty/null, creates one company-wide record (<c>branch_no = NULL</c>).
    /// </summary>
    private async Task<Sys1005VatTaxDto?> InsertAsync(Sys1005VatTaxDto dto, CancellationToken ct)
    {
        var companyNo = Company();
        var taxCode = TrimRequired(dto.TaxCode, "Tax code").ToUpperInvariant();
        var taxName = TrimRequired(dto.TaxName, "Tax name");
        var taxType = ValidateTaxType(dto.TaxType);
        var rate = ValidateRate(dto.RatePercentage);

        ValidateEffectiveDates(dto.EffectiveFrom, dto.EffectiveTo);

        var branchNos = ResolveBranches(dto.BranchNos);
        VatTax? saved = null;

        foreach (var branchNo in branchNos)
        {
            // Per-(company, branch) uniqueness — the same code may exist in another branch.
            var duplicate = await _db.VatTaxes
                .AnyAsync(t => t.TaxCode == taxCode && t.CompanyNo == companyNo
                               && t.BranchNo == branchNo && t.IsDeleted == Deleted, ct);

            if (duplicate)
            {
                throw new ValidationException("Tax code already exists for this branch: " + taxCode);
            }

            var entity = new VatTax
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                TaxCode = taxCode,
                TaxName = taxName,
                TaxType = taxType,
                RatePercentage = rate,
                EffectiveFrom = dto.EffectiveFrom!.Value,
                EffectiveTo = dto.EffectiveTo,
                GlAccountNo = dto.GlAccountNo,
                AuthorityName = TrimToNull(dto.AuthorityName),
                Remarks = dto.Remarks,
                IsActive = NormalizeFlag(dto.IsActive, Active, "is_active")
            };

            _db.VatTaxes.Add(entity);
            await _db.SaveChangesAsync(ct);
            saved = entity;

            _logger.LogInformation("VatTax inserted: vatTaxNo={VatTaxNo}, taxCode={TaxCode}, branchNo={BranchNo}",
                entity.VatTaxNo, entity.TaxCode, branchNo);
        }

        return saved == null ? null : ToDto(saved);
    }

    private async Task<Sys1005VatTaxDto> UpdateAsync(long vatTaxNo, Sys1005VatTaxDto dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(vatTaxNo, ct);

        if (dto.TaxCode != null)
        {
            var taxCode = TrimRequired(dto.TaxCode, "Tax code").ToUpperInvariant();

            var other = await _db.VatTaxes
                .FirstOrDefaultAsync(t => t.TaxCode == taxCode && t.CompanyNo == entity.CompanyNo
                                          && t.IsDeleted == Deleted, ct);

            if (other != null && other.VatTaxNo != vatTaxNo)
            {
                throw new ValidationException("Tax code already in use in this company: " + taxCode);
            }

            entity.TaxCode = taxCode;
        }

        if (dto.TaxName != null) entity.TaxName = TrimRequired(dto.TaxName, "Tax name");
        if (dto.TaxType != null) entity.TaxType = ValidateTaxType(dto.TaxType);
        if (dto.RatePercentage != null) entity.RatePercentage = ValidateRate(dto.RatePercentage);

        var effectiveFrom = dto.EffectiveFrom ?? entity.EffectiveFrom;
        var effectiveTo = dto.EffectiveTo ?? entity.EffectiveTo;

        ValidateEffectiveDates(effectiveFrom, effectiveTo);

        entity.EffectiveFrom = effectiveFrom;
        entity.EffectiveTo = effectiveTo;

        if (dto.GlAccountNo != null) entity.GlAccountNo = dto.GlAccountNo;
        if (dto.AuthorityName != null) entity.AuthorityName = TrimToNull(dto.AuthorityName);
        if (dto.Remarks != null) entity.Remarks = dto.Remarks;
        if (dto.IsActive != null) entity.IsActive = NormalizeFlag(dto.IsActive, Active, "is_active");

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("VatTax updated: vatTaxNo={VatTaxNo}", entity.VatTaxNo);

        return ToDto(entity);
    }

    // ─── Private: validation helpers ─────────────────────────────────────────

    private async Task<VatTax> LoadLiveAsync(long vatTaxNo, CancellationToken ct)
    {
        var entity = await _db.VatTaxes
                         .FirstOrDefaultAsync(t => t.VatTaxNo == vatTaxNo && t.IsDeleted == Deleted, ct)
                     ?? throw new NotFoundException("VAT/Tax not found: vatTaxNo=" + vatTaxNo);

        if (entity.CompanyNo != Company())
        {
            throw new NotFoundException("VAT/Tax not found: vatTaxNo=" + vatTaxNo);
        }

        return entity;
    }

    private long Company() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    /// <summary>
    /// <c>branch_nos</c> from the DTO: null/empty = company-wide (NULL <c>branch_no</c>);
    /// otherwise one record per branch.
    /// </summary>
    private static List<long?> ResolveBranches(List<long>? branchNos) =>
        branchNos == null || branchNos.Count == 0
            ? new List<long?> { null }
            : branchNos.Select(b => (long?)b).ToList();

    private static short ValidateTaxType(short? taxType)
    {
        if (taxType == null)
        {
            throw new ValidationException("Tax type is required");
        }

        if (taxType < 1)
        {
            throw new ValidationException("Tax type is invalid");
        }

        return taxType.Value;
    }

    /// <summary>
    /// Enforces a non-null rate within the 0–100% range that <c>rate_percentage</c> represents.
    /// </summary>
    private static decimal ValidateRate(decimal? rate)
    {
        if (rate == null)
        {
            throw new ValidationException("Tax rate percentage is required");
        }

        if (rate < 0 || rate > MaxRate)
        {
            throw new ValidationException("Tax rate percentage must be between 0 and 100");
        }

        return rate.Value;
    }

    private static void ValidateEffectiveDates(DateOnly? effectiveFrom, DateOnly? effectiveTo)
    {
        if (effectiveFrom == null)
        {
            throw new ValidationException("Effective-from date is required");
        }

        if (effectiveTo != null && effectiveTo < effectiveFrom)
        {
            throw new ValidationException("Effective-to date cannot be before the effective-from date");
        }
    }

    private static short NormalizeFlag(short? value, short defaultValue, string fieldName)
    {
        if (value == null) return defaultValue;

        if (value != 0 && value != 1)
        {
            throw new ValidationException(fieldName + " must be 0 or 1");
        }

        return value.Value;
    }

    private static string TrimRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(fieldName + " is required");
        }

        return value.Trim();
    }

    private static string? TrimToNull(string? value)
    {
        if (value == null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    // ─── Private: mapping ────────────────────────────────────────────────────

    private static Sys1005VatTaxDto ToDto(VatTax e) => new()
    {
        VatTaxNo = e.VatTaxNo,
        CompanyNo = e.CompanyNo,
        TaxCode = e.TaxCode,
        TaxName = e.TaxName,
        TaxType = e.TaxType,
        RatePercentage = e.RatePercentage,
        EffectiveFrom = e.EffectiveFrom,
        EffectiveTo = e.EffectiveTo,
        GlAccountNo = e.GlAccountNo,
        AuthorityName = e.AuthorityName,
        BranchNo = e.BranchNo,
        Remarks = e.Remarks,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };
}
