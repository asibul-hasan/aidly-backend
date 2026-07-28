using AidlyErp.Shared.Core;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Audit;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Shared.Infrastructure.Persistence.Repositories;

// ═══════════════════════════════════════════════════════════════════════════
// SYS Repository Implementations — EF Core translations of the Spring Data repos.
//
// Company/branch-scoped tables also carry an EF global query filter, so these
// deliberately use IgnoreQueryFilters() where the Java repository is explicitly
// cross-tenant (company and branch administration, session and file lookups).
// ═══════════════════════════════════════════════════════════════════════════

// ── Company / Branch ──────────────────────────────────────────────────────

public class CompanyRepository : ICompanyRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public CompanyRepository(ISysDbContext db) => _db = db;

    private IQueryable<Company> Live => _db.Companies.IgnoreQueryFilters().Where(x => x.IsDeleted == Deleted);

    public async Task<List<Company>> FindAllAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking().OrderBy(x => x.CompanyNo).ToListAsync(ct);

    public async Task<List<Company>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await Live.AsNoTracking().OrderBy(x => x.CompanyNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<Company?> FindByIdAsync(long companyNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.CompanyNo == companyNo, ct);

    public async Task<List<Company>> FindByIdsAsync(IReadOnlyCollection<long> companyNos, CancellationToken ct = default) =>
        companyNos.Count == 0
            ? new List<Company>()
            : await Live.AsNoTracking().Where(x => companyNos.Contains(x.CompanyNo)).ToListAsync(ct);

    public async Task<Company?> FindByCompanyIdAsync(string companyId, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.CompanyId == companyId, ct);

    public async Task<bool> ExistsByCompanyIdAsync(string companyId, CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.CompanyId == companyId, ct);

    public void Add(Company entity) => _db.Companies.Add(entity);
}

public class BranchRepository : IBranchRepository
{
    private const short Active = 1, Deleted = 0;
    private readonly ISysDbContext _db;
    public BranchRepository(ISysDbContext db) => _db = db;

    private IQueryable<Branch> Live => _db.Branches.IgnoreQueryFilters().Where(x => x.IsDeleted == Deleted);

    public async Task<List<Branch>> FindAllAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking().OrderBy(x => x.BranchNo).ToListAsync(ct);

    public async Task<List<Branch>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await Live.AsNoTracking().OrderBy(x => x.BranchNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<Branch?> FindByIdAsync(long branchNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.BranchNo == branchNo, ct);

    public async Task<List<Branch>> FindByIdsAsync(IReadOnlyCollection<long> branchNos, CancellationToken ct = default) =>
        branchNos.Count == 0
            ? new List<Branch>()
            : await Live.AsNoTracking().Where(x => branchNos.Contains(x.BranchNo)).ToListAsync(ct);

    public async Task<List<Branch>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.CompanyNo == companyNo).OrderBy(x => x.BranchNo).ToListAsync(ct);

    public async Task<List<Branch>> FindByCompanyPaginatedAsync(long companyNo, int skip, int take,
                                                                CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.CompanyNo == companyNo).OrderBy(x => x.BranchNo)
            .Skip(skip).Take(take).ToListAsync(ct);

    public async Task<Branch?> FindByCompanyAndBranchIdAsync(long companyNo, string branchId,
                                                             CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.CompanyNo == companyNo && x.BranchId == branchId, ct);

    public async Task<bool> ExistsByCompanyAndBranchIdAsync(long companyNo, string branchId,
                                                            CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.CompanyNo == companyNo && x.BranchId == branchId, ct);

    public async Task<Branch?> FindMainBranchAsync(long companyNo, CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyNo == companyNo && x.IsMainBranch == Active, ct);

    public void Add(Branch entity) => _db.Branches.Add(entity);
}

// ── User / Role / access ──────────────────────────────────────────────────

public class UserRepository : IUserRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public UserRepository(ISysDbContext db) => _db = db;

    private IQueryable<User> Live => _db.Users.Where(x => x.IsDeleted == Deleted);

    public async Task<List<User>> FindAllAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking().OrderBy(x => x.UserId).ToListAsync(ct);

    public async Task<User?> FindByIdAsync(long userNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.UserNo == userNo, ct);

    public async Task<User?> FindByUserIdAsync(string userId, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task<bool> ExistsByUserIdAsync(string userId, CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.UserId == userId, ct);

    public async Task<User?> FindByEmployeeNoAsync(long employeeNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.EmployeeNo == employeeNo, ct);

    public async Task<bool> ExistsByEmployeeNoAsync(long employeeNo, CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.EmployeeNo == employeeNo, ct);

    public async Task<List<User>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.CompanyNo == companyNo).OrderBy(x => x.UserId).ToListAsync(ct);

    public void Add(User entity) => _db.Users.Add(entity);
}

