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
    Task<List<Fin1101VoucherDto>> GetListAsync(long? typeNo, DateTime? fromDate, DateTime? toDate, string? search, CancellationToken ct = default);
    Task<Fin1101VoucherDto> GetDetailAsync(long voucherNo, CancellationToken ct = default);
    Task<Fin1101VoucherDto> SaveAsync(Fin1101VoucherDto dto, CancellationToken ct = default);
    Task<Fin1101VoucherDto> SubmitAsync(long voucherNo, CancellationToken ct = default);
    Task<Fin1101VoucherDto> RejectAsync(long voucherNo, CancellationToken ct = default);
    Task ApplyApprovalOutcomeAsync(long voucherNo, bool approved, CancellationToken ct = default);
    Task<Fin1101VoucherDto> CancelAsync(long voucherNo, CancellationToken ct = default);
    Task DeleteAsync(long voucherNo, CancellationToken ct = default);
    Task<bool> AlreadyPostedAsync(string sourceDocType, long sourceDocNo, long companyNo, CancellationToken ct = default);
    Task<long> ResolveSystemJournalTypeAsync(long companyNo, CancellationToken ct = default);
    Task<long> PostSystemVoucherAsync(long companyNo, long branchNo, long voucherTypeNo, DateTime voucherDate, string narration, short sourceModule, string sourceDocType, long sourceDocNo, List<Fin1101VoucherLineDto> lines, CancellationToken ct = default);
}

