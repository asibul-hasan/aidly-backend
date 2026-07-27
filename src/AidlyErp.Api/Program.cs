using System.Text;
using System.Text.Json;
using AidlyErp.Api.Middleware;
using AidlyErp.Api.Services;
using AidlyErp.Application.Auth.Services;
using AidlyErp.Application.Common.Interfaces;
using AidlyErp.Application.Common.Response;
using AidlyErp.Application.Common.Security;
using AidlyErp.Application.Common.Services;
using AidlyErp.Application.Common.Utils;
using AidlyErp.Infrastructure.Persistence;
using AidlyErp.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

/// <summary>Swagger document groups, mirroring the Java SwaggerConfig GroupedOpenApi beans.</summary>
var SwaggerGroups = new (string Name, string Title)[]
{
    ("core", "1. Core — Authentication"),
    ("sys",  "2. System — Configuration"),
    ("hrm",  "3. HRM — Human Resources"),
    ("fin",  "4. FIN — Finance"),
    ("inv",  "5. INV — Inventory"),
    ("pur",  "6. PUR — Purchase"),
    ("sal",  "7. SAL — Sales")
};

// ---------------------------------------------------------------------------
// MVC + JSON
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        // Java JacksonConfig: accept a JSON number where a string is declared
        // (the frontend sends e.g. "company_type": 1 for a String field).
        options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
    });

// ---------------------------------------------------------------------------
// Request-scoped context + application services
// ---------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICompanyBranchContext, CompanyBranchContext>();
builder.Services.AddScoped<ICurrentPermissionContext, CurrentPermissionContext>();
builder.Services.AddScoped<AuditingAndTenantInterceptor>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISysLogService, SysLogService>();
builder.Services.AddScoped<AidlyErp.Application.Bootstrap.IAppBootstrapService, AidlyErp.Application.Bootstrap.AppBootstrapService>();

// Bounded queue for fire-and-forget work (audit-log writes). See BackgroundWorkQueue for why
// this is capped rather than unbounded.
builder.Services.AddSingleton<IBackgroundWorkQueue, BackgroundWorkQueue>();
builder.Services.AddHostedService<BackgroundWorkQueueHostedService>();
builder.Services.AddScoped<ICommonLookupService, CommonLookupService>();
builder.Services.AddScoped<FileStorageUtil>();

// Auth support services (ports of the Java core.auth.service package)
builder.Services.AddSingleton<LoginAttemptLimiter>();          // sliding window must outlive the request
builder.Services.AddSingleton<IAuthConfigService, AuthConfigService>();
builder.Services.AddSingleton<IApplicationDbContextFactory, ApplicationDbContextFactory>();
builder.Services.AddScoped<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddScoped<IAuthRuntimeValidationService, AuthRuntimeValidationService>();
builder.Services.AddScoped<ISysSessionService, SysSessionService>();
builder.Services.AddScoped<IRbacAuthorizationService, RbacAuthorizationService>();

// SYS Module Services
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1001Service, AidlyErp.Application.Sys.Services.Sys1001Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1002Service, AidlyErp.Application.Sys.Services.Sys1002Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1003Service, AidlyErp.Application.Sys.Services.Sys1003Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1004Service, AidlyErp.Application.Sys.Services.Sys1004Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1005Service, AidlyErp.Application.Sys.Services.Sys1005Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1006Service, AidlyErp.Application.Sys.Services.Sys1006Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1007Service, AidlyErp.Application.Sys.Services.Sys1007Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1008Service, AidlyErp.Application.Sys.Services.Sys1008Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1101Service, AidlyErp.Application.Sys.Services.Sys1101Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1103Service, AidlyErp.Application.Sys.Services.Sys1103Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1104Service, AidlyErp.Application.Sys.Services.Sys1104Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1105Service, AidlyErp.Application.Sys.Services.Sys1105Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1107Service, AidlyErp.Application.Sys.Services.Sys1107Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1108Service, AidlyErp.Application.Sys.Services.Sys1108Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1109Service, AidlyErp.Application.Sys.Services.Sys1109Service>();
builder.Services.AddSingleton<AidlyErp.Application.Sys.Services.IPathFormCacheInvalidator, AidlyErp.Api.Controllers.Sys.PathFormCacheInvalidator>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1201Service, AidlyErp.Application.Sys.Services.Sys1201Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1202Service, AidlyErp.Application.Sys.Services.Sys1202Service>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISys1203Service, AidlyErp.Application.Sys.Services.Sys1203Service>();