public class RoleRepository : IRoleRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public RoleRepository(ISysDbContext db) => _db = db;

    private IQueryable<Role> Live => _db.Roles.Where(x => x.IsDeleted == Deleted);

    public async Task<List<Role>> FindAllAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking().OrderBy(x => x.RoleNo).ToListAsync(ct);

    public async Task<List<Role>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default) =>
        await Live.AsNoTracking().OrderBy(x => x.RoleNo).Skip(skip).Take(take).ToListAsync(ct);

    public async Task<Role?> FindByIdAsync(long roleNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.RoleNo == roleNo, ct);

    public async Task<List<Role>> FindByIdsAsync(IReadOnlyCollection<long> roleNos, CancellationToken ct = default) =>
        roleNos.Count == 0
            ? new List<Role>()
            : await Live.AsNoTracking().Where(x => roleNos.Contains(x.RoleNo)).ToListAsync(ct);

    public async Task<List<Role>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.CompanyNo == companyNo).OrderBy(x => x.RoleNo).ToListAsync(ct);

    public async Task<List<Role>> FindByCompanyPaginatedAsync(long companyNo, int skip, int take,
                                                              CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.CompanyNo == companyNo).OrderBy(x => x.RoleNo)
            .Skip(skip).Take(take).ToListAsync(ct);

    public async Task<Role?> FindByCompanyAndRoleIdAsync(long companyNo, string roleId,
                                                         CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.CompanyNo == companyNo && x.RoleId == roleId, ct);

    public async Task<bool> ExistsByCompanyAndRoleIdAsync(long companyNo, string roleId,
                                                          CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.CompanyNo == companyNo && x.RoleId == roleId, ct);

    public void Add(Role entity) => _db.Roles.Add(entity);
}

public class UserBranchRepository : IUserBranchRepository
{
    private const short Active = 1, Deleted = 0;
    private readonly ISysDbContext _db;
    public UserBranchRepository(ISysDbContext db) => _db = db;

    private IQueryable<UserBranch> Live => _db.UserBranches.Where(x => x.IsDeleted == Deleted);

    public async Task<List<UserBranch>> FindByUserAsync(long userNo, CancellationToken ct = default) =>
        await Live.Where(x => x.UserNo == userNo).ToListAsync(ct);

    public async Task<List<UserBranch>> FindAllForUserIncludingDeletedAsync(long userNo,
                                                                            CancellationToken ct = default) =>
        await _db.UserBranches.IgnoreQueryFilters().Where(x => x.UserNo == userNo).ToListAsync(ct);

    public async Task<List<UserBranch>> FindActiveByUserAsync(long userNo, CancellationToken ct = default) =>
        await Live.Where(x => x.UserNo == userNo && x.IsActive == Active).ToListAsync(ct);

    public async Task<List<UserBranch>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await Live.Where(x => x.BranchNo == branchNo && x.IsActive == Active).ToListAsync(ct);

    public async Task<UserBranch?> FindByUserAndBranchAsync(long userNo, long branchNo,
                                                            CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.UserNo == userNo && x.BranchNo == branchNo, ct);

    public async Task<UserBranch?> FindDefaultForUserAsync(long userNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.UserNo == userNo && x.IsDefault == Active && x.IsActive == Active, ct);

    public async Task<List<long>> FindRoleNosForUserInBranchAsync(long userNo, long branchNo,
                                                                  CancellationToken ct = default) =>
        await (from ub in _db.UserBranches.AsNoTracking()
               join r in _db.Roles.AsNoTracking() on ub.RoleNo equals r.RoleNo
               where ub.UserNo == userNo && ub.BranchNo == branchNo
                     && ub.IsActive == Active && ub.IsDeleted == Deleted
                     && r.IsActive == Active && r.IsDeleted == Deleted
               select r.RoleNo).ToListAsync(ct);

    public async Task<bool> UserHasAnyRoleInBranchAsync(long userNo, long branchNo, CancellationToken ct = default) =>
        await (from ub in _db.UserBranches.AsNoTracking()
               join r in _db.Roles.AsNoTracking() on ub.RoleNo equals r.RoleNo
               where ub.UserNo == userNo && ub.BranchNo == branchNo
                     && ub.IsActive == Active && ub.IsDeleted == Deleted
                     && r.IsActive == Active && r.IsDeleted == Deleted
               select ub.UserBranchNo).AnyAsync(ct);

    public void Add(UserBranch entity) => _db.UserBranches.Add(entity);
}

public class UserCompanyRepository : IUserCompanyRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public UserCompanyRepository(ISysDbContext db) => _db = db;

    public async Task<List<UserCompany>> FindByUserAsync(long userNo, CancellationToken ct = default) =>
        await _db.UserCompanies.Where(x => x.UserNo == userNo && x.IsDeleted == Deleted).ToListAsync(ct);

    public async Task<UserCompany?> FindByUserAndCompanyAsync(long userNo, long companyNo,
                                                              CancellationToken ct = default) =>
        await _db.UserCompanies.FirstOrDefaultAsync(
            x => x.UserNo == userNo && x.CompanyNo == companyNo && x.IsDeleted == Deleted, ct);

    public void Add(UserCompany entity) => _db.UserCompanies.Add(entity);
}

