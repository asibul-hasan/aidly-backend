using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Application.Interfaces;
using AidlyErp.Pur.Contracts;
using AidlyErp.Sal.Contracts;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Fin.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// FinSubLedgerService — FIN_1202 (AP) and FIN_1203 (AR)
//
// PUR and SAL each keep a sub-ledger of what every party owes; FIN keeps a
// single control account per side in the GL. Both are written from the same
// transaction, so they agree by construction — until something writes one and
// not the other. These two forms are how that gets noticed.
//
// This is NOT FIN_1307. That report ages the GL alone and answers "how overdue".
// This one puts the two sets of books side by side and answers "do they still
// agree", which is a different question with a different failure mode.
// ═══════════════════════════════════════════════════════════════════════════

public interface IFinSubLedgerService
{
    Task<FinSubLedgerReconDto> GetReconciliationAsync(short partyType, DateTime asOfDate, CancellationToken ct = default);
    Task<FinSubLedgerStatementDto> GetStatementAsync(short partyType, long partyNo, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}

public class FinSubLedgerService : IFinSubLedgerService
{
    private const short Live = 0;
    private const short PartyCustomer = 1;
    private const short PartySupplier = 2;

    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;
    private readonly IPartyLookup _partyLookup;
    private readonly IPurApLedgerReader _apLedger;
    private readonly ISalArLedgerReader _arLedger;

    public FinSubLedgerService(IFinDbContext db, ICompanyBranchContext ctx, IPartyLookup partyLookup,
                               IPurApLedgerReader apLedger, ISalArLedgerReader arLedger)
    {
        _db = db;
        _ctx = ctx;
        _partyLookup = partyLookup;
        _apLedger = apLedger;
        _arLedger = arLedger;
    }

    public async Task<FinSubLedgerReconDto> GetReconciliationAsync(short partyType, DateTime asOfDate,
                                                                   CancellationToken ct = default)
    {
        Validate(partyType);
        long companyNo = Company();
        var asOf = DateOnly.FromDateTime(asOfDate);

        // ── The sub-ledger side, straight from the module that owns it ──────────
        var subLedger = partyType == PartyCustomer
            ? (await _arLedger.GetOutstandingAsync(companyNo, asOf, ct))
                .Select(b => (b.PartyNo, b.Balance)).ToList()
            : (await _apLedger.GetOutstandingAsync(companyNo, asOf, ct))
                .Select(b => (b.PartyNo, b.Balance)).ToList();

        var result = new FinSubLedgerReconDto
        {
            AsOfDate = asOfDate,
            PartyType = partyType,
            SubLedgerTotal = subLedger.Sum(b => b.Balance)
        };

        // ── The GL side ─────────────────────────────────────────────────────────
        short controlType = partyType == PartyCustomer ? (short)1 : (short)2;

        var controlAccounts = await _db.FinAccounts.AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.ControlType == controlType && a.IsDeleted == Live)
            .Select(a => a.AccountNo)
            .ToListAsync(ct);

        if (controlAccounts.Count == 0)
        {
            // No control account configured yet. Reporting a difference equal to the whole
            // sub-ledger would be alarming and wrong — the GL simply has nothing to say.
            result.NoControlAccount = true;
            result.Rows = await BuildRowsAsync(partyType, subLedger, new Dictionary<long, decimal>(), ct);
            return result;
        }

        var legs = await _db.FinLedgers.AsNoTracking()
            .Where(l => l.CompanyNo == companyNo && l.IsDeleted == Live
                        && controlAccounts.Contains(l.AccountNo)
                        && l.VoucherDate <= asOfDate)
            .Select(l => new { l.PartyNo, l.PartyType, l.Debit, l.Credit })
            .ToListAsync(ct);

        // AR is an asset (debit-positive), AP a liability (credit-positive).
        decimal Signed(decimal debit, decimal credit) =>
            partyType == PartyCustomer ? debit - credit : credit - debit;

        result.GlControlTotal = legs.Sum(l => Signed(l.Debit, l.Credit));

        // A leg tagged with the wrong party type sits on the control account but belongs to nobody
        // this report can name, so it counts as unattributed rather than being silently reassigned.
        result.UnattributedGl = legs
            .Where(l => !l.PartyNo.HasValue || l.PartyType != partyType)
            .Sum(l => Signed(l.Debit, l.Credit));

