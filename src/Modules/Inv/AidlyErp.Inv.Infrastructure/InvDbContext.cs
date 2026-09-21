using AidlyErp.Inv.Application.Interfaces;
using AidlyErp.Inv.Domain;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Inv.Infrastructure;

public class InvDbContext : ModuleDbContext, IInvDbContext
{
    public InvDbContext(DbContextOptions<InvDbContext> options, ICompanyBranchContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<InvProduct> InvProducts => Set<InvProduct>();
    public DbSet<InvCategory> InvCategories => Set<InvCategory>();
    public DbSet<InvBrand> InvBrands => Set<InvBrand>();
    public DbSet<InvUom> InvUoms => Set<InvUom>();
    public DbSet<InvUomConversion> InvUomConversions => Set<InvUomConversion>();
    public DbSet<InvProductAttribute> InvProductAttributes => Set<InvProductAttribute>();
    public DbSet<InvProductAttributeValue> InvProductAttributeValues => Set<InvProductAttributeValue>();
    public DbSet<InvProductBarcode> InvProductBarcodes => Set<InvProductBarcode>();
    public DbSet<InvProductVariant> InvProductVariants => Set<InvProductVariant>();
    public DbSet<InvWarehouse> InvWarehouses => Set<InvWarehouse>();
    public DbSet<InvRack> InvRacks => Set<InvRack>();
    public DbSet<InvStock> InvStocks => Set<InvStock>();
    public DbSet<InvStockLedger> InvStockLedgers => Set<InvStockLedger>();
    public DbSet<InvBatch> InvBatches => Set<InvBatch>();
    public DbSet<InvReorder> InvReorders => Set<InvReorder>();
    public DbSet<InvValuationLayer> InvValuationLayers => Set<InvValuationLayer>();
    public DbSet<InvStockAdjustment> InvStockAdjustments => Set<InvStockAdjustment>();
    public DbSet<InvStockAdjustmentDtl> InvStockAdjustmentDtls => Set<InvStockAdjustmentDtl>();
    public DbSet<InvStockTransfer> InvStockTransfers => Set<InvStockTransfer>();
    public DbSet<InvStockTransferDtl> InvStockTransferDtls => Set<InvStockTransferDtl>();
    public DbSet<InvPhysicalCount> InvPhysicalCounts => Set<InvPhysicalCount>();
    public DbSet<InvPhysicalCountDtl> InvPhysicalCountDtls => Set<InvPhysicalCountDtl>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvProduct>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.ProductId, e.IsDeleted }).HasDatabaseName("idx_inv_prod_code_deleted");
            entity.HasIndex(e => new { e.CategoryNo, e.IsDeleted }).HasDatabaseName("idx_inv_prod_cat_deleted");
        });

        modelBuilder.Entity<InvStock>(entity =>
        {
            entity.HasIndex(e => new { e.WarehouseNo, e.ProductNo }).HasDatabaseName("idx_inv_stock_wh_prod");
        });

        modelBuilder.Entity<InvStockLedger>(entity =>
        {
            entity.HasIndex(e => new { e.ProductNo, e.WarehouseNo, e.MovementDate }).HasDatabaseName("idx_inv_ledger_prod_wh_date");
        });
    }
}
