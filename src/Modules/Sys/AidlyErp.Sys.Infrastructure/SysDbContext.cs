using AidlyErp.Shared.Core.Audit;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Sys.Domain;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sys.Infrastructure;

public class SysDbContext : ModuleDbContext, ISysDbContext
{
    public SysDbContext(DbContextOptions<SysDbContext> options, ICompanyBranchContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<SysSession> SysSessions => Set<SysSession>();
    public DbSet<SysLog> SysLogs => Set<SysLog>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<FinYear> FinYears => Set<FinYear>();
    public DbSet<FinYearDtl> FinYearDtls => Set<FinYearDtl>();
    public DbSet<VatTax> VatTaxes => Set<VatTax>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<DocSequence> DocSequences => Set<DocSequence>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<EnrollMenu> EnrollMenus => Set<EnrollMenu>();
    public DbSet<SysModule> SysModules => Set<SysModule>();
    public DbSet<SysSubmodule> SysSubmodules => Set<SysSubmodule>();
    public DbSet<SysFile> SysFiles => Set<SysFile>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<StepApprover> StepApprovers => Set<StepApprover>();
    public DbSet<ApprovalScope> ApprovalScopes => Set<ApprovalScope>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalRequestStep> ApprovalRequestSteps => Set<ApprovalRequestStep>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<PlatformAdmin> PlatformAdmins => Set<PlatformAdmin>();
    public DbSet<SysRoleBranch> SysRoleBranches => Set<SysRoleBranch>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();
    public DbSet<UserWarehouse> UserWarehouses => Set<UserWarehouse>();
    public DbSet<Department> Departments => Set<Department>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        // ---------------------------------------------------------------------
        // Audit columns these tables do not actually have.
        //
        // Every SYS entity inherits AuditEntity in C#, which maps eleven audit columns. The Java
        // entities are selective: sys_approval_request and sys_doc_sequence carry none of them,
        // sys_exchange_rate has no update/delete pair, sys_platform_admin no delete pair. Mapping
        // them made every query on those tables fail with 42703, so they are ignored here rather
        // than by weakening the shared base class.
        // ---------------------------------------------------------------------
        modelBuilder.Entity<ApprovalRequest>(e =>
        {
            e.Ignore(x => x.IsActive); e.Ignore(x => x.IsDeleted);
            e.Ignore(x => x.CreatedBy); e.Ignore(x => x.CreatedAt);
            e.Ignore(x => x.UpdatedBy); e.Ignore(x => x.UpdatedAt);
            e.Ignore(x => x.DeletedBy); e.Ignore(x => x.DeletedAt);
        });

        modelBuilder.Entity<DocSequence>(e =>
        {
            e.Ignore(x => x.IsActive); e.Ignore(x => x.IsDeleted);
            e.Ignore(x => x.CreatedBy); e.Ignore(x => x.CreatedAt);
            e.Ignore(x => x.UpdatedBy); e.Ignore(x => x.UpdatedAt);
            e.Ignore(x => x.DeletedBy); e.Ignore(x => x.DeletedAt);
        });

        modelBuilder.Entity<ExchangeRate>(e =>
        {
            e.Ignore(x => x.UpdatedBy); e.Ignore(x => x.UpdatedAt);
            e.Ignore(x => x.DeletedBy); e.Ignore(x => x.DeletedAt);
        });

        modelBuilder.Entity<PlatformAdmin>(e =>
        {
            e.Ignore(x => x.DeletedBy); e.Ignore(x => x.DeletedAt);
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyId, e.IsDeleted }).HasDatabaseName("idx_company_id_deleted");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.IsDeleted }).HasDatabaseName("idx_branch_company_deleted");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.IsDeleted }).HasDatabaseName("idx_user_userid_deleted");
            entity.HasIndex(e => new { e.CompanyNo, e.IsDeleted }).HasDatabaseName("idx_user_company_deleted");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.IsDeleted }).HasDatabaseName("idx_role_company_deleted");
        });
    }
}