public class Fin1101Service : IFin1101Service
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IApprovalService _approvalService;

    public const string DocType = "FIN_VOUCHER";
    private const short StDraft = 1, StPosted = 2, StCancelled = 3;

    public Fin1101Service(IFinDbContext db, ICompanyBranchContext ctx, IApprovalService approvalService)
    {
        _db = db;
        _ctx = ctx;
        _approvalService = approvalService;
    }

    public async Task<List<Fin1101VoucherDto>> GetListAsync(long? typeNo, DateTime? fromDate, DateTime? toDate, string? search, CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? 0;
        var query = _db.FinVouchers.AsNoTracking().Where(v => v.CompanyNo == companyNo && v.IsDeleted == 0);

        if (typeNo.HasValue && typeNo.Value > 0) query = query.Where(v => v.VoucherTypeNo == typeNo.Value);
        if (fromDate.HasValue) query = query.Where(v => v.VoucherDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(v => v.VoucherDate <= toDate.Value);
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
        var v = await _db.FinVouchers.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

        var lines = await GetLinesAsync(voucherNo, ct);
        var vType = await _db.FinVoucherTypes.AsNoTracking().FirstOrDefaultAsync(t => t.VoucherTypeNo == v.VoucherTypeNo, ct);

        return ToDto(v, vType?.VoucherTypeName, vType?.BaseKind, lines);
    }

    public async Task<Fin1101VoucherDto> SaveAsync(Fin1101VoucherDto dto, CancellationToken ct = default)
    {
        return dto.VoucherNo.HasValue && dto.VoucherNo.Value > 0
            ? await UpdateAsync(dto.VoucherNo.Value, dto, ct)
            : await InsertAsync(dto, ct);
    }

    private async Task<Fin1101VoucherDto> InsertAsync(Fin1101VoucherDto dto, CancellationToken ct)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        long branchNo = dto.BranchNo ?? _ctx.CurrentBranchNo() ?? throw new ValidationException("Branch context is required");

        var type = await _db.FinVoucherTypes.FirstOrDefaultAsync(t => t.VoucherTypeNo == dto.VoucherTypeNo && t.CompanyNo == companyNo && t.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher type not found: {dto.VoucherTypeNo}");

        ValidateLines(dto.Lines);

        // sys_fin_year(_dtl).start_date/end_date are DATE columns (DateOnly).
        var voucherDay = DateOnly.FromDateTime(dto.VoucherDate);

        var finYear = await _db.FinYears.FirstOrDefaultAsync(y => y.CompanyNo == companyNo && y.StartDate <= voucherDay && y.EndDate >= voucherDay && y.IsDeleted == 0, ct)
            ?? throw new ValidationException($"No financial year configured for date {dto.VoucherDate:yyyy-MM-dd}");

        var finPeriod = await _db.FinYearDtls.FirstOrDefaultAsync(p => p.FinYearNo == finYear.FinYearNo && p.StartDate <= voucherDay && p.EndDate >= voucherDay && p.IsDeleted == 0, ct)
            ?? throw new ValidationException($"No financial period configured for date {dto.VoucherDate:yyyy-MM-dd}");

        decimal totalDebit = dto.Lines.Sum(l => l.Debit);
        decimal totalCredit = dto.Lines.Sum(l => l.Credit);

        var v = new FinVoucher
        {
            CompanyNo = companyNo,
            BranchNo = branchNo,
            VoucherId = "TEMP",
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
            Status = StDraft,
            IsActive = 1,
            IsDeleted = 0,
            CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
        };

        _db.FinVouchers.Add(v);
        await _db.SaveChangesAsync(ct);

        v.VoucherId = $"{type.Prefix ?? "VCH"}-{v.VoucherNo:D6}";
        await ReplaceLinesInternalAsync(v.VoucherNo, dto.Lines, ct);
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(v.VoucherNo, ct);
    }

    private async Task<Fin1101VoucherDto> UpdateAsync(long voucherNo, Fin1101VoucherDto dto, CancellationToken ct)
    {
        var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

        if (v.Status != StDraft) throw new ValidationException("Only a Draft voucher can be edited");

        if (dto.Lines != null && dto.Lines.Count > 0)
        {
            ValidateLines(dto.Lines);
            v.TotalDebit = dto.Lines.Sum(l => l.Debit);
            v.TotalCredit = dto.Lines.Sum(l => l.Credit);
            await ReplaceLinesInternalAsync(voucherNo, dto.Lines, ct);
        }

        if (dto.Narration != null) v.Narration = dto.Narration;
        if (dto.ReferenceNo != null) v.ReferenceNo = dto.ReferenceNo;
        v.UpdatedBy = _ctx.CurrentUserNo(); v.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(voucherNo, ct);
    }

    public async Task<Fin1101VoucherDto> SubmitAsync(long voucherNo, CancellationToken ct = default)
    {
        var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

        if (v.Status != StDraft) throw new ValidationException("Only a Draft voucher can be submitted");

        var outcome = await _approvalService.RaiseAsync(DocType, v.VoucherNo, v.VoucherId, v.TotalDebit, ct);
        if (outcome.AutoApproved)
        {
            await PostInternalAsync(v, ct);
        }
        else
        {
            v.ApprovalRequestNo = outcome.ApprovalRequestNo;
            await _db.SaveChangesAsync(ct);
        }

        return await GetDetailAsync(voucherNo, ct);
    }

    public async Task<Fin1101VoucherDto> RejectAsync(long voucherNo, CancellationToken ct = default)
    {
        var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.IsDeleted == 0, ct)
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
        var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

        if (approved) await PostInternalAsync(v, ct);
        else { v.ApprovalRequestNo = null; await _db.SaveChangesAsync(ct); }
    }

    private async Task PostInternalAsync(FinVoucher v, CancellationToken ct)
    {
        if (v.Status == StPosted) return;
        if (v.Status != StDraft) throw new ValidationException("Only a Draft voucher can be posted");

        var lines = await _db.FinVoucherDtls.Where(l => l.VoucherNo == v.VoucherNo && l.IsDeleted == 0).OrderBy(l => l.LineNo).ToListAsync(ct);
        if (lines.Count < 2) throw new ValidationException("Voucher must have at least two lines to post");

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
                DrCr = l.DrCr,
                Amount = l.DrCr == "dr" ? l.Debit : l.Credit,
                IsActive = 1,
                IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.FinLedgers.Add(led);

            // Upsert Account Balance Cache
            var bal = await _db.FinAccountBalances.FirstOrDefaultAsync(b => b.AccountNo == l.AccountNo && b.FinPeriodNo == v.FinPeriodNo && b.BranchNo == v.BranchNo && b.IsDeleted == 0, ct);
            if (bal == null)
            {
                bal = new FinAccountBalance
                {
                    CompanyNo = v.CompanyNo,
                    BranchNo = v.BranchNo,
                    AccountNo = l.AccountNo,
                    FinYearNo = v.FinYearNo,
                    FinPeriodNo = v.FinPeriodNo,
                    DebitAmount = l.Debit,
                    CreditAmount = l.Credit,
                    ClosingBalance = l.Debit - l.Credit,
                    IsActive = 1, IsDeleted = 0,
                    CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
                };
                _db.FinAccountBalances.Add(bal);
            }
            else
            {
                bal.DebitAmount += l.Debit;
                bal.CreditAmount += l.Credit;
                bal.ClosingBalance += (l.Debit - l.Credit);
                bal.UpdatedBy = _ctx.CurrentUserNo(); bal.UpdatedAt = DateTime.UtcNow;
            }
        }

        v.Status = StPosted;
        v.PostedBy = _ctx.CurrentUserNo();
        v.PostedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Fin1101VoucherDto> CancelAsync(long voucherNo, CancellationToken ct = default)
    {
        var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

        if (v.Status == StCancelled) throw new ValidationException("Voucher is already cancelled");

        if (v.Status == StPosted)
        {
            var ledgers = await _db.FinLedgers.Where(l => l.VoucherNo == voucherNo && l.IsDeleted == 0).ToListAsync(ct);
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
                    IsActive = 1, IsDeleted = 0,
                    CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
                };
                _db.FinLedgers.Add(rev);

                var bal = await _db.FinAccountBalances.FirstOrDefaultAsync(b => b.AccountNo == orig.AccountNo && b.FinPeriodNo == orig.FinPeriodNo && b.BranchNo == orig.BranchNo && b.IsDeleted == 0, ct);
                if (bal != null)
                {
                    if (orig.DrCr == "dr") { bal.DebitAmount -= orig.Amount; bal.ClosingBalance -= orig.Amount; }
                    else { bal.CreditAmount -= orig.Amount; bal.ClosingBalance += orig.Amount; }
                }
            }
        }

        v.Status = StCancelled;
        v.UpdatedBy = _ctx.CurrentUserNo(); v.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(voucherNo, ct);
    }

    public async Task DeleteAsync(long voucherNo, CancellationToken ct = default)
    {
        var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == voucherNo && x.IsDeleted == 0, ct)
            ?? throw new NotFoundException($"Voucher not found: {voucherNo}");

        if (v.Status != StDraft) throw new ValidationException("Only a Draft voucher can be deleted");

        v.IsDeleted = 1; v.IsActive = 0;
        v.DeletedBy = _ctx.CurrentUserNo(); v.DeletedAt = DateTime.UtcNow;

        var lines = await _db.FinVoucherDtls.Where(l => l.VoucherNo == voucherNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var l in lines) { l.IsDeleted = 1; l.IsActive = 0; l.DeletedBy = _ctx.CurrentUserNo(); l.DeletedAt = DateTime.UtcNow; }

        await _db.SaveChangesAsync(ct);
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

        var created = await InsertAsync(dto, ct);
        var v = await _db.FinVouchers.FirstOrDefaultAsync(x => x.VoucherNo == created.VoucherNo.Value, ct);
        if (v != null)
        {
            v.SourceModule = sourceModule;
            v.SourceDocType = sourceDocType;
            v.SourceDocNo = sourceDocNo;
            await PostInternalAsync(v, ct);
        }

        return created.VoucherNo!.Value;
    }

    private static void ValidateLines(List<Fin1101VoucherLineDto>? lines)
    {
        if (lines == null || lines.Count < 2) throw new ValidationException("Voucher must have at least two lines");

        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        foreach (var l in lines)
        {
            if (l.AccountNo <= 0) throw new ValidationException("All lines must select an account");
            if (l.Debit > 0 && l.Credit > 0) throw new ValidationException("A single line cannot have both debit and credit amounts");
            if (l.Debit == 0 && l.Credit == 0) throw new ValidationException("Each line must have a non-zero debit or credit amount");
            totalDebit += l.Debit;
            totalCredit += l.Credit;
        }

        if (Math.Abs(totalDebit - totalCredit) > 0.001m)
            throw new ValidationException($"Unbalanced voucher: Total Debit ({totalDebit:N2}) != Total Credit ({totalCredit:N2})");
    }

    private async Task ReplaceLinesInternalAsync(long voucherNo, List<Fin1101VoucherLineDto> lines, CancellationToken ct)
    {
        var existing = await _db.FinVoucherDtls.Where(l => l.VoucherNo == voucherNo && l.IsDeleted == 0).ToListAsync(ct);
        foreach (var e in existing) { e.IsDeleted = 1; e.DeletedBy = _ctx.CurrentUserNo(); e.DeletedAt = DateTime.UtcNow; }

        int lineNo = 1;
        foreach (var dtoLine in lines)
        {
            string drCr = dtoLine.Debit > 0 ? "dr" : "cr";
            var dtl = new FinVoucherDtl
            {
                VoucherNo = voucherNo,
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
                LineNarration = dtoLine.LineNarration,
                IsActive = 1, IsDeleted = 0,
                CreatedBy = _ctx.CurrentUserNo(), CreatedAt = DateTime.UtcNow
            };
            _db.FinVoucherDtls.Add(dtl);
        }
    }

    private async Task<List<Fin1101VoucherLineDto>> GetLinesAsync(long voucherNo, CancellationToken ct)
    {
        var accounts = await _db.FinAccounts.AsNoTracking().Where(a => a.IsDeleted == 0).ToDictionaryAsync(a => a.AccountNo, ct);

        return await _db.FinVoucherDtls
            .AsNoTracking()
            .Where(l => l.VoucherNo == voucherNo && l.IsDeleted == 0)
            .OrderBy(l => l.LineNo)
            .Select(l => new Fin1101VoucherLineDto
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
                LineNarration = l.LineNarration,
                AccountCode = accounts.ContainsKey(l.AccountNo) ? accounts[l.AccountNo].AccountCode : null,
                AccountName = accounts.ContainsKey(l.AccountNo) ? accounts[l.AccountNo].AccountName : null
            })
            .ToListAsync(ct);
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
        IsActive = v.IsActive,
        RowVersion = v.RowVersion,
        BaseKind = baseKind,
        VoucherTypeName = typeName,
        Lines = lines ?? new()
    };
}
