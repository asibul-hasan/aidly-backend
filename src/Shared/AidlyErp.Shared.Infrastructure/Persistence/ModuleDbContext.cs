using AidlyErp.Shared.Core;
using AidlyErp.Shared.Core.Audit;
using AidlyErp.Shared.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Shared.Infrastructure.Persistence;

/// <summary>
/// Base for every module's <c>DbContext</c>. Each business module owns its own context and
/// therefore its own slice of the model; this base supplies only the cross-cutting concerns that
/// must behave identically everywhere — multi-tenancy, soft delete, the audit trail and the
/// transactional outbox.
///
/// Splitting the context per module is what actually enforces the isolation rule: a Sales handler
/// cannot reach an Inventory table, because the Inventory entities are not in the Sales model at
/// all. It is a compile-time boundary, not a convention.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    private readonly ICompanyBranchContext _tenantContext;

    protected ModuleDbContext(DbContextOptions options, ICompanyBranchContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// The audit trail is written by <see cref="AuditingAndTenantInterceptor"/>, which runs on
    /// every module context — so every module maps the table.
    /// </summary>
    public DbSet<SysAuditLog> SysAuditLogs => Set<SysAuditLog>();

    /// <summary>
    /// Transactional outbox. Mapped into every module context deliberately: a module must be able
    /// to write its integration events in the *same* transaction as its own state change. Sharing
    /// one physical table across contexts keeps a single drain worker while preserving that
    /// atomicity — the alternative (writing through another module's context) would open a second
    /// transaction and lose it.
    /// </summary>
    public DbSet<EventOutbox> EventOutboxes => Set<EventOutbox>();

    // ---------------------------------------------------------------------
    // Query-filter inputs. EF turns references to these into query parameters, so each request is
    // scoped by its own tenant context even though the compiled model is cached process-wide.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Bypasses the tenant and soft-delete filters for the duration of an
    /// <c>IUnitOfWork.RunUnfilteredAsync</c> block — the counterpart of Java's
    /// <c>TenantFilters.runUnfiltered</c>. Never set this outside such a block.
    /// </summary>
    public bool SuppressQueryFilters { get; set; }

    /// <summary>Active company; <c>null</c> on unauthenticated/public requests, which disables the tenant predicate.</summary>
    private long? TenantCompanyNo => _tenantContext.CompanyNo;

    /// <summary>Active branch. <c>-1</c> sentinel: branch unknown but scope is branch-level, so only branch-agnostic rows match.</summary>
    private long TenantBranchNo => _tenantContext.BranchNo ?? -1L;

    /// <summary>COMPANY/GLOBAL scope drops the branch predicate entirely.</summary>
    private bool TenantCompanyWide => _tenantContext.IsCompanyWide;

    private static readonly System.Reflection.MethodInfo TenantAndSoftDeleteFilterMethod =
        typeof(ModuleDbContext).GetMethod(nameof(ApplyTenantAndSoftDeleteFilter),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

    private static readonly System.Reflection.MethodInfo SoftDeleteFilterMethod =
        typeof(ModuleDbContext).GetMethod(nameof(ApplySoftDeleteFilter),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

    private void ApplyTenantAndSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDelete, IMultiTenantEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            SuppressQueryFilters
            || (e.IsDeleted == 0
                && (TenantCompanyNo == null
                    || (e.CompanyNo == TenantCompanyNo
                        && (TenantCompanyWide || e.BranchNo == null || e.BranchNo == TenantBranchNo)))));
    }

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDelete
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => SuppressQueryFilters || e.IsDeleted == 0);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------------------------------------------------------------------
        // Global query filters — soft delete + multi-tenancy
        //
        // Tenant predicate mirrors the Hibernate @Filter on the Java BaseEntity
        // (SYS re-plan §3.4, "the cardinal rule"):
        //     company_no = :companyNo
        //     AND (:companyScope = true OR branch_no IS NULL OR branch_no = :branchNo)
        //
        // Rows of the active company are kept, and — unless the session is COMPANY/GLOBAL
        // scoped — only the active branch plus branch-agnostic rows (branch_no IS NULL, e.g. a
        // central warehouse).
        // ---------------------------------------------------------------------
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            var isSoftDelete = typeof(ISoftDelete).IsAssignableFrom(clr);
            // Implementing IMultiTenantEntity is not enough — the tenant predicate reads
            // CompanyNo, so the property must actually be mapped to a column. HRM entities
            // implement the interface but are branch-scoped only (no company_no in the schema),
            // and EF cannot translate a filter over an unmapped member.
            var isMultiTenant = typeof(IMultiTenantEntity).IsAssignableFrom(clr)
                                && entityType.FindProperty(nameof(IMultiTenantEntity.CompanyNo)) != null;

            if (isSoftDelete && isMultiTenant)
            {
                TenantAndSoftDeleteFilterMethod.MakeGenericMethod(clr).Invoke(this, [modelBuilder]);
            }
            else if (isSoftDelete)
            {
                SoftDeleteFilterMethod.MakeGenericMethod(clr).Invoke(this, [modelBuilder]);
            }
        }

        ConfigureModule(modelBuilder);
    }

    /// <summary>Module-specific mapping: indexes, constraints, <c>IEntityTypeConfiguration</c>s.</summary>
    protected abstract void ConfigureModule(ModelBuilder modelBuilder);
}
