using AidlyErp.Sal.Application.Interfaces;
using AidlyErp.Sal.Domain;
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
