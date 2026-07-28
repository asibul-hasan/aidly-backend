using System.Text.RegularExpressions;
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

public interface ISys1004Service
{
    Task<List<Sys1004CurrencyDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1004CurrencyDto>> GetListByBranchAsync(long branchNo, CancellationToken cancellationToken = default);

    Task<List<CurrencyLookupDto>> GetCurrencyLookupListAsync(CancellationToken cancellationToken = default);

    Task<Sys1004CurrencyDto> GetDetailAsync(long currencyNo, CancellationToken cancellationToken = default);

    Task<List<ExchangeRate>> GetRateHistoryAsync(long currencyNo, CancellationToken cancellationToken = default);

    Task<BaseCurrencySettingsDto?> GetBaseSettingsAsync(CancellationToken cancellationToken = default);

    Task<Sys1004CurrencyDto?> SaveAsync(Sys1004CurrencyDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(long currencyNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dedicated service for the SYS_1004 Currency Setup form.
///
/// <para>Owns all business rules for <c>sys_currency</c>: branch resolution, ISO-4217
/// currency-code format and per-branch uniqueness, exchange-rate / decimal-place / number-system
/// validation, and the single-base-currency-per-branch rule. Every write runs in a single
/// transaction.</para>
/// </summary>
public class Sys1004Service : ISys1004Service
{
    private const short Active = 1;
    private const short Deleted = 0;
    private const short Base = 1;

    private static readonly Regex Iso4217 = new("^[A-Z]{3}$", RegexOptions.Compiled);

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<Sys1004Service> _logger;

    public Sys1004Service(ISysDbContext db, IUnitOfWork unitOfWork, ICompanyBranchContext ctx,
                          ILogger<Sys1004Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    // ─── Reads ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Branch-scoped read: rows for the current login branch PLUS company-wide rows
    /// (<c>branch_no IS NULL</c>). Company-wide rows apply to every branch, including branches
    /// created in the future.
    /// </summary>
    public async Task<List<Sys1004CurrencyDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = Company();
        var branchNo = _ctx.BranchNo;

        var rows = await _db.Currencies
            .AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.IsDeleted == Deleted
                        && (c.BranchNo == null || (branchNo != null && c.BranchNo == branchNo)))
            .OrderBy(c => c.CurrencyNo)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public Task<List<Sys1004CurrencyDto>> GetListByBranchAsync(long branchNo,
                                                               CancellationToken cancellationToken = default) =>
        GetListAsync(cancellationToken);

    public async Task<List<CurrencyLookupDto>> GetCurrencyLookupListAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = Company();

        return await _db.Currencies
            .AsNoTracking()
            .Where(c => c.CompanyNo == companyNo && c.IsDeleted == Deleted)
            .OrderBy(c => c.CurrencyNo)
            .Select(e => new CurrencyLookupDto
            {
                CurrencyNo = e.CurrencyNo,
                CurrencyCode = e.CurrencyCode,
                CurrencyName = e.CurrencyName,
                CurrencySymbol = e.CurrencySymbol,
                FractionName = e.FractionName,
                DecimalPlaces = e.DecimalPlaces,
                NumberSystem = e.NumberSystem,
                ExchangeRate = e.ExchangeRate,
                IsBaseCurrency = e.IsBaseCurrency
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Sys1004CurrencyDto> GetDetailAsync(long currencyNo, CancellationToken cancellationToken = default) =>
        ToDto(await LoadLiveAsync(currencyNo, cancellationToken));

    public async Task<List<ExchangeRate>> GetRateHistoryAsync(long currencyNo,
                                                              CancellationToken cancellationToken = default)
    {
        var companyNo = _ctx.CompanyNo;

        return await _db.ExchangeRates
            .AsNoTracking()
            .Where(r => r.CompanyNo == companyNo && r.CurrencyNo == currencyNo && r.IsDeleted == Deleted)
            .OrderByDescending(r => r.RateDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Base-currency formatting settings for the authenticated user's branch. Returns
    /// <c>null</c> when no base currency has been configured yet — the frontend must fall back to
    /// its built-in defaults.
    /// </summary>
    public async Task<BaseCurrencySettingsDto?> GetBaseSettingsAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = Company();
        var branchNo = _ctx.BranchNo;

        var entity = branchNo != null
            ? await _db.Currencies.AsNoTracking().FirstOrDefaultAsync(
                c => c.CompanyNo == companyNo && c.BranchNo == branchNo
                     && c.IsBaseCurrency == Base && c.IsDeleted == Deleted, cancellationToken)
            : await _db.Currencies.AsNoTracking().FirstOrDefaultAsync(
                c => c.CompanyNo == companyNo && c.BranchNo == null
                     && c.IsBaseCurrency == Base && c.IsDeleted == Deleted, cancellationToken);

        return entity == null ? null : ToBaseSettingsDto(entity);
    }

    // ─── Save (insert or update) ─────────────────────────────────────────────

    public Task<Sys1004CurrencyDto?> SaveAsync(Sys1004CurrencyDto dto, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct => dto.CurrencyNo != null
            ? await UpdateAsync(dto.CurrencyNo.Value, dto, ct)
            : await InsertAsync(dto, ct), cancellationToken);

    public Task DeleteAsync(long currencyNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var entity = await LoadLiveAsync(currencyNo, ct);

            if (entity.IsBaseCurrency == Base)
            {
                throw new ValidationException("The base currency cannot be deleted: " + entity.CurrencyCode);
            }

            entity.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Currency soft-deleted: currencyNo={CurrencyNo}", currencyNo);
        }, cancellationToken);

    // ─── Private: write paths ────────────────────────────────────────────────

    /// <summary>
    /// Insert currency. Supports multi-branch: if <c>branch_nos</c> has values, creates one record
    /// per branch; if empty/null, creates one company-wide record (<c>branch_no = NULL</c>).
    /// </summary>
    private async Task<Sys1004CurrencyDto?> InsertAsync(Sys1004CurrencyDto dto, CancellationToken ct)
    {
        var companyNo = Company();
        var currencyCode = ValidateCurrencyCode(dto.CurrencyCode);
        var currencyName = TrimRequired(dto.CurrencyName, "Currency name");
        var isBaseCurrency = NormalizeFlag(dto.IsBaseCurrency, 0, "is_base_currency");

        var rate = dto.ExchangeRate is > 0 ? dto.ExchangeRate.Value : 1m;
        var branchNos = ResolveBranches(dto.BranchNos);

        Currency? saved = null;

        foreach (var branchNo in branchNos)
        {
            // Per-(company, branch) uniqueness — the same code may exist in another branch.
            var duplicate = await _db.Currencies
                .AnyAsync(c => c.CurrencyCode == currencyCode && c.CompanyNo == companyNo
                               && c.BranchNo == branchNo && c.IsDeleted == Deleted, ct);

            if (duplicate)
            {
                throw new ValidationException("Currency code already exists for this branch: " + currencyCode);
            }

            if (branchNo != null)
            {
                await ValidateSingleBaseCurrencyAsync(branchNo, isBaseCurrency, null, ct);
            }

            var entity = new Currency
            {
                CompanyNo = companyNo,
                BranchNo = branchNo,
                CurrencyCode = currencyCode,
                CurrencyName = currencyName,
                CurrencySymbol = TrimToNull(dto.CurrencySymbol),
                FractionName = TrimToNull(dto.FractionName),
                DecimalPlaces = ValidateDecimalPlaces(dto.DecimalPlaces),
                NumberSystem = NormalizeFlag(dto.NumberSystem, 1, "number_system"),
                ExchangeRate = rate,
                IsBaseCurrency = isBaseCurrency,
                Remarks = dto.Remarks,
                IsActive = NormalizeFlag(dto.IsActive, Active, "is_active")
            };

            _db.Currencies.Add(entity);
            await _db.SaveChangesAsync(ct);
            saved = entity;

            _logger.LogInformation("Currency inserted: currencyNo={CurrencyNo}, currencyCode={Code}, branchNo={BranchNo}",
                entity.CurrencyNo, entity.CurrencyCode, branchNo);

            _db.ExchangeRates.Add(new ExchangeRate
            {
                CompanyNo = companyNo,
                CurrencyNo = entity.CurrencyNo,
                RateDate = DateOnly.FromDateTime(DateTime.Today),
                Rate = rate,
                CreatedBy = _ctx.CurrentUserNo()
            });

            await _db.SaveChangesAsync(ct);
        }

        return saved == null ? null : ToDto(saved);
    }

    private async Task<Sys1004CurrencyDto> UpdateAsync(long currencyNo, Sys1004CurrencyDto dto, CancellationToken ct)
    {
        var entity = await LoadLiveAsync(currencyNo, ct);

        if (dto.CurrencyCode != null)
        {
            var currencyCode = ValidateCurrencyCode(dto.CurrencyCode);

            var other = await _db.Currencies
                .FirstOrDefaultAsync(c => c.CurrencyCode == currencyCode && c.CompanyNo == entity.CompanyNo
                                          && c.IsDeleted == Deleted, ct);

            if (other != null && other.CurrencyNo != currencyNo)
            {
                throw new ValidationException("Currency code already in use in this company: " + currencyCode);
            }

            entity.CurrencyCode = currencyCode;
        }

        if (dto.CurrencyName != null) entity.CurrencyName = TrimRequired(dto.CurrencyName, "Currency name");
        if (dto.CurrencySymbol != null) entity.CurrencySymbol = TrimToNull(dto.CurrencySymbol);
        if (dto.FractionName != null) entity.FractionName = TrimToNull(dto.FractionName);
        if (dto.DecimalPlaces != null) entity.DecimalPlaces = ValidateDecimalPlaces(dto.DecimalPlaces);
        if (dto.NumberSystem != null) entity.NumberSystem = NormalizeFlag(dto.NumberSystem, 1, "number_system");
        if (dto.Remarks != null) entity.Remarks = dto.Remarks;
        if (dto.IsActive != null) entity.IsActive = NormalizeFlag(dto.IsActive, Active, "is_active");

        if (dto.IsBaseCurrency != null)
        {
            var isBaseCurrency = NormalizeFlag(dto.IsBaseCurrency, 0, "is_base_currency");
            await ValidateSingleBaseCurrencyAsync(entity.BranchNo, isBaseCurrency, currencyNo, ct);
            entity.IsBaseCurrency = isBaseCurrency;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Currency updated: currencyNo={CurrencyNo}", entity.CurrencyNo);

        return ToDto(entity);
    }

    // ─── Private: validation helpers ─────────────────────────────────────────

    private async Task<Currency> LoadLiveAsync(long currencyNo, CancellationToken ct)
    {
        var entity = await _db.Currencies
                         .FirstOrDefaultAsync(c => c.CurrencyNo == currencyNo && c.IsDeleted == Deleted, ct)
                     ?? throw new NotFoundException("Currency not found: currencyNo=" + currencyNo);

        if (entity.CompanyNo != Company())
        {
            throw new NotFoundException("Currency not found: currencyNo=" + currencyNo);
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

    /// <summary>Normalises a currency code to upper-case and enforces the 3-letter ISO 4217 shape.</summary>
    private static string ValidateCurrencyCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException("Currency code is required");
        }

        var normalized = code.Trim().ToUpperInvariant();

        if (!Iso4217.IsMatch(normalized))
        {
            throw new ValidationException("Currency code must be exactly 3 letters (ISO 4217 format), e.g. USD");
        }

        return normalized;
    }

    /// <summary>Enforces at most one base currency per branch.</summary>
    private async Task ValidateSingleBaseCurrencyAsync(long? branchNo, short isBaseCurrency, long? excludeCurrencyNo,
                                                       CancellationToken ct)
    {
        if (isBaseCurrency != Base)
        {
            return;
        }

        var other = await _db.Currencies
            .FirstOrDefaultAsync(c => c.BranchNo == branchNo && c.IsBaseCurrency == Base
                                      && c.IsDeleted == Deleted, ct);

        if (other != null && (excludeCurrencyNo == null || other.CurrencyNo != excludeCurrencyNo))
        {
            throw new ValidationException("A base currency already exists for this branch: " + other.CurrencyCode);
        }
    }

    private static short ValidateDecimalPlaces(short? value)
    {
        var places = value ?? 2;

        if (places < 0 || places > 6)
        {
            throw new ValidationException("Decimal places must be between 0 and 6");
        }

        return places;
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

    private static BaseCurrencySettingsDto ToBaseSettingsDto(Currency e) => new()
    {
        CurrencyCode = e.CurrencyCode,
        CurrencyName = e.CurrencyName,
        CurrencySymbol = e.CurrencySymbol,
        // number_system: 1 = International (1,000,000), 0 = Indian (10,00,000)
        NumberFormat = e.NumberSystem == 1 ? "International" : "Indian",
        DecimalPlaces = e.DecimalPlaces
    };

    private static Sys1004CurrencyDto ToDto(Currency e) => new()
    {
        CurrencyNo = e.CurrencyNo,
        CompanyNo = e.CompanyNo,
        CurrencyCode = e.CurrencyCode,
        CurrencyName = e.CurrencyName,
        CurrencySymbol = e.CurrencySymbol,
        FractionName = e.FractionName,
        DecimalPlaces = e.DecimalPlaces,
        NumberSystem = e.NumberSystem,
        ExchangeRate = e.ExchangeRate,
        IsBaseCurrency = e.IsBaseCurrency,
        BranchNo = e.BranchNo,
        Remarks = e.Remarks,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };
}