public class SysRoleBranchRepository : ISysRoleBranchRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public SysRoleBranchRepository(ISysDbContext db) => _db = db;

    public async Task<List<SysRoleBranch>> FindByRoleAsync(long roleNo, CancellationToken ct = default) =>
        await _db.SysRoleBranches.Where(x => x.RoleNo == roleNo && x.IsDeleted == Deleted).ToListAsync(ct);

    public async Task<List<SysRoleBranch>> FindByRolesAsync(IReadOnlyCollection<long> roleNos,
                                                            CancellationToken ct = default) =>
        roleNos.Count == 0
            ? new List<SysRoleBranch>()
            : await _db.SysRoleBranches.AsNoTracking()
                .Where(x => roleNos.Contains(x.RoleNo) && x.IsDeleted == Deleted).ToListAsync(ct);

    public void Add(SysRoleBranch entity) => _db.SysRoleBranches.Add(entity);
}

public class RolePermissionRepository : IRolePermissionRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public RolePermissionRepository(ISysDbContext db) => _db = db;

    public async Task<List<RolePermission>> FindByRoleAsync(long roleNo, CancellationToken ct = default) =>
        await _db.RolePermissions.Where(x => x.RoleNo == roleNo && x.IsDeleted == Deleted).ToListAsync(ct);

    public void Add(RolePermission entity) => _db.RolePermissions.Add(entity);
}

// ── Menu catalogue ────────────────────────────────────────────────────────

public class SysModuleRepository : ISysModuleRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public SysModuleRepository(ISysDbContext db) => _db = db;

    public async Task<List<SysModule>> FindAllAsync(CancellationToken ct = default) =>
        await _db.SysModules.AsNoTracking().Where(x => x.IsDeleted == Deleted).ToListAsync(ct);

    public async Task<SysModule?> FindByIdAsync(long moduleNo, CancellationToken ct = default) =>
        await _db.SysModules.FirstOrDefaultAsync(x => x.ModuleNo == moduleNo && x.IsDeleted == Deleted, ct);

    public async Task<bool> ExistsByModuleCodeAsync(string moduleCode, CancellationToken ct = default) =>
        await _db.SysModules.AnyAsync(x => x.ModuleCode == moduleCode && x.IsDeleted == Deleted, ct);

    public void Add(SysModule entity) => _db.SysModules.Add(entity);
}

public class SysSubmoduleRepository : ISysSubmoduleRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public SysSubmoduleRepository(ISysDbContext db) => _db = db;

    public async Task<List<SysSubmodule>> FindAllAsync(CancellationToken ct = default) =>
        await _db.SysSubmodules.AsNoTracking().Where(x => x.IsDeleted == Deleted).ToListAsync(ct);

    public async Task<SysSubmodule?> FindByIdAsync(long submoduleNo, CancellationToken ct = default) =>
        await _db.SysSubmodules.FirstOrDefaultAsync(x => x.SubmoduleNo == submoduleNo && x.IsDeleted == Deleted, ct);

    public async Task<List<SysSubmodule>> FindByModuleAsync(long moduleNo, CancellationToken ct = default) =>
        await _db.SysSubmodules.AsNoTracking()
            .Where(x => x.ModuleNo == moduleNo && x.IsDeleted == Deleted).ToListAsync(ct);

    public async Task<bool> ExistsByModuleAndCodeAsync(long moduleNo, string submoduleCode,
                                                       CancellationToken ct = default) =>
        await _db.SysSubmodules.AnyAsync(
            x => x.ModuleNo == moduleNo && x.SubmoduleCode == submoduleCode && x.IsDeleted == Deleted, ct);

    public void Add(SysSubmodule entity) => _db.SysSubmodules.Add(entity);
}

public class MenuRepository : IMenuRepository
{
    private const short Active = 1, Deleted = 0;
    private readonly ISysDbContext _db;
    public MenuRepository(ISysDbContext db) => _db = db;

    public async Task<List<Menu>> FindAllAsync(CancellationToken ct = default) =>
        await _db.Menus.AsNoTracking().Where(x => x.IsDeleted == Deleted).ToListAsync(ct);

    public async Task<Menu?> FindByIdAsync(long menuNo, CancellationToken ct = default) =>
        await _db.Menus.FirstOrDefaultAsync(x => x.MenuNo == menuNo && x.IsDeleted == Deleted, ct);

    public async Task<Menu?> FindByFormIdAsync(string formId, CancellationToken ct = default) =>
        await _db.Menus.FirstOrDefaultAsync(x => x.FormId == formId && x.IsDeleted == Deleted, ct);

    public async Task<bool> ExistsByFormIdAsync(string formId, CancellationToken ct = default) =>
        await _db.Menus.AnyAsync(x => x.FormId == formId && x.IsDeleted == Deleted, ct);

    public async Task<List<Menu>> FindBySubmoduleAsync(long submoduleNo, CancellationToken ct = default) =>
        await _db.Menus.AsNoTracking()
            .Where(x => x.SubmoduleNo == submoduleNo && x.IsDeleted == Deleted).ToListAsync(ct);

