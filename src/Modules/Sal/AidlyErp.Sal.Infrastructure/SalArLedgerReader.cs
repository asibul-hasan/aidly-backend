using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Sal.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sal.Infrastructure;

/// <summary>SAL's side of <see cref="ISalArLedgerReader"/>.</summary>
internal sealed class SalArLedgerReader : ISalArLedgerReader
{
    private const short Live = 0;

    private readonly ISalDbContext _db;

    public SalArLedgerReader(ISalDbContext db) => _db = db;

    public async Task<IReadOnlyList<ArPartyBalance>> GetOutstandingAsync(
        long companyNo, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        // Inclusive of the whole as-of day: txn_date is a timestamp, so comparing against the date
        // itself would drop anything booked later that morning.
        var cutoff = asOf.ToDateTime(TimeOnly.MaxValue);

        var rows = await _db.SalCustomerLedgers
            .AsNoTracking()
            .Where(l => l.CompanyNo == companyNo && l.IsDeleted == Live && l.TxnDate <= cutoff)
            .GroupBy(l => l.CustomerNo)
            // A customer is an asset: debit raises what they owe, credit settles it.
            .Select(g => new ArPartyBalance(g.Key, g.Sum(x => x.Debit) - g.Sum(x => x.Credit)))
            .ToListAsync(cancellationToken);

        // Cash sales invoice and receipt in the same breath, so a POS-heavy company would otherwise
        // list thousands of customers at zero.
        return rows.Where(r => r.Balance != 0m).ToList();
    }

    public async Task<(decimal Opening, IReadOnlyList<ArStatementRow> Rows)> GetStatementAsync(
        long companyNo, long partyNo, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var fromTs = from.ToDateTime(TimeOnly.MinValue);
        var toTs = to.ToDateTime(TimeOnly.MaxValue);

        var scoped = _db.SalCustomerLedgers
            .AsNoTracking()
            .Where(l => l.CompanyNo == companyNo && l.CustomerNo == partyNo && l.IsDeleted == Live);

        decimal opening = await scoped
            .Where(l => l.TxnDate < fromTs)
            .SumAsync(l => l.Debit - l.Credit, cancellationToken);

        var rows = await scoped
            .Where(l => l.TxnDate >= fromTs && l.TxnDate <= toTs)
            .OrderBy(l => l.TxnDate).ThenBy(l => l.CustomerLedgerNo)
            .Select(l => new ArStatementRow(l.TxnDate, l.RefDocType, l.RefDocNo,
                                            l.Debit, l.Credit, l.Remarks))
            .ToListAsync(cancellationToken);

        return (opening, rows);
    }
}
