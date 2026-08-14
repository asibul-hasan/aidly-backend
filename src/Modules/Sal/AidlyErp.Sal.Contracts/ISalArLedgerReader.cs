namespace AidlyErp.Sal.Contracts;

/// <summary>One customer's outstanding receivable, as SAL's own sub-ledger records it.</summary>
public sealed record ArPartyBalance(long PartyNo, decimal Balance);

/// <summary>One movement on a customer's account. Debit increases the receivable, credit reduces it.</summary>
public sealed record ArStatementRow(
    DateTime TxnDate,
    short RefDocType,
    string RefDocNo,
    decimal Debit,
    decimal Credit,
    string? Remarks);

/// <summary>
/// Read-only view of the AR sub-ledger for modules that reconcile against it but do not own it —
/// specifically FIN_1203, which compares this to the GL's accounts-receivable control account.
///
/// <para>Mirror of <c>IPurApLedgerReader</c> with the sign reversed: a customer owes us, a supplier
/// is owed by us. The two interfaces are kept separate rather than shared because each module owns
/// its own sub-ledger, and a shared one would put a SAL/PUR concept in neutral territory.</para>
/// </summary>
public interface ISalArLedgerReader
{
    /// <summary>Outstanding receivable per customer as of a date. Parties that net to zero are omitted.</summary>
    Task<IReadOnlyList<ArPartyBalance>> GetOutstandingAsync(long companyNo, DateOnly asOf,
                                                            CancellationToken cancellationToken = default);

    /// <summary>
    /// One customer's movements within a window, plus the balance carried in on <paramref name="from"/>.
    /// The opening figure is summed from every earlier movement rather than read from a stored
    /// running balance, which is only correct if rows were ever inserted in date order.
    /// </summary>
    Task<(decimal Opening, IReadOnlyList<ArStatementRow> Rows)> GetStatementAsync(
        long companyNo, long partyNo, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);
}