    /// <summary>
    /// Longest-<c>route_path</c>-wins, so <c>/api/v1/sys/forms/sys1005/vat-taxes</c> resolves to the
    /// SYS1005 form rather than a shorter prefix. Ported verbatim from the Java native query.
    /// </summary>
    public async Task<string?> FindBestFormIdForRequestPathAsync(string requestPath, string pathWithoutApiPrefix,
                                                                 CancellationToken ct = default) =>
        (await _db.Database.SqlQueryRaw<string?>(
            """
            SELECT m.form_id AS "Value"
            FROM   sys_menu m
            JOIN   sys_submodule s ON s.submodule_no = m.submodule_no
                                  AND s.is_active   = 1 AND s.is_deleted = 0
            JOIN   sys_module    mo ON mo.module_no = s.module_no
                                   AND mo.is_active = 1 AND mo.is_deleted = 0
            WHERE  m.form_id IS NOT NULL
              AND  m.route_path IS NOT NULL
              AND  m.is_active = 1 AND m.is_deleted = 0
              AND  ( m.route_path = {0}
                  OR m.route_path = {1}
                  OR {0} LIKE CONCAT(m.route_path, '/%')
                  OR {1} LIKE CONCAT(m.route_path, '/%') )
            ORDER BY LENGTH(m.route_path) DESC
            LIMIT 1
            """, requestPath, pathWithoutApiPrefix).ToListAsync(ct)).FirstOrDefault();

    public void Add(Menu entity) => _db.Menus.Add(entity);
}

public class EnrollMenuRepository : IEnrollMenuRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public EnrollMenuRepository(ISysDbContext db) => _db = db;

    public async Task<List<EnrollMenu>> FindCompanyWideAsync(long companyNo, CancellationToken ct = default) =>
        await _db.EnrollMenus
            .Where(x => x.CompanyNo == companyNo && x.BranchNo == null && x.IsDeleted == Deleted).ToListAsync(ct);

    public void Add(EnrollMenu entity) => _db.EnrollMenus.Add(entity);
}

// ── Financial setup ───────────────────────────────────────────────────────

public class CurrencyRepository : ICurrencyRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public CurrencyRepository(ISysDbContext db) => _db = db;

    private IQueryable<Currency> Live => _db.Currencies.Where(x => x.IsDeleted == Deleted);

    public async Task<List<Currency>> FindAllAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking().OrderBy(x => x.CurrencyNo).ToListAsync(ct);

    public async Task<Currency?> FindByIdAsync(long currencyNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.CurrencyNo == currencyNo, ct);

    public async Task<List<Currency>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.BranchNo == branchNo).OrderBy(x => x.CurrencyNo).ToListAsync(ct);

    public async Task<List<Currency>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.CompanyNo == companyNo).OrderBy(x => x.CurrencyNo).ToListAsync(ct);

    public async Task<Currency?> FindByCodeAndBranchAsync(string currencyCode, long branchNo,
                                                          CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.CurrencyCode == currencyCode && x.BranchNo == branchNo, ct);

    public async Task<bool> ExistsByCodeAndBranchAsync(string currencyCode, long branchNo,
                                                       CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.CurrencyCode == currencyCode && x.BranchNo == branchNo, ct);

    public async Task<Currency?> FindByCodeAndCompanyAsync(string currencyCode, long companyNo,
                                                           CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.CurrencyCode == currencyCode && x.CompanyNo == companyNo, ct);

    public async Task<bool> ExistsByCodeAndCompanyAsync(string currencyCode, long companyNo,
                                                        CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.CurrencyCode == currencyCode && x.CompanyNo == companyNo, ct);

    public async Task<List<Currency>> FindForBranchScopeAsync(long companyNo, long? branchNo,
                                                              CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo
                        && (x.BranchNo == null || (branchNo != null && x.BranchNo == branchNo)))
            .OrderBy(x => x.CurrencyNo).ToListAsync(ct);

    public void Add(Currency entity) => _db.Currencies.Add(entity);
}

public class VatTaxRepository : IVatTaxRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public VatTaxRepository(ISysDbContext db) => _db = db;

    private IQueryable<VatTax> Live => _db.VatTaxes.Where(x => x.IsDeleted == Deleted);

    public async Task<List<VatTax>> FindAllAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking().OrderBy(x => x.VatTaxNo).ToListAsync(ct);

    public async Task<VatTax?> FindByIdAsync(long vatTaxNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.VatTaxNo == vatTaxNo, ct);

    public async Task<List<VatTax>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.CompanyNo == companyNo).OrderBy(x => x.VatTaxNo).ToListAsync(ct);

    public async Task<VatTax?> FindByCodeAndCompanyAsync(string taxCode, long companyNo,
                                                         CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.TaxCode == taxCode && x.CompanyNo == companyNo, ct);

    public async Task<bool> ExistsByCodeAndCompanyAsync(string taxCode, long companyNo,
                                                        CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.TaxCode == taxCode && x.CompanyNo == companyNo, ct);

    public async Task<bool> ExistsByCodeCompanyAndBranchAsync(string taxCode, long companyNo, long? branchNo,
                                                              CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.TaxCode == taxCode && x.CompanyNo == companyNo && x.BranchNo == branchNo, ct);

    public async Task<List<VatTax>> FindForBranchScopeAsync(long companyNo, long? branchNo,
                                                            CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo
                        && (x.BranchNo == null || (branchNo != null && x.BranchNo == branchNo)))
            .OrderBy(x => x.VatTaxNo).ToListAsync(ct);

    public void Add(VatTax entity) => _db.VatTaxes.Add(entity);
}

