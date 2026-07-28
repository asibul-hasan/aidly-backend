using AidlyErp.Pur.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Pur.Application.Interfaces;

/// <summary>Persistence surface of the Pur module. Only Pur entities are reachable —
/// the compile-time expression of the isolated-schema rule.</summary>
public interface IPurDbContext
{
    DbSet<PurSupplier> PurSuppliers { get; }
    DbSet<PurSupplierProduct> PurSupplierProducts { get; }
    DbSet<PurSupplierLedger> PurSupplierLedgers { get; }
    DbSet<PurOrder> PurOrders { get; }
    DbSet<PurOrderDtl> PurOrderDtls { get; }
    DbSet<PurReceipt> PurReceipts { get; }
    DbSet<PurReceiptDtl> PurReceiptDtls { get; }
    DbSet<PurInvoice> PurInvoices { get; }
    DbSet<PurInvoiceDtl> PurInvoiceDtls { get; }
    DbSet<PurReturn> PurReturns { get; }
    DbSet<PurReturnDtl> PurReturnDtls { get; }
    DbSet<PurPayment> PurPayments { get; }
    DbSet<PurPaymentAlloc> PurPaymentAllocs { get; }
    DbSet<PurLandedCost> PurLandedCosts { get; }
    DbSet<PurLandedCostAlloc> PurLandedCostAllocs { get; }

    DbSet<AidlyErp.Shared.Core.EventOutbox> EventOutboxes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Raw-SQL surface: several queries are ported verbatim from Java
    /// <c>@Query(nativeQuery = true)</c> definitions whose GROUP BY / NULLS LAST
    /// semantics would risk silent drift if re-expressed in LINQ.</summary>
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
}
