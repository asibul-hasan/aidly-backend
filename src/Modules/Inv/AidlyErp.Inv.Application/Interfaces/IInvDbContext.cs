using AidlyErp.Shared.Core;
using AidlyErp.Inv.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Application.Interfaces;

/// <summary>Persistence surface of the Inv module. Only Inv entities are reachable —
/// the compile-time expression of the isolated-schema rule.</summary>
public interface IInvDbContext
{
    DbSet<InvProduct> InvProducts { get; }
    DbSet<InvCategory> InvCategories { get; }
    DbSet<InvBrand> InvBrands { get; }
    DbSet<InvUom> InvUoms { get; }
    DbSet<InvUomConversion> InvUomConversions { get; }
    DbSet<InvProductAttribute> InvProductAttributes { get; }
    DbSet<InvProductAttributeValue> InvProductAttributeValues { get; }
    DbSet<InvProductBarcode> InvProductBarcodes { get; }
    DbSet<InvProductVariant> InvProductVariants { get; }
    DbSet<InvWarehouse> InvWarehouses { get; }
    DbSet<InvRack> InvRacks { get; }
    DbSet<InvStock> InvStocks { get; }
    DbSet<InvStockLedger> InvStockLedgers { get; }
    DbSet<InvBatch> InvBatches { get; }
    DbSet<InvReorder> InvReorders { get; }
    DbSet<InvValuationLayer> InvValuationLayers { get; }
    DbSet<InvStockAdjustment> InvStockAdjustments { get; }
    DbSet<InvStockAdjustmentDtl> InvStockAdjustmentDtls { get; }
    DbSet<InvStockTransfer> InvStockTransfers { get; }
    DbSet<InvStockTransferDtl> InvStockTransferDtls { get; }
    DbSet<InvPhysicalCount> InvPhysicalCounts { get; }
    DbSet<InvPhysicalCountDtl> InvPhysicalCountDtls { get; }

    DbSet<AidlyErp.Shared.Core.EventOutbox> EventOutboxes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Raw-SQL surface: several queries are ported verbatim from Java
    /// <c>@Query(nativeQuery = true)</c> definitions whose GROUP BY / NULLS LAST
    /// semantics would risk silent drift if re-expressed in LINQ.</summary>
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
}