public class CostCenterRepository : ICostCenterRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public CostCenterRepository(ISysDbContext db) => _db = db;

    private IQueryable<CostCenter> Live => _db.CostCenters.Where(x => x.IsDeleted == Deleted);

    public async Task<List<CostCenter>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.CompanyNo == companyNo).OrderBy(x => x.CostCenterNo).ToListAsync(ct);

    public async Task<CostCenter?> FindByIdAsync(long costCenterNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.CostCenterNo == costCenterNo, ct);

    public async Task<bool> ExistsByCompanyBranchAndCodeAsync(long companyNo, long? branchNo, string costCenterId,
                                                              CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.CompanyNo == companyNo && x.BranchNo == branchNo
                                 && x.CostCenterId == costCenterId, ct);

    public async Task<bool> ExistsByParentAsync(long parentCostCenterNo, CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.ParentCostCenterNo == parentCostCenterNo, ct);

    public async Task<List<CostCenter>> FindForBranchScopeAsync(long companyNo, long? branchNo,
                                                                CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo
                        && (x.BranchNo == null || (branchNo != null && x.BranchNo == branchNo)))
            .OrderBy(x => x.CostCenterNo).ToListAsync(ct);

    public void Add(CostCenter entity) => _db.CostCenters.Add(entity);
}

public class SettingRepository : ISettingRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public SettingRepository(ISysDbContext db) => _db = db;

    private IQueryable<Setting> Live => _db.Settings.Where(x => x.IsDeleted == Deleted);

    public async Task<List<Setting>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.CompanyNo == companyNo)
            .OrderBy(x => x.SettingGroup).ThenBy(x => x.SettingKey).ToListAsync(ct);

    public async Task<Setting?> FindByIdAsync(long settingNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.SettingNo == settingNo, ct);

    public async Task<Setting?> FindByCompanyAndKeyAsync(long companyNo, string settingKey,
                                                         CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.CompanyNo == companyNo && x.SettingKey == settingKey, ct);

    public async Task<bool> ExistsByCompanyBranchAndKeyAsync(long companyNo, long? branchNo, string settingKey,
                                                             CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.CompanyNo == companyNo && x.BranchNo == branchNo
                                 && x.SettingKey == settingKey, ct);

    public async Task<List<Setting>> FindForBranchScopeAsync(long companyNo, long? branchNo,
                                                             CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo
                        && (x.BranchNo == null || (branchNo != null && x.BranchNo == branchNo)))
            .OrderBy(x => x.SettingNo).ToListAsync(ct);

    public void Add(Setting entity) => _db.Settings.Add(entity);
}

public class FinYearRepository : IFinYearRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public FinYearRepository(ISysDbContext db) => _db = db;

    private IQueryable<FinYear> Live => _db.FinYears.Where(x => x.IsDeleted == Deleted);

    public async Task<List<FinYear>> FindAllAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking().OrderBy(x => x.FinYearNo).ToListAsync(ct);

    public async Task<FinYear?> FindByIdAsync(long finYearNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.FinYearNo == finYearNo, ct);

    public async Task<FinYear?> FindByFinYearIdAndCompanyAsync(string finYearId, long companyNo,
                                                               CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.FinYearId == finYearId && x.CompanyNo == companyNo, ct);

    public async Task<bool> ExistsByFinYearIdAndCompanyAsync(string finYearId, long companyNo,
                                                             CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.FinYearId == finYearId && x.CompanyNo == companyNo, ct);

    public async Task<bool> ExistsByFinYearNameAndCompanyAsync(string finYearName, long companyNo,
                                                               CancellationToken ct = default) =>
        await Live.AnyAsync(x => x.FinYearName == finYearName && x.CompanyNo == companyNo, ct);

    public async Task<List<FinYear>> FindByBranchAsync(long branchNo, CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.BranchNo == branchNo).OrderBy(x => x.FinYearNo).ToListAsync(ct);

    public async Task<List<FinYear>> FindForBranchScopeAsync(long companyNo, long? branchNo,
                                                             CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo
                        && (x.BranchNo == null || (branchNo != null && x.BranchNo == branchNo)))
            .OrderBy(x => x.FinYearNo).ToListAsync(ct);

    public async Task<List<FinYear>> FindForBranchDateScopeAsync(long companyNo, long? branchNo, DateOnly onDate,
                                                                 CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo
                        && (x.BranchNo == null || (branchNo != null && x.BranchNo == branchNo))
                        && x.StartDate <= onDate && x.EndDate >= onDate)
            .OrderBy(x => x.FinYearNo).ToListAsync(ct);

    public void Add(FinYear entity) => _db.FinYears.Add(entity);
}

