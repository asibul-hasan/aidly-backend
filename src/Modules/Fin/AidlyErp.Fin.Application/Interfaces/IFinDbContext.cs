using AidlyErp.Shared.Core;
using AidlyErp.Fin.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Fin.Application.Interfaces;

/// <summary>Persistence surface of the Fin module. Only Fin entities are reachable —
/// the compile-time expression of the isolated-schema rule.</summary>
public interface IFinDbContext
{
    DbSet<FinAccount> FinAccounts { get; }
    DbSet<FinAccountGroup> FinAccountGroups { get; }
    DbSet<FinAccountBalance> FinAccountBalances { get; }
    DbSet<FinGlMap> FinGlMaps { get; }
    DbSet<FinVoucher> FinVouchers { get; }
    DbSet<FinVoucherDtl> FinVoucherDtls { get; }
    DbSet<FinVoucherType> FinVoucherTypes { get; }
    DbSet<FinLedger> FinLedgers { get; }
    DbSet<FinBankAccount> FinBankAccounts { get; }
    DbSet<FinBankRecon> FinBankRecons { get; }
    DbSet<FinBankReconLine> FinBankReconLines { get; }

    DbSet<AidlyErp.Shared.Core.EventOutbox> EventOutboxes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Raw-SQL surface: several queries are ported verbatim from Java
    /// <c>@Query(nativeQuery = true)</c> definitions whose GROUP BY / NULLS LAST
    /// semantics would risk silent drift if re-expressed in LINQ.</summary>
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
}
