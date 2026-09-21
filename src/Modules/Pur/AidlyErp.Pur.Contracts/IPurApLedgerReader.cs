namespace AidlyErp.Pur.Contracts;

/// <summary>One supplier's outstanding payable, as PUR's own sub-ledger records it.</summary>
public sealed record ApPartyBalance(long PartyNo, decimal Balance);

/// <summary>One movement on a supplier's account. Debit reduces the payable, credit increases it.</summary>
public sealed record ApStatementRow(
    DateTime TxnDate,
    short RefDocType,
    string RefDocNo,
    decimal Debit,
    decimal Credit,
    string? Remarks);

/// <summary>
/// Read-only view of the AP sub-ledger for modules that reconcile against it but do not own it —
/// specifically FIN_1202, which compares this to the GL's accounts-payable control account.
///
/// <para>Both are written from the same transaction with the same numbers, so they agree by
/// construction. A difference means something wrote one and not the other, which is exactly what
/// FIN_1202 exists to surface — so this returns PUR's numbers unadjusted, never reconciled ones.</para>
/// </summary>
public interface IPurApLedgerReader
{
    /// <summary>Outstanding payable per supplier as of a date. Parties that net to zero are omitted.</summary>
    Task<IReadOnlyList<ApPartyBalance>> GetOutstandingAsync(long companyNo, DateOnly asOf,
                                                            CancellationToken cancellationToken = default);

    /// <summary>
    /// One supplier's movements within a window, plus the balance carried in on <paramref name="from"/>.
    /// The opening figure is summed from every earlier movement rather than read from a stored
    /// running balance, which is only correct if rows were ever inserted in date order.
    /// </summary>
    Task<(decimal Opening, IReadOnlyList<ApStatementRow> Rows)> GetStatementAsync(
        long companyNo, long partyNo, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);
}
