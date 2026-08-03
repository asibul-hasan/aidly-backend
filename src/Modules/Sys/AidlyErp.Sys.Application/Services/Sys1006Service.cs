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

public interface ISys1006Service
{
    Task<List<SysLookupDto>> GetCurrencyOptionsAsync(CancellationToken cancellationToken = default);

    Task<List<Sys1006ExchangeRateDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<Sys1006ExchangeRateDto> SaveAsync(Sys1006ExchangeRateDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(long exchangeRateNo, CancellationToken cancellationToken = default);
}

/// <summary>SYS1006 Exchange Rate Setup — historical FX per company/currency/date.</summary>
public class Sys1006Service : ISys1006Service
{
    private const short Deleted = 0;
    private const short Active = 1;

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<Sys1006Service> _logger;

    public Sys1006Service(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx,
                          ILogger<Sys1006Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    // ─── Reads ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Scoped to the current branch — only currencies set up in this branch are selectable.
    /// </summary>
    public async Task<List<SysLookupDto>> GetCurrencyOptionsAsync(CancellationToken cancellationToken = default)
    {
        var branchNo = Branch();

        return await _db.Currencies
            .AsNoTracking()
            .Where(c => c.BranchNo == branchNo && c.IsDeleted == Deleted)
            .OrderBy(c => c.CurrencyNo)
            .Select(c => new SysLookupDto(c.CurrencyNo, c.CurrencyCode + " - " + c.CurrencyName))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Rate rows for the active company with the currency label resolved in the same query.
    /// The Java version uses a native LEFT JOIN for exactly this reason — resolving the label
    /// separately produced nulls when the currency sat outside the caller's branch scope.
    /// </summary>
    public async Task<List<Sys1006ExchangeRateDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var companyNo = Company();

        return await (from r in _db.ExchangeRates.AsNoTracking()
                      where r.CompanyNo == companyNo && r.IsDeleted == Deleted
                      join c in _db.Currencies.AsNoTracking().IgnoreQueryFilters()
                          on r.CurrencyNo equals c.CurrencyNo into currencies
                      from c in currencies.DefaultIfEmpty()
                      orderby r.RateDate descending, r.ExchangeRateNo descending
                      select new Sys1006ExchangeRateDto
                      {
                          ExchangeRateNo = r.ExchangeRateNo,
                          CurrencyNo = r.CurrencyNo,
                          CurrencyLabel = c == null ? null : c.CurrencyCode + " - " + c.CurrencyName,
                          RateDate = r.RateDate,
                          Rate = r.Rate,
                          IsActive = r.IsActive,
                          RowVersion = r.RowVersion
                      })
            .ToListAsync(cancellationToken);
    }

    // ─── Writes ──────────────────────────────────────────────────────────────

    public Task<Sys1006ExchangeRateDto> SaveAsync(Sys1006ExchangeRateDto dto,
                                                  CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            // Guard: the selected currency must be alive (not soft-deleted).
            var currency = await LoadLiveCurrencyAsync(dto.CurrencyNo, ct);

            ExchangeRate e;

            if (dto.ExchangeRateNo != null)
            {
                e = await _db.ExchangeRates
                        .FirstOrDefaultAsync(r => r.ExchangeRateNo == dto.ExchangeRateNo && r.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Exchange rate not found");
            }
            else
            {
                e = new ExchangeRate
                {
                    CompanyNo = Company(),
                    CreatedBy = _ctx.CurrentUserNo()
                };
                _db.ExchangeRates.Add(e);
            }

            e.CurrencyNo = dto.CurrencyNo!.Value;
            e.RateDate = dto.RateDate!.Value;
            e.Rate = dto.Rate!.Value;
            e.IsActive = dto.IsActive ?? Active;

            await _db.SaveChangesAsync(ct);

            await SyncCurrencyMasterRateAsync(e.CurrencyNo, ct);

            return ToDto(e, currency);
        }, cancellationToken);

    public Task DeleteAsync(long exchangeRateNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var e = await _db.ExchangeRates
                        .FirstOrDefaultAsync(r => r.ExchangeRateNo == exchangeRateNo && r.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("Exchange rate not found");

            var currencyNo = e.CurrencyNo;

            // Java sets the flags directly here rather than calling performSoftDelete, so the
            // deleted_by/at columns are intentionally left untouched.
            e.IsDeleted = 1;
            e.IsActive = 0;

            await _db.SaveChangesAsync(ct);
            await SyncCurrencyMasterRateAsync(currencyNo, ct);
        }, cancellationToken);

    // ─── Private ─────────────────────────────────────────────────────────────

    private long Company() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    private long Branch() =>
        _ctx.BranchNo ?? throw new ValidationException("No active branch in context");

    private async Task<Currency> LoadLiveCurrencyAsync(long? currencyNo, CancellationToken ct)
    {
        var currency = await _db.Currencies
                           .FirstOrDefaultAsync(c => c.CurrencyNo == currencyNo && c.IsDeleted == Deleted, ct)
                       ?? throw new ValidationException(
                           "Currency not found or has been deleted. Please select an active currency.");

        if (currency.CompanyNo != Company())
        {
            throw new ValidationException("Currency does not belong to the active company.");
        }

        return currency;
    }

    /// <summary>
    /// Pushes the newest active rate onto <c>sys_currency.exchange_rate</c> so the master row
    /// always reflects the current FX. Falls back to 1 when no active rate remains.
    /// </summary>
    private async Task SyncCurrencyMasterRateAsync(long currencyNo, CancellationToken ct)
    {
        var currency = await LoadLiveCurrencyAsync(currencyNo, ct);

        var latestRate = await _db.ExchangeRates
            .AsNoTracking()
            .Where(r => r.CompanyNo == currency.CompanyNo && r.CurrencyNo == currencyNo
                        && r.IsDeleted == Deleted && r.IsActive == Active)
            .OrderByDescending(r => r.RateDate)
            .ThenByDescending(r => r.ExchangeRateNo)
            .Select(r => (decimal?)r.Rate)
            .FirstOrDefaultAsync(ct) ?? 1m;

        currency.ExchangeRate = latestRate;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("SYS1006: synced currency {CurrencyNo} master exchange_rate to {Rate}",
            currencyNo, latestRate);
    }

    private static Sys1006ExchangeRateDto ToDto(ExchangeRate e, Currency? currency) => new()
    {
        ExchangeRateNo = e.ExchangeRateNo,
        CurrencyNo = e.CurrencyNo,
        CurrencyLabel = currency == null ? null : currency.CurrencyCode + " - " + currency.CurrencyName,
        RateDate = e.RateDate,
        Rate = e.Rate,
        IsActive = e.IsActive,
        RowVersion = e.RowVersion
    };
}
