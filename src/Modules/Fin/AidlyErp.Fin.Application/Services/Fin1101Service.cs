using AidlyErp.Fin.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Domain;

namespace AidlyErp.Fin.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Fin1101Service — Double-Entry Voucher Engine
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1101Service
{
    Task<List<Fin1101VoucherDto>> GetListAsync(long? typeNo, DateTime? fromDate, DateTime? toDate, string? search, short? status = null, CancellationToken ct = default);
    Task<Fin1101VoucherDto> GetDetailAsync(long voucherNo, CancellationToken ct = default);
    Task<Dictionary<string, object>> GetLookupsAsync(CancellationToken ct = default);
    Task<Fin1101VoucherDto> SaveAsync(Fin1101VoucherDto dto, CancellationToken ct = default);
    Task<Fin1101VoucherDto> SubmitAsync(long voucherNo, CancellationToken ct = default);
    Task<Fin1101VoucherDto> RejectAsync(long voucherNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long voucherNo, bool approved, CancellationToken ct = default);
    Task<Fin1101VoucherDto> CancelAsync(long voucherNo, CancellationToken ct = default);
    Task DeleteAsync(long voucherNo, CancellationToken ct = default);
    Task<bool> AlreadyPostedAsync(string sourceDocType, long sourceDocNo, long companyNo, CancellationToken ct = default);
    Task<long> ResolveSystemJournalTypeAsync(long companyNo, CancellationToken ct = default);
    Task<long> ResolveSystemVoucherTypeAsync(long companyNo, short preferredKind, CancellationToken ct = default);
    Task<long> PostSystemVoucherAsync(long companyNo, long branchNo, long voucherTypeNo, DateTime voucherDate, string narration, short sourceModule, string sourceDocType, long sourceDocNo, List<Fin1101VoucherLineDto> lines, CancellationToken ct = default);
}