public class FinYearDtlRepository : IFinYearDtlRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public FinYearDtlRepository(ISysDbContext db) => _db = db;

    public async Task<List<FinYearDtl>> FindByFinYearAsync(long finYearNo, CancellationToken ct = default) =>
        await _db.FinYearDtls.Where(x => x.FinYearNo == finYearNo && x.IsDeleted == Deleted)
            .OrderBy(x => x.StartDate).ToListAsync(ct);

    public async Task<FinYearDtl?> FindByIdAsync(long finPeriodNo, CancellationToken ct = default) =>
        await _db.FinYearDtls.FirstOrDefaultAsync(x => x.FinPeriodNo == finPeriodNo && x.IsDeleted == Deleted, ct);

    public void Add(FinYearDtl entity) => _db.FinYearDtls.Add(entity);
}

public class ExchangeRateRepository : IExchangeRateRepository
{
    private const short Active = 1, Deleted = 0;
    private readonly ISysDbContext _db;
    public ExchangeRateRepository(ISysDbContext db) => _db = db;

    private IQueryable<ExchangeRate> Live => _db.ExchangeRates.Where(x => x.IsDeleted == Deleted);

    public async Task<List<ExchangeRate>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.CompanyNo == companyNo)
            .OrderByDescending(x => x.RateDate).ThenByDescending(x => x.ExchangeRateNo).ToListAsync(ct);

    public async Task<ExchangeRate?> FindByIdAsync(long exchangeRateNo, CancellationToken ct = default) =>
        await Live.FirstOrDefaultAsync(x => x.ExchangeRateNo == exchangeRateNo, ct);

    public async Task<List<ExchangeRate>> FindByCompanyAndCurrencyAsync(long companyNo, long currencyNo,
                                                                        CancellationToken ct = default) =>
        await Live.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.CurrencyNo == currencyNo)
            .OrderByDescending(x => x.RateDate).ToListAsync(ct);

    public async Task<ExchangeRate?> FindLatestActiveAsync(long companyNo, long currencyNo,
                                                           CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo && x.CurrencyNo == currencyNo && x.IsActive == Active)
            .OrderByDescending(x => x.RateDate).ThenByDescending(x => x.ExchangeRateNo)
            .FirstOrDefaultAsync(ct);

    public void Add(ExchangeRate entity) => _db.ExchangeRates.Add(entity);
}

public class DocSequenceRepository : IDocSequenceRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public DocSequenceRepository(ISysDbContext db) => _db = db;

    public async Task<DocSequence?> FindByIdAsync(long docSeqNo, CancellationToken ct = default) =>
        await _db.DocSequences.FirstOrDefaultAsync(x => x.DocSeqNo == docSeqNo, ct);

    /// <summary>
    /// <c>SELECT … FOR UPDATE</c> — the row lock is the whole point: two documents created
    /// concurrently must not draw the same number.
    /// </summary>
    public async Task<DocSequence?> LockSequenceAsync(long companyNo, long? branchNo, string docType,
                                                      CancellationToken ct = default) =>
        (await _db.DocSequences
            .FromSqlRaw(
                """
                SELECT * FROM sys_doc_sequence
                WHERE  company_no = {0}
                  AND  (branch_no IS NULL OR branch_no = {1})
                  AND  doc_type = {2}
                ORDER BY branch_no NULLS LAST
                LIMIT 1
                FOR UPDATE
                """, companyNo, branchNo ?? (object)DBNull.Value, docType)
            .ToListAsync(ct)).FirstOrDefault();

    public void Add(DocSequence entity) => _db.DocSequences.Add(entity);
}

// ── Approval workflow ─────────────────────────────────────────────────────

public class ApprovalScopeRepository : IApprovalScopeRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public ApprovalScopeRepository(ISysDbContext db) => _db = db;

    public async Task<List<ApprovalScope>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await _db.ApprovalScopes.Where(x => x.CompanyNo == companyNo && x.IsDeleted == Deleted)
            .OrderBy(x => x.ScopeNo).ToListAsync(ct);

    public async Task<List<ApprovalScope>> FindByCompanyAndMenuAsync(long companyNo, long menuNo,
                                                                     CancellationToken ct = default) =>
        await _db.ApprovalScopes.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo && x.MenuNo == menuNo && x.IsDeleted == Deleted).ToListAsync(ct);

    public async Task<ApprovalScope?> FindByIdAsync(long scopeNo, CancellationToken ct = default) =>
        await _db.ApprovalScopes.FirstOrDefaultAsync(x => x.ScopeNo == scopeNo, ct);

    public void Add(ApprovalScope entity) => _db.ApprovalScopes.Add(entity);
}