// SYS shared services (the generic /api/v1/sys/* endpoints)
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISysModuleService, AidlyErp.Application.Sys.Services.SysModuleService>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ISysSubmoduleService, AidlyErp.Application.Sys.Services.SysSubmoduleService>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.IMenuService, AidlyErp.Application.Sys.Services.MenuService>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.ICompanyService, AidlyErp.Application.Sys.Services.CompanyService>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.IBranchService, AidlyErp.Application.Sys.Services.BranchService>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.IRoleService, AidlyErp.Application.Sys.Services.RoleService>();
builder.Services.AddScoped<AidlyErp.Application.Sys.Services.IUserService, AidlyErp.Application.Sys.Services.UserService>();

// FIN Module Services
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFin1001Service, AidlyErp.Application.Fin.Services.Fin1001Service>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFin1002Service, AidlyErp.Application.Fin.Services.Fin1002Service>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFin1003Service, AidlyErp.Application.Fin.Services.Fin1003Service>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFin1004Service, AidlyErp.Application.Fin.Services.Fin1004Service>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFin1005Service, AidlyErp.Application.Fin.Services.Fin1005Service>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFin1006Service, AidlyErp.Application.Fin.Services.Fin1006Service>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFin1101Service, AidlyErp.Application.Fin.Services.Fin1101Service>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFin1102Service, AidlyErp.Application.Fin.Services.Fin1102Service>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFin1201Service, AidlyErp.Application.Fin.Services.Fin1201Service>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFinPostingService, AidlyErp.Application.Fin.Services.FinPostingService>();
builder.Services.AddHostedService<AidlyErp.Api.Services.FinPostingBackgroundService>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFinReportService, AidlyErp.Application.Fin.Services.FinReportService>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFin1308ChartOfAccountsPdfService, AidlyErp.Application.Fin.Services.Fin1308ChartOfAccountsPdfService>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFin1401Service, AidlyErp.Application.Fin.Services.Fin1401Service>();
builder.Services.AddScoped<AidlyErp.Application.Fin.Services.IFinApprovalListener, AidlyErp.Application.Fin.Services.FinApprovalListener>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IApprovalService, AidlyErp.Application.Common.Services.ApprovalService>();

// HRM Module Services
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1206Service, AidlyErp.Application.Hrm.Services.Hrm1206Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1003Service, AidlyErp.Application.Hrm.Services.Hrm1003Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1004Service, AidlyErp.Application.Hrm.Services.Hrm1004Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1005Service, AidlyErp.Application.Hrm.Services.Hrm1005Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1006Service, AidlyErp.Application.Hrm.Services.Hrm1006Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1007Service, AidlyErp.Application.Hrm.Services.Hrm1007Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1008Service, AidlyErp.Application.Hrm.Services.Hrm1008Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1009Service, AidlyErp.Application.Hrm.Services.Hrm1009Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrmDepartmentService, AidlyErp.Application.Hrm.Services.HrmDepartmentService>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrmDesignationService, AidlyErp.Application.Hrm.Services.HrmDesignationService>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrmEmployeeService, AidlyErp.Application.Hrm.Services.HrmEmployeeService>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1102Service, AidlyErp.Application.Hrm.Services.Hrm1102Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1202Service, AidlyErp.Application.Hrm.Services.Hrm1202Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1203Service, AidlyErp.Application.Hrm.Services.Hrm1203Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1204Service, AidlyErp.Application.Hrm.Services.Hrm1204Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1205Service, AidlyErp.Application.Hrm.Services.Hrm1205Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1207Service, AidlyErp.Application.Hrm.Services.Hrm1207Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrmLeavePolicyService, AidlyErp.Application.Hrm.Services.HrmLeavePolicyService>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.HrmApprovalListener>();

// HRM — operational services (were missing from DI — see MIGRATION-AUDIT.md)
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1101Service, AidlyErp.Application.Hrm.Services.Hrm1101Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1103Service, AidlyErp.Application.Hrm.Services.Hrm1103Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1104Service, AidlyErp.Application.Hrm.Services.Hrm1104Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1105Service, AidlyErp.Application.Hrm.Services.Hrm1105Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1106Service, AidlyErp.Application.Hrm.Services.Hrm1106Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1201Service, AidlyErp.Application.Hrm.Services.Hrm1201Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1301Service, AidlyErp.Application.Hrm.Services.Hrm1301Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1303Service, AidlyErp.Application.Hrm.Services.Hrm1303Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1401Service, AidlyErp.Application.Hrm.Services.Hrm1401Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1402Service, AidlyErp.Application.Hrm.Services.Hrm1402Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IHrm1404Service, AidlyErp.Application.Hrm.Services.Hrm1404Service>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.ILeaveRuleEngine, AidlyErp.Application.Hrm.Services.LeaveRuleEngine>();
builder.Services.AddScoped<AidlyErp.Application.Hrm.Services.IPayrollCalculationService, AidlyErp.Application.Hrm.Services.PayrollCalculationService>();