public class Fin1101Service : IFin1101Service
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IFinCalendar _calendar;
    private readonly IApprovalService _approvalService;
    private readonly IUnitOfWork<IFinDbContext> _uow;
    private readonly IDocSequenceGenerator _docSeq;
    private readonly ICurrencyLookup _currencyLookup;

    public const string DocType = "FIN_VOUCHER";
    private const short StDraft = 1, StPosted = 2, StCancelled = 3;

    public Fin1101Service(IFinDbContext db, ICompanyBranchContext ctx, IApprovalService approvalService, IFinCalendar calendar, IUnitOfWork<IFinDbContext> uow, IDocSequenceGenerator docSeq, ICurrencyLookup currencyLookup)
    {
        _db = db;
        _ctx = ctx;
        _approvalService = approvalService;
        _calendar = calendar;
        _uow = uow;
        _docSeq = docSeq;
        _currencyLookup = currencyLookup;
    }

    public async Task<List<Fin1101VoucherDto>> GetListAsync(long? typeNo, DateTime? fromDate, DateTime? toDate, string? search, short? status = null, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var query = _db.FinVouchers.AsNoTracking().Where(v => v.CompanyNo == companyNo && v.IsDeleted == 0);

        if (typeNo.HasValue && typeNo.Value > 0) query = query.Where(v => v.VoucherTypeNo == typeNo.Value);
        if (fromDate.HasValue) query = query.Where(v => v.VoucherDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(v => v.VoucherDate <= toDate.Value);
        if (status.HasValue) query = query.Where(v => v.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim().ToLowerInvariant();
            query = query.Where(v => v.VoucherId.ToLower().Contains(s) || (v.Narration != null && v.Narration.ToLower().Contains(s)));
        }

        var types = await _db.FinVoucherTypes.AsNoTracking().Where(t => t.CompanyNo == companyNo && t.IsDeleted == 0).ToDictionaryAsync(t => t.VoucherTypeNo, ct);

        var list = await query.OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.VoucherNo).ToListAsync(ct);

        return list.Select(v =>
        {
            types.TryGetValue(v.VoucherTypeNo, out var vType);
            return ToDto(v, vType?.VoucherTypeName, vType?.BaseKind, null);
        }).ToList();
    }

    public async Task<Fin1101VoucherDto> GetDetailAsync(long voucherNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var v = await _db.FinVouchers.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

        var lines = await GetLinesAsync(voucherNo, ct);
        var vType = await _db.FinVoucherTypes.AsNoTracking().FirstOrDefaultAsync(t => t.VoucherTypeNo == v.VoucherTypeNo, ct);

        return ToDto(v, vType?.VoucherTypeName, vType?.BaseKind, lines);
    }

    public async Task<Dictionary<string, object>> GetLookupsAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;

        var types = await _db.FinVoucherTypes.AsNoTracking()
            .Where(t => t.CompanyNo == companyNo && t.IsDeleted == 0)
            .OrderBy(t => t.OrderSl).ThenBy(t => t.VoucherTypeNo)
            .Select(t => new Fin1003VoucherTypeDto
            {
                VoucherTypeNo = t.VoucherTypeNo,
                VoucherTypeCode = t.VoucherTypeCode,
                VoucherTypeName = t.VoucherTypeName,
                BaseKind = t.BaseKind,
                Prefix = t.Prefix,
                IsSystem = t.IsSystem
            })
            .ToListAsync(ct);

        var accounts = await _db.FinAccounts.AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsPostable == 1 && a.IsDeleted == 0)
            .OrderBy(a => a.AccountCode)
            .Select(a => new Fin1001AccountDto
            {
                AccountNo = a.AccountNo,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                RootType = a.RootType,
                NormalBalance = a.NormalBalance
            })
            .ToListAsync(ct);

        return new Dictionary<string, object>
        {
            ["voucherTypes"] = types,
            ["accounts"] = accounts
        };
    }

    public async Task<Fin1101VoucherDto> SaveAsync(Fin1101VoucherDto dto, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token =>
        {
            return dto.VoucherNo.HasValue && dto.VoucherNo.Value > 0
                ? await UpdateAsync(dto.VoucherNo.Value, dto, token)
                : await InsertAsync(dto, token);
        }, ct);
    }

    private async Task<Fin1101VoucherDto> InsertAsync(Fin1101VoucherDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = dto.BranchNo ?? _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");
        return await InsertInternalAsync(companyNo, branchNo, dto, ct);
    }

    private async Task<Fin1101VoucherDto> InsertInternalAsync(long companyNo, long branchNo, Fin1101VoucherDto dto, CancellationToken ct)
    {
        var type = await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.VoucherTypeNo == dto.VoucherTypeNo && t.CompanyNo == companyNo && t.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher type not found: {dto.VoucherTypeNo}");

        if (type.IsActive != 1) throw new ValidationException("Voucher type is inactive");

        await ValidateLinesAsync(dto.Lines, companyNo, ct);

        // sys_fin_year(_dtl).start_date/end_date are DATE columns (DateOnly).
        var voucherDay = DateOnly.FromDateTime(dto.VoucherDate);

        var finYear = await _calendar.FindYearForDateAsync(companyNo, voucherDay, ct)
            ?? throw new ValidationException($"No financial year configured for date {dto.VoucherDate:yyyy-MM-dd}");

        var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, voucherDay, ct)
            ?? throw new ValidationException($"No financial period configured for date {dto.VoucherDate:yyyy-MM-dd}");

        // Currency guard — reject non-base currency until multi-currency is fully supported
        if (dto.CurrencyNo.HasValue)
        {
            long baseCurrencyNo = await _currencyLookup.GetBaseCurrencyNoAsync(companyNo, ct);
            if (baseCurrencyNo == 0)
                throw new ValidationException("No base currency configured for this company");
            if (dto.CurrencyNo.Value != baseCurrencyNo)
                throw new ValidationException("Multi-currency vouchers not supported yet");
        }

        // Currency guard — reject non-unit FX rate
        if (dto.FxRate > 0 && dto.FxRate != 1m)
            throw new ValidationException("Multi-currency vouchers not supported yet");

        decimal totalDebit = dto.Lines.Sum(l => l.Debit);
        decimal totalCredit = dto.Lines.Sum(l => l.Credit);

        // The number is drawn BEFORE the insert, not stamped over a "TEMP" placeholder afterwards.
        // The old order meant a failure between the two saves left a row whose voucher_id was
        // literally "TEMP" — and because the source-doc unique index had already accepted it, every
        // later retry of that document collided with the wreckage and could never succeed.
        string prefix = (type.Prefix ?? "VCH").Trim().ToUpperInvariant();
        string voucherId = await _docSeq.NextAsync(companyNo, branchNo, "FIN_VOUCHER", prefix, 6, ct);

        var v = new FinVoucher
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            VoucherId = voucherId,
            VoucherTypeNo = dto.VoucherTypeNo,
            VoucherDate = dto.VoucherDate,
            FinYearNo = finYear.FinYearNo,
            FinPeriodNo = finPeriod.FinPeriodNo,
            Narration = dto.Narration,
            ReferenceNo = dto.ReferenceNo,
            CurrencyNo = dto.CurrencyNo,
            FxRate = dto.FxRate > 0 ? dto.FxRate : 1.0m,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            SourceModule = dto.SourceModule,
            SourceDocType = dto.SourceDocType,
            SourceDocNo = dto.SourceDocNo,
            Status = StDraft,
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };

        // A voucher is its header AND its lines: a header alone is not a smaller voucher, it is a
        // corrupt one. The header still has to be saved first so the lines have a voucher_no to
        // point at, so the two saves are held inside one transaction and roll back together.
        // Nested inside an ambient transaction (the posting engine opens one), this is a no-op and
        // the outer transaction stays in charge.
        // IsRelational guards the in-memory provider the unit tests use, which throws rather than
        // ignoring BeginTransaction. Against Postgres this always opens.
        var tx = _db.Database.CurrentTransaction is null && _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            _db.FinVouchers.Add(v);
            await _db.SaveChangesAsync(ct);

            await ReplaceLinesInternalAsync(v, dto.Lines, ct);
            await _db.SaveChangesAsync(ct);

            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        return await GetDetailAsync(v.VoucherNo, ct);
    }

    private async Task<Fin1101VoucherDto> UpdateAsync(long voucherNo, Fin1101VoucherDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

        if (v.Status != StDraft) throw new ValidationException("Only a Draft voucher can be edited");

        // Allow date change — must re-resolve fiscal year/period
        if (dto.VoucherDate != default && dto.VoucherDate != v.VoucherDate)
        {
            var voucherDay = DateOnly.FromDateTime(dto.VoucherDate);
            var finYear = await _calendar.FindYearForDateAsync(companyNo, voucherDay, ct)
                ?? throw new ValidationException($"No financial year configured for date {dto.VoucherDate:yyyy-MM-dd}");
            var finPeriod = await _calendar.FindPeriodForDateAsync(finYear.FinYearNo, voucherDay, ct)
                ?? throw new ValidationException($"No financial period configured for date {dto.VoucherDate:yyyy-MM-dd}");
            v.VoucherDate = dto.VoucherDate;
            v.FinYearNo = finYear.FinYearNo;
            v.FinPeriodNo = finPeriod.FinPeriodNo;
        }

        // Allow type change — re-validate against the new type's rules
        if (dto.VoucherTypeNo > 0 && dto.VoucherTypeNo != v.VoucherTypeNo)
        {
            var type = await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.VoucherTypeNo == dto.VoucherTypeNo && t.CompanyNo == companyNo && t.IsDeleted == 0, ct)
                ?? throw new NotFoundException($"Voucher type not found: {dto.VoucherTypeNo}");
            if (type.IsActive != 1) throw new ValidationException("Voucher type is inactive");
            v.VoucherTypeNo = dto.VoucherTypeNo;
        }

        if (dto.Lines != null && dto.Lines.Count > 0)
        {
            await ValidateLinesAsync(dto.Lines, companyNo, ct);
            v.TotalDebit = dto.Lines.Sum(l => l.Debit);
            v.TotalCredit = dto.Lines.Sum(l => l.Credit);
            await ReplaceLinesInternalAsync(v, dto.Lines, ct);
        }

        if (dto.Narration != null) v.Narration = dto.Narration;
        if (dto.ReferenceNo != null) v.ReferenceNo = dto.ReferenceNo;
        v.UpdatedBy = _ctx.CurrentUserNo(); v.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(voucherNo, ct);
    }

    public async Task<Fin1101VoucherDto> SubmitAsync(long voucherNo, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token =>
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.CompanyNo == companyNo && x.IsDeleted == 0, token)
                ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

            if (v.Status != StDraft) throw new ValidationException("Only a Draft voucher can be submitted");

            var outcome = await _approvalService.RaiseAsync(DocType, v.VoucherNo, v.VoucherId, v.TotalDebit, token);
            if (outcome.AutoApproved)
            {
                await PostInternalAsync(v, token);
            }
            else
            {
                v.ApprovalRequestNo = outcome.ApprovalRequestNo;
                await _db.SaveChangesAsync(token);
            }

            return await GetDetailAsync(voucherNo, token);
        }, ct);
    }

    public async Task<Fin1101VoucherDto> RejectAsync(long voucherNo, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

        if (v.ApprovalRequestNo.HasValue)
        {
            await _approvalService.ActAsync(v.ApprovalRequestNo.Value, false, "Rejected by user", ct);
            v.ApprovalRequestNo = null;
            await _db.SaveChangesAsync(ct);
        }

        return await GetDetailAsync(voucherNo, ct);
    }

    public async Task ApplyApprovalOutcomeAsync(long voucherNo, bool approved, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

        if (approved) await PostInternalAsync(v, ct);
        else { v.ApprovalRequestNo = null; await _db.SaveChangesAsync(ct); }
    }

    private async Task EnsurePeriodOpenAsync(long finPeriodNo, CancellationToken ct)
    {
        var period = await _calendar.FindPeriodAsync(finPeriodNo, ct)
            ?? throw new ValidationException("Financial period not found");
        if (period.PeriodStatus != 1)
            throw new ValidationException($"Cannot post: period '{period.FinPeriodName}' is not Open (status={period.PeriodStatus}). Post an adjusting voucher in an open period instead.");
    }

    private async Task PostInternalAsync(FinVoucher v, CancellationToken ct)
    {
        if (v.Status == StPosted) return;
        if (v.Status != StDraft) throw new ValidationException("Only a Draft voucher can be posted");

        await EnsurePeriodOpenAsync(v.FinPeriodNo, ct);

        var lines = await _db.FinVoucherDtls.Where(l => l.VoucherNo == v.VoucherNo && l.IsDeleted == 0).OrderBy(l => l.LineNo).ToListAsync(ct);
        if (lines.Count < 2) throw new ValidationException("A voucher must have at least two lines to post");

        // Re-verify double-entry balance before writing to the immutable ledger
        decimal dr = lines.Sum(l => l.Debit);
        decimal cr = lines.Sum(l => l.Credit);
        if (Math.Abs(dr - cr) > 0.0001m)
            throw new ValidationException($"Out of balance: debit {dr:N2} != credit {cr:N2} — voucher cannot be posted");

        foreach (var l in lines)
        {
            var led = new FinLedger
            {
                CompanyNo = v.CompanyNo,
                BranchNo = v.BranchNo,
                AccountNo = l.AccountNo,
                VoucherNo = v.VoucherNo,
                VoucherDtlNo = l.VoucherDtlNo,
                VoucherDate = v.VoucherDate,
                FinYearNo = v.FinYearNo,
                FinPeriodNo = v.FinPeriodNo,
                Debit = l.Debit,
                Credit = l.Credit,
                CostCenterNo = l.CostCenterNo,
                PartyType = l.PartyType,
                PartyNo = l.PartyNo,
                VatTaxNo = l.VatTaxNo,
                IsReversal = 0,
                IsActive = 1,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.FinLedgers.Add(led);
            // Balance cache (fin_account_balance) is intentionally NOT written here.
            // All balances are derived from fin_ledger — the single source of truth.
            // See Sprint 2 G7 decision: (B) DROP the write amplification.
        }

        v.Status = StPosted;
        v.PostedBy = _ctx.CurrentUserNo();
        v.PostedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Fin1101VoucherDto> CancelAsync(long voucherNo, CancellationToken ct = default)
    {
        return await _uow.ExecuteAsync(async token =>
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.CompanyNo == companyNo && x.IsDeleted == 0, token)
                ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

            if (v.Status == StCancelled) throw new ValidationException("Voucher is already cancelled");

            if (v.Status == StPosted)
            {
                // Period guard — resolve from original ledger rows (not voucher header)
                var ledgers = await _db.FinLedgers.Where(l => l.VoucherNo == voucherNo && l.IsDeleted == 0 && l.IsReversal == 0).ToListAsync(token);
                var periodNos = ledgers.Select(l => l.FinPeriodNo).Distinct().ToList();
                foreach (var pNo in periodNos)
                {
                    await EnsurePeriodOpenAsync(pNo, token);
                }
                foreach (var orig in ledgers)
                {
                    var rev = new FinLedger
                    {
                        CompanyNo = v.CompanyNo,
                        BranchNo = v.BranchNo,
                        AccountNo = orig.AccountNo,
                        VoucherNo = v.VoucherNo,
                        VoucherDtlNo = orig.VoucherDtlNo,
                        VoucherDate = v.VoucherDate,
                        FinYearNo = orig.FinYearNo,
                        FinPeriodNo = orig.FinPeriodNo,
                        DrCr = orig.DrCr == "dr" ? "cr" : "dr",
                        Amount = orig.Amount,
                        IsReversal = 1,
                        IsActive = 1, IsDeleted = 0,
                        CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
                    };
                    _db.FinLedgers.Add(rev);
                    // Balance cache is intentionally NOT updated here.
                    // All balances are derived from fin_ledger — the single source of truth.
                }
                // Reversal model (A): same-voucher reversal. Reversing rows are appended to
                // fin_ledger under the ORIGINAL voucher_no with is_reversal = 1.
                // reversal_of_no is NOT set (no self-reference). The is_reversal flag on
                // each ledger row IS the audit trail. See fin-db.md § reversal model.
            }

            v.Status = StCancelled;
            v.UpdatedBy = _ctx.CurrentUserNo(); v.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(token);
            return await GetDetailAsync(voucherNo, token);
        }, ct);
    }

    public async Task DeleteAsync(long voucherNo, CancellationToken ct = default)
    {
        await _uow.ExecuteAsync(async token =>
        {
            long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
            var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.CompanyNo == companyNo && x.IsDeleted == 0, token)
                ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

            if (v.Status != StDraft) throw new ValidationException("Only a Draft voucher can be deleted");

            v.IsDeleted = 1; v.IsActive = 0;
            v.DeletedBy = _ctx.CurrentUserNo(); v.DeletedAt = DateTime.UtcNow;

            var lines = await _db.FinVoucherDtls.Where(l => l.VoucherNo == voucherNo && l.IsDeleted == 0).ToListAsync(token);
            foreach (var l in lines) { l.IsDeleted = 1; l.IsActive = 0; l.DeletedBy = _ctx.CurrentUserNo(); l.DeletedAt = DateTime.UtcNow; }

            await _db.SaveChangesAsync(token);
        }, ct);
    }

    public async Task<bool> AlreadyPostedAsync(string sourceDocType, long sourceDocNo, long companyNo, CancellationToken ct = default) =>
        await _db.FinVouchers.AnyAsync(v => v.CompanyNo == companyNo && v.SourceDocType == sourceDocType && v.SourceDocNo == sourceDocNo && v.Status == StPosted && v.IsDeleted == 0, ct);

    public async Task<long> ResolveSystemJournalTypeAsync(long companyNo, CancellationToken ct = default)
    {
        var type = await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.CompanyNo == companyNo && t.VoucherTypeCode == "JV" && t.IsDeleted == 0, ct)
            ?? await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.CompanyNo == companyNo && t.IsDeleted == 0, ct)
            ?? throw new ValidationException("No voucher type available");
        return type.VoucherTypeNo;
    }

    public async Task<long> ResolveSystemVoucherTypeAsync(long companyNo, short preferredKind, CancellationToken ct = default)
    {
        var type = await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.CompanyNo == companyNo && t.BaseKind == preferredKind && t.IsDeleted == 0, ct)
            ?? await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.CompanyNo == companyNo && t.IsDeleted == 0, ct)
            ?? throw new ValidationException("No voucher type available");
        return type.VoucherTypeNo;
    }

    public async Task<long> PostSystemVoucherAsync(long companyNo, long branchNo, long voucherTypeNo, DateTime voucherDate, string narration, short sourceModule, string sourceDocType, long sourceDocNo, List<Fin1101VoucherLineDto> lines, CancellationToken ct = default)
    {
        var dto = new Fin1101VoucherDto
        {
            VoucherTypeNo = voucherTypeNo,
            VoucherDate = voucherDate,
            Narration = narration,
            SourceModule = sourceModule,
            SourceDocType = sourceDocType,
            SourceDocNo = sourceDocNo,
            BranchNo = branchNo,
            Lines = lines
        };

        var created = await InsertInternalAsync(companyNo, branchNo, dto, ct);
        var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == created.VoucherNo.Value, ct);
        if (v != null)
        {
            await PostInternalAsync(v, ct);
        }

        return created.VoucherNo!.Value;
    }

    private async Task ValidateLinesAsync(List<Fin1101VoucherLineDto>? lines, long companyNo, CancellationToken ct)
    {
        if (lines == null || lines.Count < 2) throw new ValidationException("A voucher needs at least two lines");

        // Batch-load all referenced accounts to avoid N+1
        var accountNos = lines.Select(l => l.AccountNo).Distinct().ToList();
        var accounts = await _db.FinAccounts.AsNoTracking()
            .Where(a => accountNos.Contains(a.AccountNo) && a.IsDeleted == 0)
            .ToDictionaryAsync(a => a.AccountNo, ct);

        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            int lineNum = i + 1;

            if (l.Debit < 0 || l.Credit < 0)
                throw new ValidationException($"Line {lineNum}: amounts cannot be negative");
            if (l.AccountNo <= 0) throw new ValidationException("All lines must select an account");
            if (l.Debit > 0 && l.Credit > 0)
                throw new ValidationException($"Line {lineNum}: a line is either debit OR credit, not both");
            if (l.Debit == 0 && l.Credit == 0)
                throw new ValidationException($"Line {lineNum}: enter a debit or a credit");

            if (!accounts.TryGetValue(l.AccountNo, out var acc))
                throw new NotFoundException($"Line {lineNum}: account not found: no={l.AccountNo}");
            if (acc.CompanyNo != companyNo)
                throw new ValidationException($"Line {lineNum}: account belongs to another company");
            if (acc.IsActive != 1)
                throw new ValidationException($"Line {lineNum}: '{acc.AccountName}' is inactive");
            if (acc.IsPostable != 1)
                throw new ValidationException($"Line {lineNum}: '{acc.AccountName}' is a header account — not postable");
            if (acc.RequiresParty == 1 && !l.PartyNo.HasValue)
                throw new ValidationException($"Line {lineNum}: '{acc.AccountName}' requires a party (customer/supplier)");
            if (acc.RequiresCostCenter == 1 && !l.CostCenterNo.HasValue)
                throw new ValidationException($"Line {lineNum}: '{acc.AccountName}' requires a cost center");

            totalDebit += l.Debit;
            totalCredit += l.Credit;
        }

        if (totalDebit == 0)
            throw new ValidationException("Voucher total is zero");
        if (Math.Abs(totalDebit - totalCredit) > 0.0001m)
            throw new ValidationException($"Out of balance: debit {totalDebit:N2} ≠ credit {totalCredit:N2}");
    }

    private async Task ReplaceLinesInternalAsync(FinVoucher v, List<Fin1101VoucherLineDto> lines, CancellationToken ct)
    {
        long voucherNo = v.VoucherNo;
        var existing = await _db.FinVoucherDtls.Where(l => l.VoucherNo == voucherNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var e in existing) { e.IsDeleted = 1; e.DeletedBy = _ctx.CurrentUserNo(); e.DeletedAt = DateTime.UtcNow; }

        int lineNo = 1;
        foreach (var dtoLine in lines)
        {
            string drCr = dtoLine.Debit > 0 ? "dr" : "cr";
            var dtl = new FinVoucherDtl
            {
                VoucherNo = voucherNo,
                // Lines inherit the voucher's scope — both columns are NOT NULL.
                CompanyNo = v.CompanyNo,
                BranchNo = v.BranchNo,
                LineNo = lineNo++,
                AccountNo = dtoLine.AccountNo,
                DrCr = drCr,
                Debit = dtoLine.Debit,
                Credit = dtoLine.Credit,
                DebitFc = dtoLine.Debit,
                CreditFc = dtoLine.Credit,
                CostCenterNo = dtoLine.CostCenterNo,
                PartyType = dtoLine.PartyType,
                PartyNo = dtoLine.PartyNo,
                AgainstVoucherNo = dtoLine.AgainstVoucherNo,
                VatTaxNo = dtoLine.VatTaxNo,
                TaxRatePct = dtoLine.TaxRatePct,
                LineNarration = dtoLine.LineNarration,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.FinVoucherDtls.Add(dtl);
        }
    }

    private async Task<List<Fin1101VoucherLineDto>> GetLinesAsync(long voucherNo, CancellationToken ct)
    {
        // Load only the lines for this voucher, then batch-load only the referenced accounts
        var lines = await _db.FinVoucherDtls
            .AsNoTracking()
            .Where(l => l.VoucherNo == voucherNo && l.IsDeleted == 0)
            .OrderBy(l => l.LineNo)
            .ToListAsync(ct);

        var accountNos = lines.Select(l => l.AccountNo).Distinct().ToList();
        var accounts = await _db.FinAccounts.AsNoTracking()
            .Where(a => accountNos.Contains(a.AccountNo) && a.IsDeleted == 0)
            .ToDictionaryAsync(a => a.AccountNo, ct);

        return lines.Select(l => new Fin1101VoucherLineDto
        {
            VoucherDtlNo = l.VoucherDtlNo,
            LineNo = l.LineNo,
            AccountNo = l.AccountNo,
            DrCr = l.DrCr,
            Debit = l.Debit,
            Credit = l.Credit,
            DebitFc = l.DebitFc,
            CreditFc = l.CreditFc,
            CostCenterNo = l.CostCenterNo,
            PartyType = l.PartyType,
            PartyNo = l.PartyNo,
            AgainstVoucherNo = l.AgainstVoucherNo,
            VatTaxNo = l.VatTaxNo,
            TaxRatePct = l.TaxRatePct,
            LineNarration = l.LineNarration,
            AccountCode = accounts.ContainsKey(l.AccountNo) ? accounts[l.AccountNo].AccountCode : null,
            AccountName = accounts.ContainsKey(l.AccountNo) ? accounts[l.AccountNo].AccountName : null
        }).ToList();
    }

    private static Fin1101VoucherDto ToDto(FinVoucher v, string? typeName, short? baseKind, List<Fin1101VoucherLineDto>? lines) => new()
    {
        VoucherNo = v.VoucherNo,
        VoucherId = v.VoucherId,
        VoucherTypeNo = v.VoucherTypeNo,
        VoucherDate = v.VoucherDate,
        FinYearNo = v.FinYearNo,
        FinPeriodNo = v.FinPeriodNo,
        Narration = v.Narration,
        ReferenceNo = v.ReferenceNo,
        CurrencyNo = v.CurrencyNo,
        FxRate = v.FxRate,
        TotalDebit = v.TotalDebit,
        TotalCredit = v.TotalCredit,
        Status = v.Status,
        SourceModule = v.SourceModule,
        SourceDocType = v.SourceDocType,
        SourceDocNo = v.SourceDocNo,
        ReversalOfNo = v.ReversalOfNo,
        ApprovalRequestNo = v.ApprovalRequestNo,
        ApprovedBy = v.ApprovedBy,
        PostedBy = v.PostedBy,
        PostedAt = v.PostedAt,
        BranchNo = v.BranchNo,
        IsActive = v.IsActive ?? 0,
        RowVersion = v.RowVersion,
        BaseKind = baseKind,
        VoucherTypeName = typeName,
        Lines = lines ?? new()
    };
}
