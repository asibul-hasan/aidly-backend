using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Core;

namespace AidlyErp.Shared.Infrastructure.Persistence;

public class AuditingAndTenantInterceptor : SaveChangesInterceptor
{
    private readonly ICompanyBranchContext _tenantContext;

    public AuditingAndTenantInterceptor(ICompanyBranchContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditAndTenantFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditAndTenantFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void UpdateAuditAndTenantFields(DbContext? context)
    {
        if (context == null) return;

        var now = DateTime.UtcNow;
        var currentUserId = _tenantContext.UserNo;
        var currentCompanyNo = _tenantContext.CompanyNo;
        var currentBranchNo = _tenantContext.BranchNo;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditEntity auditEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    auditEntity.CreatedAt = now;
                    auditEntity.CreatedBy ??= currentUserId;
                    auditEntity.IsActive = 1;
                    auditEntity.IsDeleted = 0;
                    // Java: @PrePersist initVersion() — an INSERT must land at version 1 so the
                    // optimistic-lock WHERE clause never targets version 0.
                    (entry.Entity as AuditEntity)?.InitVersion();
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditEntity.UpdatedAt = now;
                    auditEntity.UpdatedBy = currentUserId;
                    auditEntity.RowVersion++;
                }
                else if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete softDelete)
                {
                    entry.State = EntityState.Modified;
                    softDelete.IsDeleted = 1;
                    softDelete.DeletedAt = now;
                    softDelete.DeletedBy = currentUserId;
                    auditEntity.IsActive = 0;
                    auditEntity.UpdatedAt = now;
                    auditEntity.UpdatedBy = currentUserId;
                }
            }

            if (entry.State == EntityState.Added)
            {
                // IMultiTenantEntity is checked first because BaseEntity implements it directly —
                // without this branch every BaseEntity descendant would be inserted with a null
                // company_no/branch_no and then be invisible to the tenant query filter.
                if (entry.Entity is IMultiTenantEntity multiTenant)
                {
                    if (!multiTenant.CompanyNo.HasValue || multiTenant.CompanyNo == 0)
                        multiTenant.CompanyNo = currentCompanyNo;
                    if (!multiTenant.BranchNo.HasValue || multiTenant.BranchNo == 0)
                        multiTenant.BranchNo = currentBranchNo;
                }
                else if (entry.Entity is ICompanyScopedEntity companyScoped
                         && (!companyScoped.CompanyNo.HasValue || companyScoped.CompanyNo == 0))
                {
                    companyScoped.CompanyNo = currentCompanyNo;
                }
            }
        }
    }
}
