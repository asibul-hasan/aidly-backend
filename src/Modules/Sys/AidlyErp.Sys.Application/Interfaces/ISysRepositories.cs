using AidlyErp.Shared.Core.Audit;
using AidlyErp.Sys.Domain;
using AidlyErp.Sys.Domain;

namespace AidlyErp.Sys.Application.Interfaces;

// ═══════════════════════════════════════════════════════════════════════════
// SYS Repository Interfaces — port of the Spring Data JPA repositories.
// Every method here has a 1:1 counterpart in the Java sys/repository/ package.
//
// Java derived-query names carry their filters in the method name
// (findByCompanyNoAndIsDeleted…). Those are shortened here because is_deleted = 0
// is applied unconditionally: the .NET services never ask for deleted rows.
// ═══════════════════════════════════════════════════════════════════════════

// ── Company / Branch ──────────────────────────────────────────────────────

public interface ICompanyRepository
{
    Task<List<Company>> FindAllAsync(CancellationToken ct = default);
    Task<List<Company>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<Company?> FindByIdAsync(long companyNo, CancellationToken ct = default);
    Task<List<Company>> FindByIdsAsync(IReadOnlyCollection<long> companyNos, CancellationToken ct = default);
    Task<Company?> FindByCompanyIdAsync(string companyId, CancellationToken ct = default);
    Task<bool> ExistsByCompanyIdAsync(string companyId, CancellationToken ct = default);
    void Add(Company entity);
}

public interface IBranchRepository
{
    Task<List<Branch>> FindAllAsync(CancellationToken ct = default);
    Task<List<Branch>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<Branch?> FindByIdAsync(long branchNo, CancellationToken ct = default);
    Task<List<Branch>> FindByIdsAsync(IReadOnlyCollection<long> branchNos, CancellationToken ct = default);
    Task<List<Branch>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<List<Branch>> FindByCompanyPaginatedAsync(long companyNo, int skip, int take, CancellationToken ct = default);
    Task<Branch?> FindByCompanyAndBranchIdAsync(long companyNo, string branchId, CancellationToken ct = default);
    Task<bool> ExistsByCompanyAndBranchIdAsync(long companyNo, string branchId, CancellationToken ct = default);

    /// <summary>The company's main branch, if one is designated.</summary>
    Task<Branch?> FindMainBranchAsync(long companyNo, CancellationToken ct = default);

    void Add(Branch entity);
}

// ── User / Role / access ──────────────────────────────────────────────────

public interface IUserRepository
{
    Task<List<User>> FindAllAsync(CancellationToken ct = default);
    Task<User?> FindByIdAsync(long userNo, CancellationToken ct = default);
    Task<User?> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<bool> ExistsByUserIdAsync(string userId, CancellationToken ct = default);
    Task<User?> FindByEmployeeNoAsync(long employeeNo, CancellationToken ct = default);
    Task<bool> ExistsByEmployeeNoAsync(long employeeNo, CancellationToken ct = default);
    Task<List<User>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    void Add(User entity);
}

public interface IRoleRepository
{
    Task<List<Role>> FindAllAsync(CancellationToken ct = default);
    Task<List<Role>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<Role?> FindByIdAsync(long roleNo, CancellationToken ct = default);
    Task<List<Role>> FindByIdsAsync(IReadOnlyCollection<long> roleNos, CancellationToken ct = default);
    Task<List<Role>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<List<Role>> FindByCompanyPaginatedAsync(long companyNo, int skip, int take, CancellationToken ct = default);
    Task<Role?> FindByCompanyAndRoleIdAsync(long companyNo, string roleId, CancellationToken ct = default);
    Task<bool> ExistsByCompanyAndRoleIdAsync(long companyNo, string roleId, CancellationToken ct = default);
    void Add(Role entity);
}

public interface IUserBranchRepository
{
    Task<List<UserBranch>> FindByUserAsync(long userNo, CancellationToken ct = default);

    /// <summary>
    /// Every row for a user, <b>including soft-deleted ones</b>. SYS1104 reconcile uses this to
    /// revive a previously-removed (user, branch) slot instead of inserting a duplicate that
    /// would violate <c>uq_user_branch</c>.
    /// </summary>
    Task<List<UserBranch>> FindAllForUserIncludingDeletedAsync(long userNo, CancellationToken ct = default);

