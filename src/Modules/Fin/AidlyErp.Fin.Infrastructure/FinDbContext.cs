using AidlyErp.Fin.Application.Interfaces;
using AidlyErp.Fin.Domain;
using AidlyErp.Sys.Domain;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Fin.Infrastructure;

public class FinDbContext : ModuleDbContext, IFinDbContext
{
    public FinDbContext(DbContextOptions<FinDbContext> options, ICompanyBranchContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<FinAccount> FinAccounts => Set<FinAccount>();
    public DbSet<FinAccountGroup> FinAccountGroups => Set<FinAccountGroup>();
    public DbSet<FinAccountBalance> FinAccountBalances => Set<FinAccountBalance>();
    public DbSet<FinGlMap> FinGlMaps => Set<FinGlMap>();
    public DbSet<FinVoucher> FinVouchers => Set<FinVoucher>();
    public DbSet<FinVoucherDtl> FinVoucherDtls => Set<FinVoucherDtl>();
    public DbSet<FinVoucherType> FinVoucherTypes => Set<FinVoucherType>();
    public DbSet<FinLedger> FinLedgers => Set<FinLedger>();
    public DbSet<FinBankAccount> FinBankAccounts => Set<FinBankAccount>();
    public DbSet<FinBankRecon> FinBankRecons => Set<FinBankRecon>();
    public DbSet<FinBankReconLine> FinBankReconLines => Set<FinBankReconLine>();

    public DbSet<Currency> Currencies => Set<Currency>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinAccount>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.IsDeleted }).HasDatabaseName("idx_fin_acc_company_deleted");
            entity.HasIndex(e => new { e.AccountGroupNo, e.IsDeleted }).HasDatabaseName("idx_fin_acc_group");
        });

        modelBuilder.Entity<FinVoucher>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.FinPeriodNo, e.Status }).HasDatabaseName("idx_fin_voucher_period");
            entity.HasIndex(e => new { e.CompanyNo, e.BranchNo, e.VoucherDate }).HasDatabaseName("idx_fin_voucher_date");
        });

        modelBuilder.Entity<FinLedger>(entity =>
        {
            entity.HasIndex(e => new { e.AccountNo, e.VoucherDate }).HasDatabaseName("idx_fin_ledger_account_date");
            entity.HasIndex(e => new { e.VoucherNo }).HasDatabaseName("idx_fin_ledger_voucher");
        });
    }
}
