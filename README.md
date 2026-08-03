# Aidly ERP — .NET Backend

.NET 10 / EF Core / PostgreSQL backend application powering **Aidly ERP**.

---

## 📁 Project Folder & File Structure

Below is the complete project directory structure including all source files, modules, controllers, services, entities, contracts, middleware, and test suites.

```text
sme-dotnet-backend/
├── .gitignore
├── AidlyErp.slnx
├── MIGRATION-AUDIT.md
├── README.md
├── src/
│   ├── Host/
│   │   └── AidlyErp.Api/
│   │       ├── AidlyErp.Api.csproj
│   │       ├── AidlyErp.Api.csproj.user
│   │       ├── Program.cs
│   │       ├── SmeErp.Api.http
│   │       ├── appsettings.json
│   │       ├── appsettings.Development.json
│   │       ├── appsettings.Local.json
│   │       ├── appsettings.Production.json
│   │       ├── appsettings.Test.json
│   │       ├── Bootstrap/
│   │       │   └── AppBootstrapService.cs
│   │       ├── Controllers/
│   │       │   ├── ApiControllerBase.cs
│   │       │   ├── AppBootstrapController.cs
│   │       │   ├── AuthController.cs
│   │       │   ├── CommonLookupController.cs
│   │       │   ├── CompanyController.cs
│   │       │   ├── FinControllers.cs
│   │       │   ├── Hrm1010Controller.cs
│   │       │   ├── HrmAttendanceController.cs.legacy
│   │       │   ├── HrmControllersExpanded.cs
│   │       │   ├── HrmDepartmentController.cs.legacy
│   │       │   ├── HrmDesignationController.cs.legacy
│   │       │   ├── HrmEmployeeController.cs.legacy
│   │       │   ├── HrmLeaveApplicationController.cs.legacy
│   │       │   ├── HrmLeaveTypeController.cs.legacy
│   │       │   ├── HrmPayrollRunController.cs.legacy
│   │       │   ├── HrmShiftController.cs.legacy
│   │       │   ├── InvControllers.cs.legacy
│   │       │   ├── SysControllersPart1.cs.legacy
│   │       │   ├── SysControllersPart2.cs.legacy
│   │       │   ├── WelcomeController.cs
│   │       │   ├── Inv/
│   │       │   │   ├── Inv1001Controller.cs
│   │       │   │   ├── Inv1003Controller.cs
│   │       │   │   ├── Inv1004Controller.cs
│   │       │   │   └── Inv1005Controller.cs
│   │       │   ├── Pur/
│   │       │   │   ├── Pur1001Controller.cs
│   │       │   │   ├── Pur1102Controller.cs
│   │       │   │   ├── Pur1104Controller.cs
│   │       │   │   └── PurApLedgerController.cs
│   │       │   ├── Sal/
│   │       │   │   ├── Sal1001Controller.cs
│   │       │   │   ├── Sal1101Controller.cs
│   │       │   │   ├── Sal1102Controller.cs
│   │       │   │   ├── Sal1103Controller.cs
│   │       │   │   └── SalArLedgerController.cs
│   │       │   └── Sys/
│   │       │       ├── ApprovalInboxController.cs
│   │       │       ├── CompanyAndBranchControllers.cs
│   │       │       ├── Sys1001Controller.cs
│   │       │       ├── Sys1002Controller.cs
│   │       │       ├── Sys1003Controller.cs
│   │       │       ├── Sys1004Controller.cs
│   │       │       ├── Sys1005Controller.cs
│   │       │       ├── Sys1006Controller.cs
│   │       │       ├── Sys1007Controller.cs
│   │       │       ├── Sys1008Controller.cs
│   │       │       ├── Sys1101Controller.cs
│   │       │       ├── Sys1103Controller.cs
│   │       │       ├── Sys1104Controller.cs
│   │       │       ├── Sys1105Controller.cs
│   │       │       ├── Sys1107Controller.cs
│   │       │       ├── Sys1108Controller.cs
│   │       │       ├── Sys1109Controller.cs
│   │       │       ├── Sys1201Controller.cs
│   │       │       ├── Sys1202Controller.cs
│   │       │       ├── Sys1203Controller.cs
│   │       │       ├── SysCatalogControllers.cs
│   │       │       ├── SysRoleController.cs
│   │       │       └── SysUserController.cs
│   │       ├── Middleware/
│   │       │   ├── AuditLogMiddleware.cs
│   │       │   ├── GlobalExceptionHandlerMiddleware.cs
│   │       │   ├── RbacAuthorizationMiddleware.cs
│   │       │   └── TenantContextMiddleware.cs
│   │       ├── Properties/
│   │       │   └── launchSettings.json
│   │       └── Services/
│   │           ├── BackgroundWorkQueue.cs
│   │           ├── DbFixer.cs
│   │           └── FinPostingService.cs
│   │
│   ├── Modules/
│   │   ├── Fin/  (Financial Management)
│   │   │   ├── AidlyErp.Fin.Application/
│   │   │   │   ├── AidlyErp.Fin.Application.csproj
│   │   │   │   ├── Dto/
│   │   │   │   │   └── FinDtos.cs
│   │   │   │   ├── Interfaces/
│   │   │   │   │   └── IFinDbContext.cs
│   │   │   │   └── Services/
│   │   │   │       ├── Fin1001Service.cs
│   │   │   │       ├── Fin1002Service.cs
│   │   │   │       ├── Fin1003Service.cs
│   │   │   │       ├── Fin1004Service.cs
│   │   │   │       ├── Fin1005Service.cs
│   │   │   │       ├── Fin1006Service.cs
│   │   │   │       ├── Fin1101Service.cs
│   │   │   │       ├── Fin1102Service.cs
│   │   │   │       ├── Fin1201Service.cs
│   │   │   │       ├── Fin1308ChartOfAccountsPdfService.cs
│   │   │   │       ├── Fin1401Service.cs
│   │   │   │       ├── FinPostingService.cs
│   │   │   │       └── FinReportService.cs
│   │   │   ├── AidlyErp.Fin.Contracts/
│   │   │   │   ├── AidlyErp.Fin.Contracts.csproj
│   │   │   │   └── GlPostingPayload.cs
│   │   │   ├── AidlyErp.Fin.Domain/
│   │   │   │   ├── AidlyErp.Fin.Domain.csproj
│   │   │   │   ├── FinChartOfAccountsEntities.cs
│   │   │   │   └── FinVoucherAndBankingEntities.cs
│   │   │   └── AidlyErp.Fin.Infrastructure/
│   │   │       ├── AidlyErp.Fin.Infrastructure.csproj
│   │   │       ├── FinDbContext.cs
│   │   │       └── FinModuleRegistration.cs
│   │   │
│   │   ├── Hrm/  (Human Resource Management)
│   │   │   ├── AidlyErp.Hrm.Application/
│   │   │   │   ├── AidlyErp.Hrm.Application.csproj
│   │   │   │   ├── Dto/
│   │   │   │   │   ├── Hrm1206LoanDto.cs
│   │   │   │   │   └── HrmDtos.cs
│   │   │   │   ├── Interfaces/
│   │   │   │   │   ├── IHrmDbContext.cs
│   │   │   │   │   └── IHrmRepositories.cs
│   │   │   │   └── Services/
│   │   │   │       ├── Hrm1003Service.cs
│   │   │   │       ├── Hrm1004Service.cs
│   │   │   │       ├── Hrm1005Service.cs
│   │   │   │       ├── Hrm1006Service.cs
│   │   │   │       ├── Hrm1007Service.cs
│   │   │   │       ├── Hrm1008Service.cs
│   │   │   │       ├── Hrm1009Service.cs
│   │   │   │       ├── Hrm1101Service.cs
│   │   │   │       ├── Hrm1102Service.cs
│   │   │   │       ├── Hrm1103Service.cs
│   │   │   │       ├── Hrm1104Service.cs
│   │   │   │       ├── Hrm1105Service.cs
│   │   │   │       ├── Hrm1106Service.cs
│   │   │   │       ├── Hrm1201Service.cs
│   │   │   │       ├── Hrm1202Service.cs
│   │   │   │       ├── Hrm1203Service.cs
│   │   │   │       ├── Hrm1204Service.cs
│   │   │   │       ├── Hrm1205Service.cs
│   │   │   │       ├── Hrm1206Service.cs
│   │   │   │       ├── Hrm1207Service.cs
│   │   │   │       ├── Hrm1301Service.cs
│   │   │   │       ├── Hrm1303Service.cs
│   │   │   │       ├── Hrm1401Service.cs
│   │   │   │       ├── Hrm1402Service.cs
│   │   │   │       ├── Hrm1404Service.cs
│   │   │   │       ├── HrmApprovalListener.cs
│   │   │   │       ├── HrmDepartmentService.cs
│   │   │   │       ├── HrmDesignationService.cs
│   │   │   │       ├── HrmEmployeeService.cs
│   │   │   │       ├── HrmLeavePolicyService.cs
│   │   │   │       ├── HrmValidation.cs
│   │   │   │       ├── LeaveRuleEngine.cs
│   │   │   │       └── PayrollCalculationService.cs
│   │   │   ├── AidlyErp.Hrm.Contracts/
│   │   │   │   ├── AidlyErp.Hrm.Contracts.csproj
│   │   │   │   ├── IEmployeeDirectory.cs
│   │   │   │   └── IHrmLookups.cs
│   │   │   ├── AidlyErp.Hrm.Domain/
│   │   │   │   ├── AidlyErp.Hrm.Domain.csproj
│   │   │   │   ├── HrmAttendance.cs
│   │   │   │   ├── HrmDepartment.cs
│   │   │   │   ├── HrmDesignation.cs
│   │   │   │   ├── HrmEmployee.cs
│   │   │   │   ├── HrmEntitiesExtra.cs
│   │   │   │   ├── HrmLeaveAndAttendanceEntities.cs
│   │   │   │   ├── HrmLeaveApplication.cs
│   │   │   │   ├── HrmLeaveType.cs
│   │   │   │   ├── HrmPayrollAndBonusEntities.cs
│   │   │   │   ├── HrmPayrollRun.cs
│   │   │   │   ├── HrmPayslip.cs
│   │   │   │   └── HrmRecruitmentAndLifecycleEntities.cs
│   │   │   └── AidlyErp.Hrm.Infrastructure/
│   │   │       ├── AidlyErp.Hrm.Infrastructure.csproj
│   │   │       ├── EmployeeDirectory.cs
│   │   │       ├── HrmDbContext.cs
│   │   │       ├── HrmLookups.cs
│   │   │       ├── HrmModuleRegistration.cs
│   │   │       └── HrmRepositories.cs
│   │   │
│   │   ├── Inv/  (Inventory Management)
│   │   │   ├── AidlyErp.Inv.Application/
│   │   │   │   ├── AidlyErp.Inv.Application.csproj
│   │   │   │   ├── Dto/
│   │   │   │   │   └── InvDtos.cs
│   │   │   │   ├── Engine/
│   │   │   │   │   └── InvEngine.cs
│   │   │   │   ├── Interfaces/
│   │   │   │   │   └── IInvDbContext.cs
│   │   │   │   └── Services/
│   │   │   │       ├── Inv1001Service.cs
│   │   │   │       ├── Inv1003Service.cs
│   │   │   │       ├── Inv1004Service.cs
│   │   │   │       ├── Inv1005Service.cs
│   │   │   │       ├── Inv1006Service.cs
│   │   │   │       ├── Inv1101Service.cs
│   │   │   │       ├── Inv1102Service.cs
│   │   │   │       ├── Inv2001Service.cs
│   │   │   │       ├── Inv2002Service.cs
│   │   │   │       ├── Inv2003Service.cs
│   │   │   │       ├── Inv2004Service.cs
│   │   │   │       ├── Inv2005Service.cs
│   │   │   │       ├── InvDocSequenceService.cs
│   │   │   │       └── InvPeriodResolver.cs
│   │   │   ├── AidlyErp.Inv.Contracts/
│   │   │   │   ├── AidlyErp.Inv.Contracts.csproj
│   │   │   │   ├── IInvCatalog.cs
│   │   │   │   └── IInvStockPostingService.cs
│   │   │   ├── AidlyErp.Inv.Domain/
│   │   │   │   ├── AidlyErp.Inv.Domain.csproj
│   │   │   │   ├── InvProductCatalogEntities.cs
│   │   │   │   ├── InvStockAndWarehouseEntities.cs
│   │   │   │   └── InvStockMovementEntities.cs
│   │   │   └── AidlyErp.Inv.Infrastructure/
│   │   │       ├── AidlyErp.Inv.Infrastructure.csproj
│   │   │       ├── InvCatalog.cs
│   │   │       ├── InvDbContext.cs
│   │   │       └── InvModuleRegistration.cs
│   │   │
│   │   ├── Pur/  (Purchase Management)
│   │   │   ├── AidlyErp.Pur.Application/
│   │   │   │   ├── AidlyErp.Pur.Application.csproj
│   │   │   │   ├── Dto/
│   │   │   │   │   └── PurDtos.cs
│   │   │   │   ├── Interfaces/
│   │   │   │   │   └── IPurDbContext.cs
│   │   │   │   └── Services/
│   │   │   │       ├── Pur1001Service.cs
│   │   │   │       ├── Pur1002Service.cs
│   │   │   │       ├── Pur1101Service.cs
│   │   │   │       ├── Pur1102Service.cs
│   │   │   │       ├── Pur1103Service.cs
│   │   │   │       ├── Pur1104Service.cs
│   │   │   │       ├── Pur1105Service.cs
│   │   │   │       ├── Pur1106Service.cs
│   │   │   │       └── PurApLedgerService.cs
│   │   │   ├── AidlyErp.Pur.Contracts/
│   │   │   │   └── AidlyErp.Pur.Contracts.csproj
│   │   │   ├── AidlyErp.Pur.Domain/
│   │   │   │   ├── AidlyErp.Pur.Domain.csproj
│   │   │   │   ├── PurReceiptAndInvoiceEntities.cs
│   │   │   │   └── PurSupplierAndOrderEntities.cs
│   │   │   └── AidlyErp.Pur.Infrastructure/
│   │   │       ├── AidlyErp.Pur.Infrastructure.csproj
│   │   │       ├── PurDbContext.cs
│   │   │       └── PurModuleRegistration.cs
│   │   │
│   │   ├── Sal/  (Sales Management)
│   │   │   ├── AidlyErp.Sal.Application/
│   │   │   │   ├── AidlyErp.Sal.Application.csproj
│   │   │   │   ├── Dto/
│   │   │   │   │   └── SalDtos.cs
│   │   │   │   ├── Interfaces/
│   │   │   │   │   └── ISalDbContext.cs
│   │   │   │   └── Services/
│   │   │   │       ├── Sal1001Service.cs
│   │   │   │       ├── Sal1101Service.cs
│   │   │   │       ├── Sal1102Service.cs
│   │   │   │       ├── Sal1103Service.cs
│   │   │   │       ├── SalApprovalListener.cs
│   │   │   │       └── SalArLedgerService.cs
│   │   │   ├── AidlyErp.Sal.Contracts/
│   │   │   │   └── AidlyErp.Sal.Contracts.csproj
│   │   │   ├── AidlyErp.Sal.Domain/
│   │   │   │   ├── AidlyErp.Sal.Domain.csproj
│   │   │   │   └── SalCustomerAndSalesEntities.cs
│   │   │   └── AidlyErp.Sal.Infrastructure/
│   │   │       ├── AidlyErp.Sal.Infrastructure.csproj
│   │   │       ├── SalDbContext.cs
│   │   │       └── SalModuleRegistration.cs
│   │   │
│   │   └── Sys/  (System Core & Administration)
│   │       ├── AidlyErp.Sys.Application/
│   │       │   ├── AidlyErp.Sys.Application.csproj
│   │       │   ├── Auth/
│   │       │   │   ├── Dto/
│   │       │   │   │   ├── AuthDtos.cs
│   │       │   │   │   └── RbacDtos.cs
│   │       │   │   └── Services/
│   │       │   │       ├── AuthConfigService.cs
│   │       │   │       ├── AuthRuntimeValidationService.cs
│   │       │   │       ├── AuthService.cs
│   │       │   │       ├── LoginAttemptLimiter.cs
│   │       │   │       ├── LoginAttemptService.cs
│   │       │   │       ├── RbacAuthorizationService.cs
│   │       │   │       └── SysSessionService.cs
│   │       │   ├── Dto/
│   │       │   │   ├── Sys1001CompanyDto.cs
│   │       │   │   ├── Sys1002BranchDto.cs
│   │       │   │   ├── Sys1003FinYearDto.cs
│   │       │   │   ├── Sys1004CurrencyDto.cs
│   │       │   │   ├── Sys1005VatTaxDto.cs
│   │       │   │   ├── Sys1006ExchangeRateDto.cs
│   │       │   │   ├── Sys1007CostCenterDto.cs
│   │       │   │   ├── Sys1008SettingDto.cs
│   │       │   │   ├── Sys1101Dtos.cs
│   │       │   │   ├── Sys1103Dtos.cs
│   │       │   │   ├── Sys1104Dtos.cs
│   │       │   │   ├── Sys1105AuditLogDto.cs
│   │       │   │   ├── Sys1107Dtos.cs
│   │       │   │   ├── Sys1108Dtos.cs
│   │       │   │   ├── Sys1109Dtos.cs
│   │       │   │   ├── Sys1201Dtos.cs
│   │       │   │   ├── Sys1202MenuRow.cs
│   │       │   │   ├── Sys1203FileDto.cs
│   │       │   │   └── SysSharedDtos.cs
│   │       │   ├── Interfaces/
│   │       │   │   ├── ISysDbContext.cs
│   │       │   │   └── ISysRepositories.cs
│   │       │   └── Services/
│   │       │       ├── ApprovalService.cs
│   │       │       ├── BranchService.cs
│   │       │       ├── CommonLookupService.cs
│   │       │       ├── CompanyService.cs
│   │       │       ├── MenuService.cs
│   │       │       ├── RoleService.cs
│   │       │       ├── Sys1001Service.cs
│   │       │       ├── Sys1002Service.cs
│   │       │       ├── Sys1003Service.cs
│   │       │       ├── Sys1004Service.cs
│   │       │       ├── Sys1005Service.cs
│   │       │       ├── Sys1006Service.cs
│   │       │       ├── Sys1007Service.cs
│   │       │       ├── Sys1008Service.cs
│   │       │       ├── Sys1101Service.cs
│   │       │       ├── Sys1103Service.cs
│   │       │       ├── Sys1104Service.cs
│   │       │       ├── Sys1105Service.cs
│   │       │       ├── Sys1107Service.cs
│   │       │       ├── Sys1108Service.cs
│   │       │       ├── Sys1109Service.cs
│   │       │       ├── Sys1201Service.cs
│   │       │       ├── Sys1202Service.cs
│   │       │       ├── Sys1203Service.cs
│   │       │       ├── SysLogService.cs
│   │       │       ├── SysModuleService.cs
│   │       │       ├── SysSubmoduleService.cs
│   │       │       └── UserService.cs
│   │       ├── AidlyErp.Sys.Contracts/
│   │       │   ├── AidlyErp.Sys.Contracts.csproj
│   │       │   ├── IApprovalService.cs
│   │       │   └── SysDirectory.cs
│   │       ├── AidlyErp.Sys.Domain/
│   │       │   ├── AidlyErp.Sys.Domain.csproj
│   │       │   ├── Branch.cs
│   │       │   ├── Company.cs
│   │       │   ├── Role.cs
│   │       │   ├── RolePermission.cs
│   │       │   ├── SysApprovalAndSecurityEntities.cs
│   │       │   ├── SysFinancialEntities.cs
│   │       │   ├── SysMenuAndModuleEntities.cs
│   │       │   ├── SysSession.cs
│   │       │   ├── User.cs
│   │       │   └── UserBranch.cs
│   │       └── AidlyErp.Sys.Infrastructure/
│   │           ├── AidlyErp.Sys.Infrastructure.csproj
│   │           ├── SysDbContext.cs
│   │           ├── SysDirectory.cs
│   │           ├── SysModuleRegistration.cs
│   │           └── SysRepositories.cs
│   │
│   └── Shared/
│       ├── AidlyErp.Shared.Contracts/
│       │   ├── AidlyErp.Shared.Contracts.csproj
│       │   ├── IJwtTokenService.cs
│       │   └── ISysLogService.cs
│       ├── AidlyErp.Shared.Core/
│       │   ├── AidlyErp.Shared.Core.csproj
│       │   ├── AuditEntity.cs
│       │   ├── Enums.cs
│       │   ├── EventOutbox.cs
│       │   ├── Abstractions/
│       │   │   ├── IFileStorage.cs
│       │   │   ├── IModuleDbContextFactory.cs
│       │   │   └── IUnitOfWork.cs
│       │   ├── Audit/
│       │   │   └── SysAuditLog.cs
│       │   ├── Dto/
│       │   │   └── LookupDtos.cs
│       │   ├── Exceptions/
│       │   │   └── CustomExceptions.cs
│       │   ├── Response/
│       │   │   └── ApiResponse.cs
│       │   ├── Security/
│       │   │   ├── ICompanyBranchContext.cs
│       │   │   ├── ICurrentPermissionContext.cs
│       │   │   └── SecurityUtils.cs
│       │   └── Utils/
│       │       └── CommonUtils.cs
│       └── AidlyErp.Shared.Infrastructure/
│           ├── AidlyErp.Shared.Infrastructure.csproj
│           ├── Persistence/
│           │   ├── AuditingAndTenantInterceptor.cs
│           │   ├── ModuleDbContext.cs
│           │   ├── ModuleDbContextFactory.cs
│           │   ├── ModuleRegistration.cs
│           │   └── UnitOfWork.cs
│           ├── Security/
│           │   └── JwtTokenService.cs
│           └── Storage/
│               └── FileStorageUtil.cs
│
└── tests/
    ├── AidlyErp.Tests/
    │   ├── AidlyErp.Tests.csproj
    │   └── UnitTest1.cs
    └── Architecture/
        └── AidlyErp.Architecture.Tests/
            ├── AidlyErp.Architecture.Tests.csproj
            ├── ModuleBoundaryTests.cs
            ├── SchemaDriftTests.cs
            └── appsettings.Local.json
```