    Task<List<UserBranch>> FindActiveByUserAsync(long userNo, CancellationToken ct = default);
    Task<List<UserBranch>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<UserBranch?> FindByUserAndBranchAsync(long userNo, long branchNo, CancellationToken ct = default);
    Task<UserBranch?> FindDefaultForUserAsync(long userNo, CancellationToken ct = default);
    Task<List<long>> FindRoleNosForUserInBranchAsync(long userNo, long branchNo, CancellationToken ct = default);
    Task<bool> UserHasAnyRoleInBranchAsync(long userNo, long branchNo, CancellationToken ct = default);
    void Add(UserBranch entity);
}

public interface IUserCompanyRepository
{
    Task<List<UserCompany>> FindByUserAsync(long userNo, CancellationToken ct = default);
    Task<UserCompany?> FindByUserAndCompanyAsync(long userNo, long companyNo, CancellationToken ct = default);
    void Add(UserCompany entity);
}

public interface ISysRoleBranchRepository
{
    Task<List<SysRoleBranch>> FindByRoleAsync(long roleNo, CancellationToken ct = default);
    Task<List<SysRoleBranch>> FindByRolesAsync(IReadOnlyCollection<long> roleNos, CancellationToken ct = default);
    void Add(SysRoleBranch entity);
}

public interface IRolePermissionRepository
{
    Task<List<RolePermission>> FindByRoleAsync(long roleNo, CancellationToken ct = default);
    void Add(RolePermission entity);
}

// ── Menu catalogue ────────────────────────────────────────────────────────

public interface ISysModuleRepository
{
    Task<List<SysModule>> FindAllAsync(CancellationToken ct = default);
    Task<SysModule?> FindByIdAsync(long moduleNo, CancellationToken ct = default);
    Task<bool> ExistsByModuleCodeAsync(string moduleCode, CancellationToken ct = default);
    void Add(SysModule entity);
}

public interface ISysSubmoduleRepository
{
    Task<List<SysSubmodule>> FindAllAsync(CancellationToken ct = default);
    Task<SysSubmodule?> FindByIdAsync(long submoduleNo, CancellationToken ct = default);
    Task<List<SysSubmodule>> FindByModuleAsync(long moduleNo, CancellationToken ct = default);
    Task<bool> ExistsByModuleAndCodeAsync(long moduleNo, string submoduleCode, CancellationToken ct = default);
    void Add(SysSubmodule entity);
}

public interface IMenuRepository
{
    Task<List<Menu>> FindAllAsync(CancellationToken ct = default);
    Task<Menu?> FindByIdAsync(long menuNo, CancellationToken ct = default);
    Task<Menu?> FindByFormIdAsync(string formId, CancellationToken ct = default);
    Task<bool> ExistsByFormIdAsync(string formId, CancellationToken ct = default);
    Task<List<Menu>> FindBySubmoduleAsync(long submoduleNo, CancellationToken ct = default);

    /// <summary>
    /// The <c>form_id</c> that best matches an incoming request URI, by longest matching
    /// <c>route_path</c>. Used by RBAC to map a path to its permission gate.
    /// </summary>
    Task<string?> FindBestFormIdForRequestPathAsync(string requestPath, string pathWithoutApiPrefix,
                                                    CancellationToken ct = default);

    void Add(Menu entity);
}

public interface IEnrollMenuRepository
{
    Task<List<EnrollMenu>> FindCompanyWideAsync(long companyNo, CancellationToken ct = default);
    void Add(EnrollMenu entity);
}

// ── Financial setup ───────────────────────────────────────────────────────