        var glByParty = legs
            .Where(l => l.PartyNo.HasValue && l.PartyType == partyType)
            .GroupBy(l => l.PartyNo!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => Signed(l.Debit, l.Credit)));

        result.Difference = result.SubLedgerTotal - result.GlControlTotal;
        result.Rows = await BuildRowsAsync(partyType, subLedger, glByParty, ct);
        return result;
    }

    /// <summary>
    /// Unions the two sides so a party appearing on only one of them still gets a row — those are
    /// the interesting ones, and an inner join would hide exactly the cases worth seeing.
    /// </summary>
    private async Task<List<FinSubLedgerPartyDto>> BuildRowsAsync(
        short partyType, List<(long PartyNo, decimal Balance)> subLedger,
        Dictionary<long, decimal> glByParty, CancellationToken ct)
    {
        var subByParty = subLedger.ToDictionary(b => b.PartyNo, b => b.Balance);
        var partyNos = subByParty.Keys.Union(glByParty.Keys).ToList();
        if (partyNos.Count == 0) return new List<FinSubLedgerPartyDto>();

        var names = await _partyLookup.GetPartyNamesAsync(partyType, partyNos, ct);

        return partyNos
            .Select(no =>
            {
                decimal sub = subByParty.GetValueOrDefault(no);
                decimal gl = glByParty.GetValueOrDefault(no);
                return new FinSubLedgerPartyDto
                {
                    PartyNo = no,
                    PartyName = names.GetValueOrDefault(no) ?? $"Party #{no}",
                    SubLedgerBalance = sub,
                    GlBalance = gl,
                    Difference = sub - gl
                };
            })
            // Mismatches first — a clean row needs no attention, so it should not be at the top.
            .OrderByDescending(r => Math.Abs(r.Difference))
            .ThenByDescending(r => Math.Abs(r.SubLedgerBalance))
            .ToList();
    }

    public async Task<FinSubLedgerStatementDto> GetStatementAsync(short partyType, long partyNo,
                                                                  DateTime fromDate, DateTime toDate,
                                                                  CancellationToken ct = default)
    {
        Validate(partyType);
        if (toDate < fromDate) throw new ValidationException("The end date falls before the start date");

        long companyNo = Company();
        var from = DateOnly.FromDateTime(fromDate);
        var to = DateOnly.FromDateTime(toDate);

        decimal opening;
        List<FinSubLedgerStatementRowDto> rows;

        if (partyType == PartyCustomer)
        {
            var (open, src) = await _arLedger.GetStatementAsync(companyNo, partyNo, from, to, ct);
            opening = open;
            rows = src.Select(r => new FinSubLedgerStatementRowDto
            {
                TxnDate = r.TxnDate, RefDocType = r.RefDocType, RefDocNo = r.RefDocNo,
                Debit = r.Debit, Credit = r.Credit, Remarks = r.Remarks
            }).ToList();
        }
        else
        {
            var (open, src) = await _apLedger.GetStatementAsync(companyNo, partyNo, from, to, ct);
            opening = open;
            rows = src.Select(r => new FinSubLedgerStatementRowDto
            {
                TxnDate = r.TxnDate, RefDocType = r.RefDocType, RefDocNo = r.RefDocNo,
                Debit = r.Debit, Credit = r.Credit, Remarks = r.Remarks
            }).ToList();
        }

        decimal running = opening;
        foreach (var row in rows)
        {
            running += partyType == PartyCustomer ? row.Debit - row.Credit : row.Credit - row.Debit;
            row.RunningBalance = running;
        }

        var names = await _partyLookup.GetPartyNamesAsync(partyType, new[] { partyNo }, ct);

        return new FinSubLedgerStatementDto
        {
            PartyNo = partyNo,
            PartyName = names.GetValueOrDefault(partyNo) ?? $"Party #{partyNo}",
            PartyType = partyType,
            FromDate = fromDate,
            ToDate = toDate,
            OpeningBalance = opening,
            ClosingBalance = running,
            Rows = rows
        };
    }

    private static void Validate(short partyType)
    {
        if (partyType is not (PartyCustomer or PartySupplier))
            throw new ValidationException("Party type must be 1 (customer) or 2 (supplier)");
    }

    private long Company() =>
        _ctx.CurrentCompanyNo() ?? throw new ValidationException("No active company in context");
}