// HRM — repositories (36 repositories — port of Spring Data JPA repositories)
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmDepartmentRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmDepartmentRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmDesignationRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmDesignationRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmEmployeeRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmEmployeeRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmShiftRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmShiftRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmGradeRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmGradeRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmGradeStepRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmGradeStepRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmLeaveTypeRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmLeaveTypeRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmHolidayRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmHolidayRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmSalaryComponentRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmSalaryComponentRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmPayrollPolicyRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmPayrollPolicyRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmTaxSlabRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmTaxSlabRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmAttendanceRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmAttendanceRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmAttendanceAdjustmentRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmAttendanceAdjustmentRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmShiftRosterRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmShiftRosterRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmShiftRosterLineRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmShiftRosterLineRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmOvertimeRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmOvertimeRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmEmployeeMovementRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmEmployeeMovementRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmSalaryStructureRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmSalaryStructureRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmSalaryStructureDtlRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmSalaryStructureDtlRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmPayrollRunRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmPayrollRunRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmPayslipRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmPayslipRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmPayslipDtlRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmPayslipDtlRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmBonusRunRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmBonusRunRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmBonusLineRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmBonusLineRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmBonusScopeDesignationRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmBonusScopeDesignationRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmBonusScopeEmployeeRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmBonusScopeEmployeeRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmLoanAdvanceRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmLoanAdvanceRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmFinalSettlementRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmFinalSettlementRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmLeaveApplicationRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmLeaveApplicationRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmLeaveBalanceRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmLeaveBalanceRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmLeaveLedgerRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmLeaveLedgerRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmLeavePolicySetupRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmLeavePolicySetupRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmLeaveApplicationRuleRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmLeaveApplicationRuleRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmJobRequisitionRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmJobRequisitionRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmCandidateRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmCandidateRepository>();
builder.Services.AddScoped<AidlyErp.Application.Common.Interfaces.IHrmOfferRepository, AidlyErp.Infrastructure.Persistence.Repositories.HrmOfferRepository>();

// SAL Module Services
builder.Services.AddScoped<AidlyErp.Application.Sal.Services.ISalArLedgerService, AidlyErp.Application.Sal.Services.SalArLedgerService>();
builder.Services.AddScoped<AidlyErp.Application.Sal.Services.ISal1001Service, AidlyErp.Application.Sal.Services.Sal1001Service>();
builder.Services.AddScoped<AidlyErp.Application.Sal.Services.ISal1101Service, AidlyErp.Application.Sal.Services.Sal1101Service>();
builder.Services.AddScoped<AidlyErp.Application.Sal.Services.ISal1102Service, AidlyErp.Application.Sal.Services.Sal1102Service>();
builder.Services.AddScoped<AidlyErp.Application.Sal.Services.ISal1103Service, AidlyErp.Application.Sal.Services.Sal1103Service>();
builder.Services.AddScoped<AidlyErp.Application.Sal.Services.ISalApprovalListener, AidlyErp.Application.Sal.Services.SalApprovalListener>();

// INV Module Services
builder.Services.AddScoped<AidlyErp.Application.Inv.Engine.IInvStockPostingService, AidlyErp.Application.Inv.Engine.InvStockPostingService>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInvDocSequenceService, AidlyErp.Application.Inv.Services.InvDocSequenceService>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInvPeriodResolver, AidlyErp.Application.Inv.Services.InvPeriodResolver>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv1001Service, AidlyErp.Application.Inv.Services.Inv1001Service>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv1003Service, AidlyErp.Application.Inv.Services.Inv1003Service>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv1004Service, AidlyErp.Application.Inv.Services.Inv1004Service>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv1005Service, AidlyErp.Application.Inv.Services.Inv1005Service>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv1101Service, AidlyErp.Application.Inv.Services.Inv1101Service>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv1102Service, AidlyErp.Application.Inv.Services.Inv1102Service>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv2001Service, AidlyErp.Application.Inv.Services.Inv2001Service>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv2003Service, AidlyErp.Application.Inv.Services.Inv2003Service>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv1006Service, AidlyErp.Application.Inv.Services.Inv1006Service>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv2002Service, AidlyErp.Application.Inv.Services.Inv2002Service>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv2004Service, AidlyErp.Application.Inv.Services.Inv2004Service>();
builder.Services.AddScoped<AidlyErp.Application.Inv.Services.IInv2005Service, AidlyErp.Application.Inv.Services.Inv2005Service>();

