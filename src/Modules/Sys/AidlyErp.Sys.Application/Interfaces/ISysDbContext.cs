using AidlyErp.Shared.Core.Audit;
using AidlyErp.Shared.Core;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Sys.Application.Interfaces;

/// <summary>Persistence surface of the Sys module. Only Sys entities are reachable —
/// the compile-time expression of the isolated-schema rule.</summary>
public interface ISysDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<Branch> Branches { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserBranch> UserBranches { get; }
    DbSet<SysSession> SysSessions { get; }
    DbSet<SysLog> SysLogs { get; }
    DbSet<Currency> Currencies { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    DbSet<FinYear> FinYears { get; }
    DbSet<FinYearDtl> FinYearDtls { get; }
    DbSet<VatTax> VatTaxes { get; }
    DbSet<CostCenter> CostCenters { get; }
    DbSet<DocSequence> DocSequences { get; }
    DbSet<Menu> Menus { get; }
    DbSet<EnrollMenu> EnrollMenus { get; }
    DbSet<SysModule> SysModules { get; }
    DbSet<SysSubmodule> SysSubmodules { get; }
    DbSet<SysFile> SysFiles { get; }
    DbSet<Setting> Settings { get; }
    DbSet<ApprovalStep> ApprovalSteps { get; }
    DbSet<StepApprover> StepApprovers { get; }
    DbSet<ApprovalScope> ApprovalScopes { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<ApprovalRequestStep> ApprovalRequestSteps { get; }
    DbSet<LoginAttempt> LoginAttempts { get; }
    DbSet<PlatformAdmin> PlatformAdmins { get; }
    DbSet<SysRoleBranch> SysRoleBranches { get; }
    DbSet<UserCompany> UserCompanies { get; }
    DbSet<UserWarehouse> UserWarehouses { get; }
    DbSet<Department> Departments { get; }

    DbSet<AidlyErp.Shared.Core.EventOutbox> EventOutboxes { get; }

    DbSet<AidlyErp.Shared.Core.Audit.SysAuditLog> SysAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Raw-SQL surface: several queries are ported verbatim from Java
    /// <c>@Query(nativeQuery = true)</c> definitions whose GROUP BY / NULLS LAST
    /// semantics would risk silent drift if re-expressed in LINQ.</summary>
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
}