public class ApprovalStepRepository : IApprovalStepRepository
{
    private const short Active = 1, Deleted = 0;
    private readonly ISysDbContext _db;
    public ApprovalStepRepository(ISysDbContext db) => _db = db;

    public async Task<List<ApprovalStep>> FindByScopeAsync(long scopeNo, CancellationToken ct = default) =>
        await _db.ApprovalSteps.Where(x => x.ScopeNo == scopeNo).OrderBy(x => x.StepNumber).ToListAsync(ct);

    public async Task<List<ApprovalStep>> FindActiveByScopeAsync(long scopeNo, CancellationToken ct = default) =>
        await _db.ApprovalSteps.AsNoTracking()
            .Where(x => x.ScopeNo == scopeNo && x.IsActive == Active && x.IsDeleted == Deleted)
            .OrderBy(x => x.StepNumber).ToListAsync(ct);

    public void Add(ApprovalStep entity) => _db.ApprovalSteps.Add(entity);

    public void RemoveRange(IEnumerable<ApprovalStep> entities) => _db.ApprovalSteps.RemoveRange(entities);
}

public class StepApproverRepository : IStepApproverRepository
{
    private readonly ISysDbContext _db;
    public StepApproverRepository(ISysDbContext db) => _db = db;

    public async Task<List<StepApprover>> FindByStepAsync(long stepNo, CancellationToken ct = default) =>
        await _db.StepApprovers.Where(x => x.StepNo == stepNo).ToListAsync(ct);

    public async Task<List<StepApprover>> FindByStepsAsync(IReadOnlyCollection<long> stepNos,
                                                           CancellationToken ct = default) =>
        stepNos.Count == 0
            ? new List<StepApprover>()
            : await _db.StepApprovers.AsNoTracking().Where(x => stepNos.Contains(x.StepNo)).ToListAsync(ct);

    public void Add(StepApprover entity) => _db.StepApprovers.Add(entity);

    public void RemoveRange(IEnumerable<StepApprover> entities) => _db.StepApprovers.RemoveRange(entities);
}

public class ApprovalRequestRepository : IApprovalRequestRepository
{
    private const short ReqPending = 1;
    private readonly ISysDbContext _db;
    public ApprovalRequestRepository(ISysDbContext db) => _db = db;

    public async Task<ApprovalRequest?> FindByIdAsync(long approvalRequestNo, CancellationToken ct = default) =>
        await _db.ApprovalRequests.FirstOrDefaultAsync(x => x.ApprovalRequestNo == approvalRequestNo, ct);

    public async Task<List<ApprovalRequest>> FindPendingByCompanyAsync(long companyNo,
                                                                       CancellationToken ct = default) =>
        await _db.ApprovalRequests.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo && x.Status == ReqPending)
            .OrderByDescending(x => x.ApprovalRequestNo).ToListAsync(ct);

    public void Add(ApprovalRequest entity) => _db.ApprovalRequests.Add(entity);
}

public class ApprovalRequestStepRepository : IApprovalRequestStepRepository
{
    private readonly ISysDbContext _db;
    public ApprovalRequestStepRepository(ISysDbContext db) => _db = db;

    public async Task<List<ApprovalRequestStep>> FindByRequestAsync(long approvalRequestNo,
                                                                    CancellationToken ct = default) =>
        await _db.ApprovalRequestSteps.Where(x => x.ApprovalRequestNo == approvalRequestNo)
            .OrderBy(x => x.StepNumber).ToListAsync(ct);

    public async Task<List<ApprovalRequestStep>> FindByRequestsForApproverAsync(
        IReadOnlyCollection<long> approvalRequestNos, long empNo, CancellationToken ct = default) =>
        approvalRequestNos.Count == 0
            ? new List<ApprovalRequestStep>()
            : await _db.ApprovalRequestSteps.AsNoTracking()
                .Where(x => approvalRequestNos.Contains(x.ApprovalRequestNo) && x.EmpNo == empNo).ToListAsync(ct);

    public void Add(ApprovalRequestStep entity) => _db.ApprovalRequestSteps.Add(entity);
}

// ── Files / audit / sessions ──────────────────────────────────────────────

public class SysFileRepository : ISysFileRepository
{
    private const short Deleted = 0;
    private readonly ISysDbContext _db;
    public SysFileRepository(ISysDbContext db) => _db = db;

    /// <summary>Metadata only — <c>file_bytes</c> is deliberately never selected here.</summary>
    public async Task<List<SysFile>> SearchAsync(long companyNo, short? entityType, long? entityNo,
                                                 CancellationToken ct = default)
    {
        var query = _db.SysFiles.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == Deleted);

        if (entityType != null) query = query.Where(x => x.EntityType == entityType);
        if (entityNo != null) query = query.Where(x => x.EntityNo == entityNo);

