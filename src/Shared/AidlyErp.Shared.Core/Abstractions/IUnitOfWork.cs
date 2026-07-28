namespace AidlyErp.Shared.Core.Abstractions;

/// <summary>
/// Explicit transaction boundary — the .NET counterpart of Spring's <c>@Transactional</c>.
///
/// <para>The Java services declare 602 transactional methods. EF Core wraps a single
/// <c>SaveChanges</c> in an implicit transaction, but that is <b>not</b> equivalent: the Java
/// services routinely perform several saves plus reads inside one atomic unit (post a voucher and
/// write its ledger lines; run payroll and write payslips; receive stock, write the stock ledger
/// and update the valuation layer). Without an explicit boundary a mid-operation failure leaves
/// those writes half-applied.</para>
///
/// <para>Usage — the direct analogue of annotating the method <c>@Transactional</c>:</para>
/// <code>
/// await _unitOfWork.ExecuteAsync(async ct =&gt;
/// {
///     var voucher = await PostVoucherAsync(request, ct);
///     await WriteLedgerLinesAsync(voucher, ct);
///     return voucher.VoucherNo;
/// }, cancellationToken);
/// </code>
///
/// <para>Nesting is safe: an inner call joins the ambient transaction rather than starting a
/// second one, mirroring the default <c>Propagation.REQUIRED</c> behaviour.</para>
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> inside a transaction, committing on success and rolling back on any exception.</summary>
    Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken cancellationToken = default);

    /// <summary>Runs <paramref name="work"/> inside a transaction, committing on success and rolling back on any exception.</summary>
    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> with the tenant and soft-delete query filters disabled — the
    /// counterpart of Java's <c>TenantFilters.runUnfiltered</c>. Reserve this for deliberate,
    /// audited cross-company/cross-branch admin or consolidation reads.
    /// </summary>
    Task<TResult> RunUnfilteredAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