public interface ICurrencyRepository
{
    Task<List<Currency>> FindAllAsync(CancellationToken ct = default);
    Task<Currency?> FindByIdAsync(long currencyNo, CancellationToken ct = default);
    Task<List<Currency>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<List<Currency>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<Currency?> FindByCodeAndBranchAsync(string currencyCode, long branchNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAndBranchAsync(string currencyCode, long branchNo, CancellationToken ct = default);
    Task<Currency?> FindByCodeAndCompanyAsync(string currencyCode, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAndCompanyAsync(string currencyCode, long companyNo, CancellationToken ct = default);

    /// <summary>The branch's own rows plus company-wide ones (<c>branch_no IS NULL</c>).</summary>
    Task<List<Currency>> FindForBranchScopeAsync(long companyNo, long? branchNo, CancellationToken ct = default);

    void Add(Currency entity);
}

public interface IVatTaxRepository
{
    Task<List<VatTax>> FindAllAsync(CancellationToken ct = default);
    Task<VatTax?> FindByIdAsync(long vatTaxNo, CancellationToken ct = default);
    Task<List<VatTax>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<VatTax?> FindByCodeAndCompanyAsync(string taxCode, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAndCompanyAsync(string taxCode, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeCompanyAndBranchAsync(string taxCode, long companyNo, long? branchNo,
                                                 CancellationToken ct = default);
    Task<List<VatTax>> FindForBranchScopeAsync(long companyNo, long? branchNo, CancellationToken ct = default);
    void Add(VatTax entity);
}

public interface ICostCenterRepository
{
    Task<List<CostCenter>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<CostCenter?> FindByIdAsync(long costCenterNo, CancellationToken ct = default);
    Task<bool> ExistsByCompanyBranchAndCodeAsync(long companyNo, long? branchNo, string costCenterId,
                                                 CancellationToken ct = default);

    /// <summary>Guards deletion and re-parenting — a cost centre with children is locked.</summary>
    Task<bool> ExistsByParentAsync(long parentCostCenterNo, CancellationToken ct = default);

    Task<List<CostCenter>> FindForBranchScopeAsync(long companyNo, long? branchNo, CancellationToken ct = default);
    void Add(CostCenter entity);
}

public interface ISettingRepository
{
    Task<List<Setting>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<Setting?> FindByIdAsync(long settingNo, CancellationToken ct = default);
    Task<Setting?> FindByCompanyAndKeyAsync(long companyNo, string settingKey, CancellationToken ct = default);
    Task<bool> ExistsByCompanyBranchAndKeyAsync(long companyNo, long? branchNo, string settingKey,
                                                CancellationToken ct = default);
    Task<List<Setting>> FindForBranchScopeAsync(long companyNo, long? branchNo, CancellationToken ct = default);
    void Add(Setting entity);
}

public interface IFinYearRepository
{
    Task<List<FinYear>> FindAllAsync(CancellationToken ct = default);
    Task<FinYear?> FindByIdAsync(long finYearNo, CancellationToken ct = default);
    Task<FinYear?> FindByFinYearIdAndCompanyAsync(string finYearId, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByFinYearIdAndCompanyAsync(string finYearId, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsByFinYearNameAndCompanyAsync(string finYearName, long companyNo, CancellationToken ct = default);
    Task<List<FinYear>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<List<FinYear>> FindForBranchScopeAsync(long companyNo, long? branchNo, CancellationToken ct = default);

    /// <summary>Financial years whose date range covers <paramref name="onDate"/>.</summary>
    Task<List<FinYear>> FindForBranchDateScopeAsync(long companyNo, long? branchNo, DateOnly onDate,
                                                    CancellationToken ct = default);

    void Add(FinYear entity);
}

public interface IFinYearDtlRepository
{
    Task<List<FinYearDtl>> FindByFinYearAsync(long finYearNo, CancellationToken ct = default);
    Task<FinYearDtl?> FindByIdAsync(long finPeriodNo, CancellationToken ct = default);
    void Add(FinYearDtl entity);
}

public interface IExchangeRateRepository
{
    Task<List<ExchangeRate>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<ExchangeRate?> FindByIdAsync(long exchangeRateNo, CancellationToken ct = default);
    Task<List<ExchangeRate>> FindByCompanyAndCurrencyAsync(long companyNo, long currencyNo,
                                                           CancellationToken ct = default);

    /// <summary>Newest active rate for a currency; drives the currency master's <c>exchange_rate</c>.</summary>
    Task<ExchangeRate?> FindLatestActiveAsync(long companyNo, long currencyNo, CancellationToken ct = default);

    void Add(ExchangeRate entity);
}

public interface IDocSequenceRepository
{
    Task<DocSequence?> FindByIdAsync(long docSeqNo, CancellationToken ct = default);

    /// <summary>
    /// Reads the sequence row <b>with a row lock</b> so concurrent document creation cannot
    /// allocate the same number twice.
    /// </summary>
    Task<DocSequence?> LockSequenceAsync(long companyNo, long? branchNo, string docType,
                                         CancellationToken ct = default);

    void Add(DocSequence entity);
}

// ── Approval workflow ─────────────────────────────────────────────────────

public interface IApprovalScopeRepository
{
    Task<List<ApprovalScope>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<List<ApprovalScope>> FindByCompanyAndMenuAsync(long companyNo, long menuNo, CancellationToken ct = default);
    Task<ApprovalScope?> FindByIdAsync(long scopeNo, CancellationToken ct = default);
    void Add(ApprovalScope entity);
}

public interface IApprovalStepRepository
{
    Task<List<ApprovalStep>> FindByScopeAsync(long scopeNo, CancellationToken ct = default);
    Task<List<ApprovalStep>> FindActiveByScopeAsync(long scopeNo, CancellationToken ct = default);
    void Add(ApprovalStep entity);
    void RemoveRange(IEnumerable<ApprovalStep> entities);
}

public interface IStepApproverRepository
{
    Task<List<StepApprover>> FindByStepAsync(long stepNo, CancellationToken ct = default);
    Task<List<StepApprover>> FindByStepsAsync(IReadOnlyCollection<long> stepNos, CancellationToken ct = default);
    void Add(StepApprover entity);
    void RemoveRange(IEnumerable<StepApprover> entities);
}

public interface IApprovalRequestRepository
{
    Task<ApprovalRequest?> FindByIdAsync(long approvalRequestNo, CancellationToken ct = default);
    Task<List<ApprovalRequest>> FindPendingByCompanyAsync(long companyNo, CancellationToken ct = default);
    void Add(ApprovalRequest entity);
}

public interface IApprovalRequestStepRepository
{
    Task<List<ApprovalRequestStep>> FindByRequestAsync(long approvalRequestNo, CancellationToken ct = default);
    Task<List<ApprovalRequestStep>> FindByRequestsForApproverAsync(IReadOnlyCollection<long> approvalRequestNos,
                                                                   long empNo, CancellationToken ct = default);
    void Add(ApprovalRequestStep entity);
}

// ── Files / audit / sessions ──────────────────────────────────────────────

public interface ISysFileRepository
{
    Task<List<SysFile>> SearchAsync(long companyNo, short? entityType, long? entityNo, CancellationToken ct = default);
    Task<SysFile?> FindByIdForCompanyAsync(long fileNo, long companyNo, CancellationToken ct = default);
    void Add(SysFile entity);
}

public interface ISysAuditLogRepository
{
    Task<List<SysAuditLog>> SearchAsync(long companyNo, string? tableName, string? actionType, long? userNo,
                                        DateTime? fromAt, DateTime? toAt, int limit, CancellationToken ct = default);
    void Add(SysAuditLog entity);
}

public interface ILoginAttemptRepository
{
    Task<List<LoginAttempt>> FindRecentForCompanyAsync(long companyNo, int limit, CancellationToken ct = default);
    void Add(LoginAttempt entity);
}

public interface ISysSessionRepository
{
    Task<SysSession?> FindByIdAsync(long sessionNo, CancellationToken ct = default);
    Task<List<SysSession>> FindLiveForUserAsync(long userNo, CancellationToken ct = default);
    void Add(SysSession entity);
}

public interface ISysLogRepository
{
    /// <summary>Most recent request-traffic rows for a company.</summary>
    Task<List<SysLog>> FindRecentForCompanyAsync(long companyNo, int limit, CancellationToken ct = default);

    void Add(SysLog entity);
}

public interface IEventOutboxRepository
{
    /// <summary>Undrained events, oldest first — the GL posting consumer's work queue.</summary>
    Task<List<EventOutbox>> FindUndrainedAsync(int limit, CancellationToken ct = default);
    void Add(EventOutbox entity);
}