        return await query
            .OrderByDescending(x => x.IsPrimary).ThenByDescending(x => x.FileNo)
            .Select(x => new SysFile
            {
                FileNo = x.FileNo,
                CompanyNo = x.CompanyNo,
                EntityType = x.EntityType,
                EntityNo = x.EntityNo,
                FileName = x.FileName,
                ContentType = x.ContentType,
                FileExtension = x.FileExtension,
                FileSize = x.FileSize,
                StorageType = x.StorageType,
                IsPrimary = x.IsPrimary,
                WidthPx = x.WidthPx,
                HeightPx = x.HeightPx,
                ChecksumSha256 = x.ChecksumSha256,
                CreatedAt = x.CreatedAt,
                RowVersion = x.RowVersion
            })
            .ToListAsync(ct);
    }

    public async Task<SysFile?> FindByIdForCompanyAsync(long fileNo, long companyNo, CancellationToken ct = default) =>
        await _db.SysFiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.FileNo == fileNo && x.CompanyNo == companyNo && x.IsDeleted == Deleted, ct);

    public void Add(SysFile entity) => _db.SysFiles.Add(entity);
}

public class SysAuditLogRepository : ISysAuditLogRepository
{
    private readonly ISysDbContext _db;
    public SysAuditLogRepository(ISysDbContext db) => _db = db;

    public async Task<List<SysAuditLog>> SearchAsync(long companyNo, string? tableName, string? actionType,
                                                     long? userNo, DateTime? fromAt, DateTime? toAt, int limit,
                                                     CancellationToken ct = default)
    {
        var query = _db.SysAuditLogs.AsNoTracking().Where(x => x.CompanyNo == companyNo);

        if (!string.IsNullOrWhiteSpace(tableName)) query = query.Where(x => x.TableName == tableName);
        if (!string.IsNullOrWhiteSpace(actionType)) query = query.Where(x => x.ActionType == actionType);
        if (userNo != null) query = query.Where(x => x.UserNo == userNo);
        if (fromAt != null) query = query.Where(x => x.ActionAt >= fromAt);
        if (toAt != null) query = query.Where(x => x.ActionAt <= toAt);

        return await query.OrderByDescending(x => x.ActionAt).Take(limit).ToListAsync(ct);
    }

    public void Add(SysAuditLog entity) => _db.SysAuditLogs.Add(entity);
}

public class LoginAttemptRepository : ILoginAttemptRepository
{
    private readonly ISysDbContext _db;
    public LoginAttemptRepository(ISysDbContext db) => _db = db;

    /// <summary>Joined to <c>sys_user</c> so only attempts against this company's users appear.</summary>
    public async Task<List<LoginAttempt>> FindRecentForCompanyAsync(long companyNo, int limit,
                                                                    CancellationToken ct = default) =>
        await (from la in _db.LoginAttempts.AsNoTracking()
               join u in _db.Users.AsNoTracking() on la.UserId equals u.UserId
               where u.CompanyNo == companyNo && u.IsDeleted == 0
               orderby la.AttemptedAt descending
               select la).Take(limit).ToListAsync(ct);

    public void Add(LoginAttempt entity) => _db.LoginAttempts.Add(entity);
}

public class SysSessionRepository : ISysSessionRepository
{
    private readonly ISysDbContext _db;
    public SysSessionRepository(ISysDbContext db) => _db = db;

    public async Task<SysSession?> FindByIdAsync(long sessionNo, CancellationToken ct = default) =>
        await _db.SysSessions.FirstOrDefaultAsync(x => x.SessionNo == sessionNo, ct);

    /// <summary>Not revoked, not logged out, and not past expiry.</summary>
    public async Task<List<SysSession>> FindLiveForUserAsync(long userNo, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        return await _db.SysSessions.AsNoTracking()
            .Where(x => x.UserNo == userNo && x.IsRevoked == 0 && x.LogoutAt == null
                        && (x.ExpiredAt == null || x.ExpiredAt > now))
            .OrderByDescending(x => x.LoginAt).ToListAsync(ct);
    }

    public void Add(SysSession entity) => _db.SysSessions.Add(entity);
}

public class SysLogRepository : ISysLogRepository
{
    private readonly ISysDbContext _db;
    public SysLogRepository(ISysDbContext db) => _db = db;

    public async Task<List<SysLog>> FindRecentForCompanyAsync(long companyNo, int limit,
                                                              CancellationToken ct = default) =>
        await _db.SysLogs.AsNoTracking()
            .Where(x => x.CompanyNo == companyNo)
            .OrderByDescending(x => x.LogNo).Take(limit).ToListAsync(ct);

    public void Add(SysLog entity) => _db.SysLogs.Add(entity);
}

public class EventOutboxRepository : IEventOutboxRepository
{
    private readonly ISysDbContext _db;
    public EventOutboxRepository(ISysDbContext db) => _db = db;

    public async Task<List<EventOutbox>> FindUndrainedAsync(int limit, CancellationToken ct = default) =>
        await _db.EventOutboxes.Where(x => x.Status == 1 && x.IsProcessed == 0)
            .OrderBy(x => x.EventNo).Take(limit).ToListAsync(ct);

    public void Add(EventOutbox entity) => _db.EventOutboxes.Add(entity);
}
