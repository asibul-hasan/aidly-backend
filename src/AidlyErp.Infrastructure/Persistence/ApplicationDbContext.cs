using Microsoft.EntityFrameworkCore;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Security;
using AidlyErp.Domain.Audit;
using AidlyErp.Domain.Auth;
using AidlyErp.Domain.Common;
using AidlyErp.Domain.Fin;
using AidlyErp.Domain.Hrm;
using AidlyErp.Domain.Inv;
using AidlyErp.Domain.Pur;
using AidlyErp.Domain.Sal;
using AidlyErp.Domain.Sys;

namespace AidlyErp.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICompanyBranchContext _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICompanyBranchContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // ---------------------------------------------------------------------
    // Query-filter inputs. These are read by the global filters below; EF turns
    // references to them into query parameters, so each request is scoped by its own
    // tenant context even though the compiled model is cached process-wide.
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
        typeof(ApplicationDbContext).GetMethod(nameof(ApplyTenantAndSoftDeleteFilter),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

    private static readonly System.Reflection.MethodInfo SoftDeleteFilterMethod =
        typeof(ApplicationDbContext).GetMethod(nameof(ApplySoftDeleteFilter),
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

    // Core SYS DbSets
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<SysSession> SysSessions => Set<SysSession>();
    public DbSet<SysAuditLog> SysAuditLogs => Set<SysAuditLog>();
    public DbSet<SysLog> SysLogs => Set<SysLog>();

    // Financial & Setup SYS DbSets
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<FinYear> FinYears => Set<FinYear>();
    public DbSet<FinYearDtl> FinYearDtls => Set<FinYearDtl>();
    public DbSet<VatTax> VatTaxes => Set<VatTax>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<DocSequence> DocSequences => Set<DocSequence>();

    // Menu & Module SYS DbSets
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<EnrollMenu> EnrollMenus => Set<EnrollMenu>();
    public DbSet<SysModule> SysModules => Set<SysModule>();
    public DbSet<SysSubmodule> SysSubmodules => Set<SysSubmodule>();
    public DbSet<SysFile> SysFiles => Set<SysFile>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<SysCatalogEntity> SysCatalogEntities => Set<SysCatalogEntity>();

    // Approval & Security SYS DbSets
    public DbSet<ApprovalWorkflow> ApprovalWorkflows => Set<ApprovalWorkflow>();
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
    public DbSet<EventOutbox> EventOutboxes => Set<EventOutbox>();
    public DbSet<Department> Departments => Set<Department>();

    // Core HRM DbSets
    public DbSet<HrmDepartment> HrmDepartments => Set<HrmDepartment>();
    public DbSet<HrmDesignation> HrmDesignations => Set<HrmDesignation>();
    public DbSet<HrmEmployee> HrmEmployees => Set<HrmEmployee>();
    public DbSet<HrmAttendance> HrmAttendances => Set<HrmAttendance>();
    public DbSet<HrmLeaveApplication> HrmLeaveApplications => Set<HrmLeaveApplication>();
    public DbSet<HrmLeaveType> HrmLeaveTypes => Set<HrmLeaveType>();
    public DbSet<HrmPayrollRun> HrmPayrollRuns => Set<HrmPayrollRun>();
    public DbSet<HrmPayslip> HrmPayslips => Set<HrmPayslip>();
    public DbSet<HrmPayslipDtl> HrmPayslipDtls => Set<HrmPayslipDtl>();
    public DbSet<HrmSalaryComponent> HrmSalaryComponents => Set<HrmSalaryComponent>();
    public DbSet<HrmShift> HrmShifts => Set<HrmShift>();
    public DbSet<HrmHoliday> HrmHolidays => Set<HrmHoliday>();
    public DbSet<HrmGrade> HrmGrades => Set<HrmGrade>();

    // Expanded HRM DbSets (All 36 entities)
    public DbSet<HrmAttendanceAdjustment> HrmAttendanceAdjustments => Set<HrmAttendanceAdjustment>();
    public DbSet<HrmLeaveBalance> HrmLeaveBalances => Set<HrmLeaveBalance>();
    public DbSet<HrmLeaveLedger> HrmLeaveLedgers => Set<HrmLeaveLedger>();
    public DbSet<HrmLeavePolicySetup> HrmLeavePolicySetups => Set<HrmLeavePolicySetup>();
    public DbSet<HrmLeaveApplicationRule> HrmLeaveApplicationRules => Set<HrmLeaveApplicationRule>();
    public DbSet<HrmOvertime> HrmOvertimes => Set<HrmOvertime>();
    public DbSet<HrmShiftRoster> HrmShiftRosters => Set<HrmShiftRoster>();
    public DbSet<HrmShiftRosterLine> HrmShiftRosterLines => Set<HrmShiftRosterLine>();
    public DbSet<HrmBonusRun> HrmBonusRuns => Set<HrmBonusRun>();
    public DbSet<HrmBonusLine> HrmBonusLines => Set<HrmBonusLine>();
    public DbSet<HrmBonusScopeDesignation> HrmBonusScopeDesignations => Set<HrmBonusScopeDesignation>();
    public DbSet<HrmBonusScopeEmployee> HrmBonusScopeEmployees => Set<HrmBonusScopeEmployee>();
    public DbSet<HrmLoanAdvance> HrmLoanAdvances => Set<HrmLoanAdvance>();
    public DbSet<HrmPayrollPolicy> HrmPayrollPolicies => Set<HrmPayrollPolicy>();
    public DbSet<HrmSalaryStructure> HrmSalaryStructures => Set<HrmSalaryStructure>();
    public DbSet<HrmSalaryStructureDtl> HrmSalaryStructureDtls => Set<HrmSalaryStructureDtl>();
    public DbSet<HrmTaxSlab> HrmTaxSlabs => Set<HrmTaxSlab>();
    public DbSet<HrmCandidate> HrmCandidates => Set<HrmCandidate>();
    public DbSet<HrmJobRequisition> HrmJobRequisitions => Set<HrmJobRequisition>();
    public DbSet<HrmOffer> HrmOffers => Set<HrmOffer>();
    public DbSet<HrmEmployeeMovement> HrmEmployeeMovements => Set<HrmEmployeeMovement>();
    public DbSet<HrmFinalSettlement> HrmFinalSettlements => Set<HrmFinalSettlement>();
    public DbSet<HrmGradeStep> HrmGradeSteps => Set<HrmGradeStep>();

    // FIN Module DbSets (All 11 entities)
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

    // INV Module DbSets (All 20 entities)
    public DbSet<InvProduct> InvProducts => Set<InvProduct>();
    public DbSet<InvCategory> InvCategories => Set<InvCategory>();
    public DbSet<InvBrand> InvBrands => Set<InvBrand>();
    public DbSet<InvUom> InvUoms => Set<InvUom>();
    public DbSet<InvUomConversion> InvUomConversions => Set<InvUomConversion>();
    public DbSet<InvProductAttribute> InvProductAttributes => Set<InvProductAttribute>();
    public DbSet<InvProductAttributeValue> InvProductAttributeValues => Set<InvProductAttributeValue>();
    public DbSet<InvProductBarcode> InvProductBarcodes => Set<InvProductBarcode>();
    public DbSet<InvProductVariant> InvProductVariants => Set<InvProductVariant>();
    public DbSet<InvWarehouse> InvWarehouses => Set<InvWarehouse>();
    public DbSet<InvRack> InvRacks => Set<InvRack>();
    public DbSet<InvStock> InvStocks => Set<InvStock>();
    public DbSet<InvStockLedger> InvStockLedgers => Set<InvStockLedger>();
    public DbSet<InvBatch> InvBatches => Set<InvBatch>();
    public DbSet<InvReorder> InvReorders => Set<InvReorder>();
    public DbSet<InvValuationLayer> InvValuationLayers => Set<InvValuationLayer>();
    public DbSet<InvStockAdjustment> InvStockAdjustments => Set<InvStockAdjustment>();
    public DbSet<InvStockAdjustmentDtl> InvStockAdjustmentDtls => Set<InvStockAdjustmentDtl>();
    public DbSet<InvStockTransfer> InvStockTransfers => Set<InvStockTransfer>();
    public DbSet<InvStockTransferDtl> InvStockTransferDtls => Set<InvStockTransferDtl>();

    // PUR Module DbSets (All 15 entities)
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

    // SAL Module DbSets (All 8 entities)
    public DbSet<SalCustomer> SalCustomers => Set<SalCustomer>();
    public DbSet<SalCustomerLedger> SalCustomerLedgers => Set<SalCustomerLedger>();
    public DbSet<SalInvoice> SalInvoices => Set<SalInvoice>();
    public DbSet<SalInvoiceDtl> SalInvoiceDtls => Set<SalInvoiceDtl>();
    public DbSet<SalReceipt> SalReceipts => Set<SalReceipt>();
    public DbSet<SalReceiptAlloc> SalReceiptAllocs => Set<SalReceiptAlloc>();
    public DbSet<SalReturn> SalReturns => Set<SalReturn>();
    public DbSet<SalReturnDtl> SalReturnDtls => Set<SalReturnDtl>();

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
        // central warehouse). References to context properties become query parameters, so the
        // filter re-evaluates per request even though the model itself is cached.
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

        // Entity configuration constraints
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

        modelBuilder.Entity<HrmEmployee>(entity =>
        {
            entity.HasIndex(e => new { e.BranchNo, e.IsDeleted }).HasDatabaseName("idx_emp_branch_deleted");
            entity.HasIndex(e => new { e.DepartmentNo, e.IsDeleted }).HasDatabaseName("idx_emp_dept_deleted");
            entity.HasIndex(e => new { e.DesignationNo, e.IsDeleted }).HasDatabaseName("idx_emp_designation_deleted");
        });

        modelBuilder.Entity<HrmAttendance>(entity =>
        {
            entity.HasIndex(e => new { e.BranchNo, e.AttDate, e.IsDeleted }).HasDatabaseName("idx_att_branch_date_deleted");
            entity.HasIndex(e => new { e.EmployeeNo, e.AttDate, e.IsDeleted }).HasDatabaseName("idx_att_emp_date_deleted");
        });

        modelBuilder.Entity<HrmLeaveApplication>(entity =>
        {
            entity.HasIndex(e => new { e.EmployeeNo, e.Status, e.IsDeleted }).HasDatabaseName("idx_leaveapp_emp_status_deleted");
            entity.HasIndex(e => new { e.BranchNo, e.IsDeleted }).HasDatabaseName("idx_leaveapp_branch_deleted");
        });

        modelBuilder.Entity<HrmPayrollRun>(entity =>
        {
            entity.HasIndex(e => new { e.BranchNo, e.PayPeriod, e.IsDeleted }).HasDatabaseName("idx_payrun_branch_period_deleted");
        });

        // FIN Module Indexes
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

        // INV Module Indexes
        modelBuilder.Entity<InvProduct>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.ProductCode, e.IsDeleted }).HasDatabaseName("idx_inv_prod_code_deleted");
            entity.HasIndex(e => new { e.CategoryNo, e.IsDeleted }).HasDatabaseName("idx_inv_prod_cat_deleted");
        });

        modelBuilder.Entity<InvStock>(entity =>
        {
            entity.HasIndex(e => new { e.WarehouseNo, e.ProductNo, e.IsDeleted }).HasDatabaseName("idx_inv_stock_wh_prod");
        });

        modelBuilder.Entity<InvStockLedger>(entity =>
        {
            entity.HasIndex(e => new { e.ProductNo, e.WarehouseNo, e.TransDate }).HasDatabaseName("idx_inv_ledger_prod_wh_date");
        });

        // PUR Module Indexes
        modelBuilder.Entity<PurSupplier>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.SupplierCode, e.IsDeleted }).HasDatabaseName("idx_pur_sup_code_deleted");
        });

        modelBuilder.Entity<PurOrder>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.SupplierNo, e.Status }).HasDatabaseName("idx_pur_order_sup_status");
        });

        // SAL Module Indexes
        modelBuilder.Entity<SalCustomer>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.CustomerCode, e.IsDeleted }).HasDatabaseName("idx_sal_cust_code_deleted");
        });

        modelBuilder.Entity<SalInvoice>(entity =>
        {
            entity.HasIndex(e => new { e.CompanyNo, e.CustomerNo, e.Status }).HasDatabaseName("idx_sal_inv_cust_status");
        });
    }
}