// PUR Module Services
builder.Services.AddScoped<AidlyErp.Application.Pur.Services.IPurApLedgerService, AidlyErp.Application.Pur.Services.PurApLedgerService>();
builder.Services.AddScoped<AidlyErp.Application.Pur.Services.IPur1001Service, AidlyErp.Application.Pur.Services.Pur1001Service>();
builder.Services.AddScoped<AidlyErp.Application.Pur.Services.IPur1102Service, AidlyErp.Application.Pur.Services.Pur1102Service>();
builder.Services.AddScoped<AidlyErp.Application.Pur.Services.IPur1104Service, AidlyErp.Application.Pur.Services.Pur1104Service>();
builder.Services.AddScoped<AidlyErp.Application.Pur.Services.IPur1002Service, AidlyErp.Application.Pur.Services.Pur1002Service>();
builder.Services.AddScoped<AidlyErp.Application.Pur.Services.IPur1101Service, AidlyErp.Application.Pur.Services.Pur1101Service>();
builder.Services.AddScoped<AidlyErp.Application.Pur.Services.IPur1103Service, AidlyErp.Application.Pur.Services.Pur1103Service>();
builder.Services.AddScoped<AidlyErp.Application.Pur.Services.IPur1105Service, AidlyErp.Application.Pur.Services.Pur1105Service>();
builder.Services.AddScoped<AidlyErp.Application.Pur.Services.IPur1106Service, AidlyErp.Application.Pur.Services.Pur1106Service>();

// ---------------------------------------------------------------------------
// Persistence
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=aidly_sme;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<AuditingAndTenantInterceptor>();
    options.UseNpgsql(connectionString)
           .AddInterceptors(interceptor);
});

builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

// ---------------------------------------------------------------------------
// Authentication — JWT bearer.
//
// Previously UseAuthentication() ran with no scheme registered, so every request was
// anonymous: the tenant middleware never populated company/branch and the API was
// effectively unauthenticated. This registers the same HMAC-SHA256 validation the Java
// JwtAuthenticationFilter performs.
// ---------------------------------------------------------------------------
var jwtSecret = builder.Configuration["Aidly:Jwt:Secret"]
                ?? Environment.GetEnvironmentVariable("JWT_SECRET") ?? string.Empty;

if (jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "JWT secret must be at least 32 characters long for HMAC-SHA256 security.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// CORS — mirrors the Java SecurityConfig allowed-origin patterns
// ---------------------------------------------------------------------------
const string CorsPolicy = "AidlyCors";
builder.Services.AddCors(options =>
{
    // A predicate, not WithOrigins. ASP.NET matches WithOrigins entries as literal strings —
    // "http://localhost:*" never matches "http://localhost:4202", and
    // SetIsOriginAllowedToAllowWildcardSubdomains only covers subdomains, not ports. That is
    // why the browser was reporting a CORS error against the dev frontend.
    options.AddPolicy(CorsPolicy, policy => policy
        .SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;

            var host = uri.Host;

            // Any localhost / loopback port — dev servers move around.
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.Equals("127.0.0.1", StringComparison.Ordinal)
                || host.Equals("::1", StringComparison.Ordinal))
            {
                return true;
            }

            // Hugging Face Spaces, on any subdomain.
            if (host.Equals("hf.space", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".hf.space", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // infoaidtech.net and any subdomain, on either scheme and any port.
            // EndsWith(".infoaidtech.net") — not Contains — so evil-infoaidtech.net is rejected.
            return host.Equals("infoaidtech.net", StringComparison.OrdinalIgnoreCase)
                   || host.EndsWith(".infoaidtech.net", StringComparison.OrdinalIgnoreCase);
        })
        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH")
        .AllowAnyHeader()
        .WithExposedHeaders("x-auth-token", "Authorization")
        .AllowCredentials()
        .SetPreflightMaxAge(TimeSpan.FromSeconds(3600)));
});

