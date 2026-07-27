using Microsoft.EntityFrameworkCore;
using AidlyErp.Domain.Audit;
using AidlyErp.Domain.Auth;
using AidlyErp.Domain.Fin;
using AidlyErp.Domain.Hrm;
using AidlyErp.Domain.Inv;
using AidlyErp.Domain.Pur;
using AidlyErp.Domain.Sal;
using AidlyErp.Domain.Sys;

namespace AidlyErp.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Core SYS DbSets
    DbSet<Company> Companies { get; }
    DbSet<Branch> Branches { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserBranch> UserBranches { get; }
    DbSet<SysSession> SysSessions { get; }
    DbSet<SysAuditLog> SysAuditLogs { get; }
    DbSet<SysLog> SysLogs { get; }

    // Financial & Setup SYS DbSets
    DbSet<Currency> Currencies { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    DbSet<FinYear> FinYears { get; }
    DbSet<FinYearDtl> FinYearDtls { get; }
    DbSet<VatTax> VatTaxes { get; }
    DbSet<CostCenter> CostCenters { get; }
    DbSet<DocSequence> DocSequences { get; }

    // Menu & Module SYS DbSets
    DbSet<Menu> Menus { get; }
    DbSet<EnrollMenu> EnrollMenus { get; }
    DbSet<SysModule> SysModules { get; }
    DbSet<SysSubmodule> SysSubmodules { get; }
    DbSet<SysFile> SysFiles { get; }
    DbSet<Setting> Settings { get; }
    DbSet<SysCatalogEntity> SysCatalogEntities { get; }

    // Approval & Security SYS DbSets
    DbSet<ApprovalWorkflow> ApprovalWorkflows { get; }
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
    DbSet<EventOutbox> EventOutboxes { get; }
    DbSet<Department> Departments { get; }

    // Core HRM DbSets
    DbSet<HrmDepartment> HrmDepartments { get; }
    DbSet<HrmDesignation> HrmDesignations { get; }
    DbSet<HrmEmployee> HrmEmployees { get; }
    DbSet<HrmAttendance> HrmAttendances { get; }
    DbSet<HrmLeaveApplication> HrmLeaveApplications { get; }
    DbSet<HrmLeaveType> HrmLeaveTypes { get; }
    DbSet<HrmPayrollRun> HrmPayrollRuns { get; }
    DbSet<HrmPayslip> HrmPayslips { get; }
    DbSet<HrmPayslipDtl> HrmPayslipDtls { get; }
    DbSet<HrmSalaryComponent> HrmSalaryComponents { get; }
    DbSet<HrmShift> HrmShifts { get; }
    DbSet<HrmHoliday> HrmHolidays { get; }
    DbSet<HrmGrade> HrmGrades { get; }

    // Expanded HRM DbSets (All 36 entities)
    DbSet<HrmAttendanceAdjustment> HrmAttendanceAdjustments { get; }
    DbSet<HrmLeaveBalance> HrmLeaveBalances { get; }
    DbSet<HrmLeaveLedger> HrmLeaveLedgers { get; }
    DbSet<HrmLeavePolicySetup> HrmLeavePolicySetups { get; }
    DbSet<HrmLeaveApplicationRule> HrmLeaveApplicationRules { get; }
    DbSet<HrmOvertime> HrmOvertimes { get; }
    DbSet<HrmShiftRoster> HrmShiftRosters { get; }
    DbSet<HrmShiftRosterLine> HrmShiftRosterLines { get; }
    DbSet<HrmBonusRun> HrmBonusRuns { get; }
    DbSet<HrmBonusLine> HrmBonusLines { get; }
    DbSet<HrmBonusScopeDesignation> HrmBonusScopeDesignations { get; }
    DbSet<HrmBonusScopeEmployee> HrmBonusScopeEmployees { get; }
    DbSet<HrmLoanAdvance> HrmLoanAdvances { get; }
    DbSet<HrmPayrollPolicy> HrmPayrollPolicies { get; }
    DbSet<HrmSalaryStructure> HrmSalaryStructures { get; }
    DbSet<HrmSalaryStructureDtl> HrmSalaryStructureDtls { get; }
    DbSet<HrmTaxSlab> HrmTaxSlabs { get; }
    DbSet<HrmCandidate> HrmCandidates { get; }
    DbSet<HrmJobRequisition> HrmJobRequisitions { get; }
    DbSet<HrmOffer> HrmOffers { get; }
    DbSet<HrmEmployeeMovement> HrmEmployeeMovements { get; }
    DbSet<HrmFinalSettlement> HrmFinalSettlements { get; }
    DbSet<HrmGradeStep> HrmGradeSteps { get; }

    // FIN Module DbSets (All 11 entities)
    DbSet<FinAccount> FinAccounts { get; }
    DbSet<FinAccountGroup> FinAccountGroups { get; }
    DbSet<FinAccountBalance> FinAccountBalances { get; }
    DbSet<FinGlMap> FinGlMaps { get; }
    DbSet<FinVoucher> FinVouchers { get; }
    DbSet<FinVoucherDtl> FinVoucherDtls { get; }
    DbSet<FinVoucherType> FinVoucherTypes { get; }
    DbSet<FinLedger> FinLedgers { get; }
    DbSet<FinBankAccount> FinBankAccounts { get; }
    DbSet<FinBankRecon> FinBankRecons { get; }
    DbSet<FinBankReconLine> FinBankReconLines { get; }

    // INV Module DbSets (All 20 entities)
    DbSet<InvProduct> InvProducts { get; }
    DbSet<InvCategory> InvCategories { get; }
    DbSet<InvBrand> InvBrands { get; }
    DbSet<InvUom> InvUoms { get; }
    DbSet<InvUomConversion> InvUomConversions { get; }
    DbSet<InvProductAttribute> InvProductAttributes { get; }
    DbSet<InvProductAttributeValue> InvProductAttributeValues { get; }
    DbSet<InvProductBarcode> InvProductBarcodes { get; }
    DbSet<InvProductVariant> InvProductVariants { get; }
    DbSet<InvWarehouse> InvWarehouses { get; }
    DbSet<InvRack> InvRacks { get; }
    DbSet<InvStock> InvStocks { get; }
    DbSet<InvStockLedger> InvStockLedgers { get; }
    DbSet<InvBatch> InvBatches { get; }
    DbSet<InvReorder> InvReorders { get; }
    DbSet<InvValuationLayer> InvValuationLayers { get; }
    DbSet<InvStockAdjustment> InvStockAdjustments { get; }
    DbSet<InvStockAdjustmentDtl> InvStockAdjustmentDtls { get; }
    DbSet<InvStockTransfer> InvStockTransfers { get; }
    DbSet<InvStockTransferDtl> InvStockTransferDtls { get; }

    // PUR Module DbSets (All 15 entities)
    DbSet<PurSupplier> PurSuppliers { get; }
    DbSet<PurSupplierProduct> PurSupplierProducts { get; }
    DbSet<PurSupplierLedger> PurSupplierLedgers { get; }
    DbSet<PurOrder> PurOrders { get; }
    DbSet<PurOrderDtl> PurOrderDtls { get; }
    DbSet<PurReceipt> PurReceipts { get; }
    DbSet<PurReceiptDtl> PurReceiptDtls { get; }
    DbSet<PurInvoice> PurInvoices { get; }
    DbSet<PurInvoiceDtl> PurInvoiceDtls { get; }
    DbSet<PurReturn> PurReturns { get; }
    DbSet<PurReturnDtl> PurReturnDtls { get; }
    DbSet<PurPayment> PurPayments { get; }
    DbSet<PurPaymentAlloc> PurPaymentAllocs { get; }
    DbSet<PurLandedCost> PurLandedCosts { get; }
    DbSet<PurLandedCostAlloc> PurLandedCostAllocs { get; }

    // SAL Module DbSets (All 8 entities)
    DbSet<SalCustomer> SalCustomers { get; }
    DbSet<SalCustomerLedger> SalCustomerLedgers { get; }
    DbSet<SalInvoice> SalInvoices { get; }
    DbSet<SalInvoiceDtl> SalInvoiceDtls { get; }
    DbSet<SalReceipt> SalReceipts { get; }
    DbSet<SalReceiptAlloc> SalReceiptAllocs { get; }
    DbSet<SalReturn> SalReturns { get; }
    DbSet<SalReturnDtl> SalReturnDtls { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Raw-SQL / transaction surface. Needed because several RBAC and reporting queries are
    /// ported verbatim from the Java <c>@Query(nativeQuery = true)</c> definitions — reproducing
    /// their <c>GROUP BY</c> / <c>MAX</c> / <c>MIN</c> / <c>HAVING</c> / <c>NULLS LAST</c>
    /// semantics in LINQ would risk silent behavioural drift.
    /// </summary>
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
}
