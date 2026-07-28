using AidlyErp.Pur.Application.Interfaces;
using AidlyErp.Pur.Domain;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Pur.Infrastructure;

public class PurDbContext : ModuleDbContext, IPurDbContext
{
    public PurDbContext(DbContextOptions<PurDbContext> options, ICompanyBranchContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<PurSupplier> PurSuppliers => Set<PurSupplier>();
    public DbSet<PurSupplierProduct> PurSupplierProducts => Set<PurSupplierProduct>();
    public DbSet<PurSupplierLedger> PurSupplierLedgers => Set<PurSupplierLedger>();
    public DbSet<PurOrder> PurOrders => Set<PurOrder>();
    public DbSet<PurOrderDtl> PurOrderDtls => Set<PurOrderDtl>();
    public DbSet<PurReceipt> PurReceipts => Set<PurReceipt>();
    public DbSet<PurReceiptDtl> PurReceiptDtls => Set<PurReceiptDtl>();
    public DbSet<PurInvoice> PurInvoices => Set<PurInvoice>();
    public DbSet<PurInvoiceDtl> PurInvoiceDtls => Set<PurInvoiceDtl>();
    public DbSet<PurReturn> PurReturns => Set<PurReturn>();
    public DbSet<PurReturnDtl> PurReturnDtls => Set<PurReturnDtl>();
    public DbSet<PurPayment> PurPayments => Set<PurPayment>();
    public DbSet<PurPaymentAlloc> PurPaymentAllocs => Set<PurPaymentAlloc>();
    public DbSet<PurLandedCost> PurLandedCosts => Set<PurLandedCost>();
    public DbSet<PurLandedCostAlloc> PurLandedCostAllocs => Set<PurLandedCostAlloc>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurSupplier>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.SupplierCode, e.IsDeleted }).HasDatabaseName("idx_pur_sup_code_deleted");
        });

        modelBuilder.Entity<PurOrder>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.SupplierNo, e.Status }).HasDatabaseName("idx_pur_order_sup_status");
        });
    }
}