// ---------------------------------------------------------------------------
// OpenAPI — port of the Java SwaggerConfig: one document per module group, each
// carrying the JWT bearer scheme so the "Authorize" affordance is available.
// ---------------------------------------------------------------------------
foreach (var (groupName, groupTitle) in SwaggerGroups)
{
    var prefix = groupName == "core" ? "/api/v1/auth" : $"/api/v1/{groupName}";

    builder.Services.AddOpenApi(groupName, options =>
    {
        // Route an operation into its group by URL prefix, mirroring Java's pathsToMatch.
        options.ShouldInclude = description =>
            ("/" + (description.RelativePath ?? string.Empty))
            .StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new Microsoft.OpenApi.OpenApiInfo
            {
                Title = groupTitle,
                Version = "v1.0.0",
                Description = "REST API documentation for the **Aidly ERP** modular monolith backend.\n\n"
                              + "Authenticate via `POST /api/v1/auth/login`, copy the `accessToken` from the "
                              + "response, then send it as `Authorization: Bearer <token>`.",
                Contact = new Microsoft.OpenApi.OpenApiContact
                {
                    Name = "InfoAidTech",
                    Email = "dev@infoaidtech.com"
                },
                License = new Microsoft.OpenApi.OpenApiLicense
                {
                    Name = "Proprietary",
                    Url = new Uri("https://infoaidtech.com")
                }
            };

            document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
            document.Components.SecuritySchemes ??=
                new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();

            document.Components.SecuritySchemes["bearerAuth"] = new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = Microsoft.OpenApi.ParameterLocation.Header,
                Description = "Paste the JWT access token obtained from POST /api/v1/auth/login"
            };

            return Task.CompletedTask;
        });
    });
}

// ---------------------------------------------------------------------------
// Response compression — the Java server.compression settings.
// ---------------------------------------------------------------------------
if (builder.Configuration.GetValue("ResponseCompression:Enabled", true))
{
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.MimeTypes = builder.Configuration
            .GetSection("ResponseCompression:MimeTypes").Get<string[]>()
            ?? new[] { "application/json", "application/xml", "text/plain", "text/css", "application/javascript" };
    });
}

var app = builder.Build();

// ---------------------------------------------------------------------------
// HTTP pipeline
// ---------------------------------------------------------------------------
app.UseResponseCompression();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Security response headers — Java SecurityConfig .headers(...)
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Frame-Options"] = "DENY";
    headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'";
    headers["X-Content-Type-Options"] = "nosniff";
    await next();
});

if (app.Environment.IsDevelopment())
{
    // One document per module group, served at the Java springdoc path so existing
    // tooling and bookmarks keep working.
    app.MapOpenApi("/v3/api-docs/{documentName}.json");
}
else
{
    // includeSubDomains, max-age 31536000 — matches the Java HSTS configuration
    app.UseHsts();
}

app.UseHttpsRedirection();

// Java WebConfig.addResourceHandlers: serve the uploads directory at /uploads/**
var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
Directory.CreateDirectory(uploadPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadPath),
    RequestPath = "/uploads"
});

app.UseCors(CorsPolicy);

app.UseAuthentication();

// Order mirrors the Java WebConfig interceptor chain:
//   1) tenant context from the JWT (company/branch/scope/permBits) — must be first, everything
//      downstream reads it, including the tenant query filter;
//   2) RBAC form-permission gate;
//   3) framework authorization.
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<RbacAuthorizationMiddleware>();
app.UseAuthorization();
app.UseMiddleware<AuditLogMiddleware>();

// Wrap framework-generated 404 / 405 / 415 responses in the standard envelope, matching the
// Java handlers for NoResourceFound / MethodNotSupported / MediaTypeNotSupported.
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    if (response.ContentLength.HasValue || response.HasStarted) return;

    var request = statusCodeContext.HttpContext.Request;
    var message = response.StatusCode switch
    {
        404 => $"Endpoint not found: {request.Method} {request.Path}",
        405 => $"HTTP method '{request.Method}' is not supported for this endpoint.",
        415 => $"Content-Type '{request.ContentType}' is not supported for this endpoint.",
        401 => "Authentication required.",
        403 => "You do not have permission to perform this action.",
        _ => $"Request failed with status {response.StatusCode}."
    };

    response.ContentType = "application/json";
    await response.WriteAsync(JsonSerializer.Serialize(
        ApiResponse<object>.Error(response.StatusCode, message),
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
});

app.MapControllers();

app.Run();
