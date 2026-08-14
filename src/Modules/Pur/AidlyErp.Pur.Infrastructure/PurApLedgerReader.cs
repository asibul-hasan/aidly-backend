using AidlyErp.Pur.Application.Interfaces;
using AidlyErp.Pur.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Pur.Infrastructure;

/// <summary>PUR's side of <see cref="IPurApLedgerReader"/>.</summary>
internal sealed class PurApLedgerReader : IPurApLedgerReader
{
    private const short Live = 0;

    private readonly IPurDbContext _db;

    public PurApLedgerReader(IPurDbContext db) => _db = db;

    public async Task<IReadOnlyList<ApPartyBalance>> GetOutstandingAsync(
        long companyNo, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        // Inclusive of the whole as-of day: txn_date is a timestamp, so comparing against the date
        // itself would drop anything booked later that morning.
        var cutoff = asOf.ToDateTime(TimeOnly.MaxValue);

        var rows = await _db.PurSupplierLedgers
            .AsNoTracking()
            .Where(l => l.CompanyNo == companyNo && l.IsDeleted == Live && l.TxnDate <= cutoff)
            .GroupBy(l => l.SupplierNo)
            // A supplier is a liability: credit raises what we owe, debit settles it.
            .Select(g => new ApPartyBalance(g.Key, g.Sum(x => x.Credit) - g.Sum(x => x.Debit)))
            .ToListAsync(cancellationToken);

        // A supplier billed and paid in full is settled, not outstanding — dropping them keeps the
        // report to accounts that still need action.
        return rows.Where(r => r.Balance != 0m).ToList();
    }

    public async Task<(decimal Opening, IReadOnlyList<ApStatementRow> Rows)> GetStatementAsync(
        long companyNo, long partyNo, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var fromTs = from.ToDateTime(TimeOnly.MinValue);
        var toTs = to.ToDateTime(TimeOnly.MaxValue);

        var scoped = _db.PurSupplierLedgers
            .AsNoTracking()
            .Where(l => l.CompanyNo == companyNo && l.SupplierNo == partyNo && l.IsDeleted == Live);

        decimal opening = await scoped
            .Where(l => l.TxnDate < fromTs)
            .SumAsync(l => l.Credit - l.Debit, cancellationToken);

        var rows = await scoped
            .Where(l => l.TxnDate >= fromTs && l.TxnDate <= toTs)
            .OrderBy(l => l.TxnDate).ThenBy(l => l.SupplierLedgerNo)
            .Select(l => new ApStatementRow(l.TxnDate, l.RefDocType, l.RefDocNo,
                                            l.Debit, l.Credit, l.Remarks))
            .ToListAsync(cancellationToken);

        return (opening, rows);
    }
}
