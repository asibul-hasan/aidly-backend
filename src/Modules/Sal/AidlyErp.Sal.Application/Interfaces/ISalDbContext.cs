using AidlyErp.Shared.Core;
using AidlyErp.Sal.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sal.Application.Interfaces;

/// <summary>Persistence surface of the Sal module. Only Sal entities are reachable —
/// the compile-time expression of the isolated-schema rule.</summary>
public interface ISalDbContext
{
    DbSet<SalCustomer> SalCustomers { get; }
    DbSet<SalCustomerLedger> SalCustomerLedgers { get; }
    DbSet<SalInvoice> SalInvoices { get; }
    DbSet<SalInvoiceDtl> SalInvoiceDtls { get; }
    DbSet<SalReceipt> SalReceipts { get; }
    DbSet<SalReceiptAlloc> SalReceiptAllocs { get; }
    DbSet<SalReturn> SalReturns { get; }
    DbSet<SalReturnDtl> SalReturnDtls { get; }

    DbSet<AidlyErp.Shared.Core.EventOutbox> EventOutboxes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Raw-SQL surface: several queries are ported verbatim from Java
    /// <c>@Query(nativeQuery = true)</c> definitions whose GROUP BY / NULLS LAST
    /// semantics would risk silent drift if re-expressed in LINQ.</summary>
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
}