---

## 🏛️ Architecture & Modular Breakdown

The solution follows a **Modular Monolith** architecture with clean separation across domain, application, infrastructure, and contract layers for each business module.

| Layer / Module | Project Name | Description |
|---|---|---|
| **Host** | `AidlyErp.Api` | Web API composition root, HTTP endpoints, Middleware & Bootstrap services. |
| **Fin (Finance)** | `AidlyErp.Fin.*` | Chart of accounts, general ledger, vouchers, banking, and financial reports. |
| **Hrm (HR & Payroll)** | `AidlyErp.Hrm.*` | Employee management, attendance, leave rules, loans, recruitment, and payroll calculation. |
| **Inv (Inventory)** | `AidlyErp.Inv.*` | Product catalog, warehouses, stock movements, and stock posting engine. |
| **Pur (Purchase)** | `AidlyErp.Pur.*` | Suppliers, purchase requisitions, orders, receipts, invoices, and AP ledger. |
| **Sal (Sales)** | `AidlyErp.Sal.*` | Customers, quotations, sales orders, delivery notes, sales invoices, and AR ledger. |
| **Sys (System Core)** | `AidlyErp.Sys.*` | RBAC authorization, multi-tenant company/branch management, system audit logs, and approval workflows. |
| **Shared** | `AidlyErp.Shared.*` | Common core abstractions, base entities, global response contracts, JWT token services, and shared persistence abstractions. |
| **Tests** | `AidlyErp.Tests`, `AidlyErp.Architecture.Tests` | Unit test suite and architecture boundary / schema drift enforcement tests. |

---

## ⚙️ Environment & Configuration

Configuration settings are specified in `appsettings.json` with environment variable overrides.

### Required Configuration Keys

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=aidly_db;Username=postgres;Password=your_password"
  },
  "Aidly": {
    "Jwt": {
      "Secret": "your_minimum_32_character_secret_key_here"
    }
  }
}
```

> **Note:** The JWT secret must be **≥ 32 characters**. The host validates secret key length at startup and will fail fast if invalid.

---

## 🚀 Building & Running

### Build Solution
```bash
dotnet build AidlyErp.slnx
```

### Run API Project
```bash
dotnet run --project src/Host/AidlyErp.Api
```

### OpenAPI Documentation
When running in `Development` environment, OpenAPI endpoints are exposed per module:
- `/v3/api-docs/core.json`
- `/v3/api-docs/sys.json`
- `/v3/api-docs/hrm.json`
- `/v3/api-docs/fin.json`
- `/v3/api-docs/inv.json`
- `/v3/api-docs/pur.json`
- `/v3/api-docs/sal.json`

### Run Architecture & Unit Tests
```bash
dotnet test tests/Architecture/AidlyErp.Architecture.Tests
dotnet test tests/AidlyErp.Tests
```
