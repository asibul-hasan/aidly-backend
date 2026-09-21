using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Sal.Domain;
using AidlyErp.Sys.Domain;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sal.Infrastructure;

public class SalDbContext : ModuleDbContext, ISalDbContext
{
    public SalDbContext(DbContextOptions<SalDbContext> options, ICompanyBranchContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<SalCustomer> SalCustomers => Set<SalCustomer>();
    public DbSet<SalCustomerLedger> SalCustomerLedgers => Set<SalCustomerLedger>();
    public DbSet<SalInvoice> SalInvoices => Set<SalInvoice>();
    public DbSet<SalInvoiceDtl> SalInvoiceDtls => Set<SalInvoiceDtl>();
    public DbSet<SalReceipt> SalReceipts => Set<SalReceipt>();
    public DbSet<SalReceiptAlloc> SalReceiptAllocs => Set<SalReceiptAlloc>();
    public DbSet<SalReturn> SalReturns => Set<SalReturn>();
    public DbSet<SalReturnDtl> SalReturnDtls => Set<SalReturnDtl>();
    public DbSet<SalPosTerminal> SalPosTerminals => Set<SalPosTerminal>();
    public DbSet<SalPosSession> SalPosSessions => Set<SalPosSession>();
    public DbSet<SalInvoicePayment> SalInvoicePayments => Set<SalInvoicePayment>();
    public DbSet<SalCustomerGroup> SalCustomerGroups => Set<SalCustomerGroup>();
    public DbSet<SalPromotion> SalPromotions => Set<SalPromotion>();
    public DbSet<SalPromotionDtl> SalPromotionDtls => Set<SalPromotionDtl>();

    public DbSet<VatTax> VatTaxes => Set<VatTax>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SalCustomer>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.CustomerId, e.IsDeleted }).HasDatabaseName("idx_sal_cust_code_deleted");
        });

        modelBuilder.Entity<SalInvoice>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.CustomerNo, e.Status }).HasDatabaseName("idx_sal_inv_cust_status");
        });
    }
}
