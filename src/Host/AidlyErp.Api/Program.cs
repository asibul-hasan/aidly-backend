using AidlyErp.Sal.Infrastructure;
using AidlyErp.Pur.Infrastructure;
using AidlyErp.Inv.Infrastructure;
using AidlyErp.Fin.Infrastructure;
using AidlyErp.Hrm.Infrastructure;
using AidlyErp.Sys.Infrastructure;
using System.Text;
using System.Text.Json;
using AidlyErp.Api.Middleware;
using AidlyErp.Api.Services;
using AidlyErp.Sys.Application.Auth.Services;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Shared.Core.Response;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Services;
using AidlyErp.Shared.Core.Utils;
using AidlyErp.Shared.Infrastructure.Persistence;
using AidlyErp.Shared.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

// Local developer secrets — connection string and JWT key. Gitignored, and not an environment
// name, so the conventional appsettings.{Environment}.json probe never picks it up; it has to be
// added explicitly. Optional, so CI and containers fall through to environment variables.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Fail at startup, not on the first request that happens to hit a bad registration. With one
// DbContext per module and cross-module contracts resolved through DI, a missing or
// wrongly-scoped registration is the most likely wiring mistake — this surfaces all of them at
// boot. Development only: ValidateOnBuild constructs every registration, which costs startup time.
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = context.HostingEnvironment.IsDevelopment();
    options.ValidateOnBuild = context.HostingEnvironment.IsDevelopment();
});

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
builder.Services.AddMemoryCache();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        // Java JacksonConfig: accept a JSON number where a string is declared
        // (the frontend sends e.g. "company_type": 1 for a String field).
        options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
        options.JsonSerializerOptions.Converters.Add(new AidlyErp.Shared.Core.Utils.SafeLongJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new AidlyErp.Shared.Core.Utils.SafeIntJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new AidlyErp.Shared.Core.Utils.SafeShortJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new AidlyErp.Shared.Core.Utils.SafeDecimalJsonConverter());
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
builder.Services.AddScoped<ISysLogService, SysLogService>();
builder.Services.AddScoped<AidlyErp.Api.Bootstrap.IAppBootstrapService, AidlyErp.Api.Bootstrap.AppBootstrapService>();

// Bounded queue for fire-and-forget work (audit-log writes). See BackgroundWorkQueue for why
// this is capped rather than unbounded.
builder.Services.AddSingleton<IBackgroundWorkQueue, BackgroundWorkQueue>();
builder.Services.AddHostedService<BackgroundWorkQueueHostedService>();
builder.Services.AddScoped<ICommonLookupService, CommonLookupService>();
builder.Services.AddScoped<AidlyErp.Shared.Core.Abstractions.IFileStorage, AidlyErp.Shared.Infrastructure.Storage.FileStorageUtil>();

// Auth support services (ports of the Java core.auth.service package)
builder.Services.AddSingleton<LoginAttemptLimiter>();          // sliding window must outlive the request
builder.Services.AddSingleton<IAuthConfigService, AuthConfigService>();
builder.Services.AddScoped<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddScoped<IAuthRuntimeValidationService, AuthRuntimeValidationService>();
builder.Services.AddScoped<ISysSessionService, SysSessionService>();
builder.Services.AddScoped<IRbacAuthorizationService, RbacAuthorizationService>();
builder.Services.AddScoped<ICurrentUserScopeResolver, CurrentUserScopeResolver>();
builder.Services.AddScoped<INotificationActionHandler, AidlyErp.Hrm.Application.Services.HrmNotificationActionHandler>();

// SYS Module Services
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1001Service, AidlyErp.Sys.Application.Services.Sys1001Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1002Service, AidlyErp.Sys.Application.Services.Sys1002Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1003Service, AidlyErp.Sys.Application.Services.Sys1003Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1004Service, AidlyErp.Sys.Application.Services.Sys1004Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1005Service, AidlyErp.Sys.Application.Services.Sys1005Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1006Service, AidlyErp.Sys.Application.Services.Sys1006Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1007Service, AidlyErp.Sys.Application.Services.Sys1007Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1008Service, AidlyErp.Sys.Application.Services.Sys1008Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1101Service, AidlyErp.Sys.Application.Services.Sys1101Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1103Service, AidlyErp.Sys.Application.Services.Sys1103Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1104Service, AidlyErp.Sys.Application.Services.Sys1104Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1105Service, AidlyErp.Sys.Application.Services.Sys1105Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1107Service, AidlyErp.Sys.Application.Services.Sys1107Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1108Service, AidlyErp.Sys.Application.Services.Sys1108Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1109Service, AidlyErp.Sys.Application.Services.Sys1109Service>();
builder.Services.AddSingleton<AidlyErp.Sys.Application.Services.IPathFormCacheInvalidator, AidlyErp.Api.Controllers.Sys.PathFormCacheInvalidator>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1201Service, AidlyErp.Sys.Application.Services.Sys1201Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1202Service, AidlyErp.Sys.Application.Services.Sys1202Service>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISys1203Service, AidlyErp.Sys.Application.Services.Sys1203Service>();

// SYS shared services (the generic /api/v1/sys/* endpoints)
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISysModuleService, AidlyErp.Sys.Application.Services.SysModuleService>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ISysSubmoduleService, AidlyErp.Sys.Application.Services.SysSubmoduleService>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.IMenuService, AidlyErp.Sys.Application.Services.MenuService>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.ICompanyService, AidlyErp.Sys.Application.Services.CompanyService>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.IBranchService, AidlyErp.Sys.Application.Services.BranchService>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.IRoleService, AidlyErp.Sys.Application.Services.RoleService>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.IUserService, AidlyErp.Sys.Application.Services.UserService>();
builder.Services.AddSignalR();
// Identify connections by userNo so Clients.User(userNo) reaches the right person — the default
// provider resolves the user NAME from `sub`, which never matched what the publisher sends.
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, AidlyErp.Api.Hubs.UserNoUserIdProvider>();
builder.Services.AddScoped<INotificationRealtimePublisher, SignalRNotificationRealtimePublisher>();
// Scoped: one pending-push buffer per request, drained after the transaction commits.
builder.Services.AddScoped<INotificationRealtimeQueue, AidlyErp.Api.Services.NotificationRealtimeQueue>();
builder.Services.AddScoped<AidlyErp.Sys.Application.Services.SysNotificationService>();
builder.Services.AddScoped<INotificationDispatcher>(sp => sp.GetRequiredService<AidlyErp.Sys.Application.Services.SysNotificationService>());

// FIN Module Services
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFin1001Service, AidlyErp.Fin.Application.Services.Fin1001Service>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFin1002Service, AidlyErp.Fin.Application.Services.Fin1002Service>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFin1003Service, AidlyErp.Fin.Application.Services.Fin1003Service>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFin1004Service, AidlyErp.Fin.Application.Services.Fin1004Service>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFin1005Service, AidlyErp.Fin.Application.Services.Fin1005Service>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFin1006Service, AidlyErp.Fin.Application.Services.Fin1006Service>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFin1101Service, AidlyErp.Fin.Application.Services.Fin1101Service>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFin1102Service, AidlyErp.Fin.Application.Services.Fin1102Service>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFin1201Service, AidlyErp.Fin.Application.Services.Fin1201Service>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFinPostingService, AidlyErp.Fin.Application.Services.FinPostingService>();
builder.Services.AddHostedService<AidlyErp.Api.Services.FinPostingBackgroundService>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFinReportService, AidlyErp.Fin.Application.Services.FinReportService>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFin1308ChartOfAccountsPdfService, AidlyErp.Fin.Application.Services.Fin1308ChartOfAccountsPdfService>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFin1401Service, AidlyErp.Fin.Application.Services.Fin1401Service>();
builder.Services.AddScoped<AidlyErp.Fin.Application.Services.IFinApprovalListener, AidlyErp.Fin.Application.Services.FinApprovalListener>();
builder.Services.AddScoped<AidlyErp.Sys.Contracts.IApprovalService, AidlyErp.Sys.Application.Services.ApprovalService>();

// HRM Module Services
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1206Service, AidlyErp.Hrm.Application.Services.Hrm1206Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1003Service, AidlyErp.Hrm.Application.Services.Hrm1003Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1004Service, AidlyErp.Hrm.Application.Services.Hrm1004Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1005Service, AidlyErp.Hrm.Application.Services.Hrm1005Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1006Service, AidlyErp.Hrm.Application.Services.Hrm1006Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1007Service, AidlyErp.Hrm.Application.Services.Hrm1007Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1008Service, AidlyErp.Hrm.Application.Services.Hrm1008Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1009Service, AidlyErp.Hrm.Application.Services.Hrm1009Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrmDepartmentService, AidlyErp.Hrm.Application.Services.HrmDepartmentService>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrmDesignationService, AidlyErp.Hrm.Application.Services.HrmDesignationService>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrmEmployeeService, AidlyErp.Hrm.Application.Services.HrmEmployeeService>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1102Service, AidlyErp.Hrm.Application.Services.Hrm1102Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1202Service, AidlyErp.Hrm.Application.Services.Hrm1202Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1203Service, AidlyErp.Hrm.Application.Services.Hrm1203Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1204Service, AidlyErp.Hrm.Application.Services.Hrm1204Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1205Service, AidlyErp.Hrm.Application.Services.Hrm1205Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1207Service, AidlyErp.Hrm.Application.Services.Hrm1207Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrmLeavePolicyService, AidlyErp.Hrm.Application.Services.HrmLeavePolicyService>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.HrmApprovalListener>();

// HRM — operational services (were missing from DI — see MIGRATION-AUDIT.md)
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1101Service, AidlyErp.Hrm.Application.Services.Hrm1101Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1103Service, AidlyErp.Hrm.Application.Services.Hrm1103Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1104Service, AidlyErp.Hrm.Application.Services.Hrm1104Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1105Service, AidlyErp.Hrm.Application.Services.Hrm1105Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1106Service, AidlyErp.Hrm.Application.Services.Hrm1106Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1201Service, AidlyErp.Hrm.Application.Services.Hrm1201Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1301Service, AidlyErp.Hrm.Application.Services.Hrm1301Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1303Service, AidlyErp.Hrm.Application.Services.Hrm1303Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1401Service, AidlyErp.Hrm.Application.Services.Hrm1401Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1402Service, AidlyErp.Hrm.Application.Services.Hrm1402Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IHrm1404Service, AidlyErp.Hrm.Application.Services.Hrm1404Service>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.ILeaveRuleEngine, AidlyErp.Hrm.Application.Services.LeaveRuleEngine>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Services.IPayrollCalculationService, AidlyErp.Hrm.Application.Services.PayrollCalculationService>();

// HRM — repositories (36 repositories — port of Spring Data JPA repositories)
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmDepartmentRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmDepartmentRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmDesignationRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmDesignationRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmEmployeeRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmEmployeeRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmShiftRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmShiftRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmGradeRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmGradeRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmGradeStepRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmGradeStepRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmLeaveTypeRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmLeaveTypeRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmHolidayRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmHolidayRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmSalaryComponentRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmSalaryComponentRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmPayrollPolicyRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmPayrollPolicyRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmTaxSlabRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmTaxSlabRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmAttendanceRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmAttendanceRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmAttendanceAdjustmentRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmAttendanceAdjustmentRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmShiftRosterRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmShiftRosterRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmShiftRosterLineRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmShiftRosterLineRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmOvertimeRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmOvertimeRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmEmployeeMovementRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmEmployeeMovementRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmSalaryStructureRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmSalaryStructureRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmSalaryStructureDtlRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmSalaryStructureDtlRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmPayrollRunRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmPayrollRunRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmPayslipRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmPayslipRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmPayslipDtlRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmPayslipDtlRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmBonusRunRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmBonusRunRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmBonusLineRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmBonusLineRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmBonusScopeDesignationRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmBonusScopeDesignationRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmBonusScopeEmployeeRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmBonusScopeEmployeeRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmLoanAdvanceRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmLoanAdvanceRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmFinalSettlementRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmFinalSettlementRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmLeaveApplicationRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmLeaveApplicationRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmLeaveBalanceRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmLeaveBalanceRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmLeaveLedgerRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmLeaveLedgerRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmLeavePolicySetupRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmLeavePolicySetupRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmLeaveApplicationRuleRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmLeaveApplicationRuleRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmJobRequisitionRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmJobRequisitionRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmCandidateRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmCandidateRepository>();
builder.Services.AddScoped<AidlyErp.Hrm.Application.Interfaces.IHrmOfferRepository, AidlyErp.Shared.Infrastructure.Persistence.Repositories.HrmOfferRepository>();

// SAL Module Services
builder.Services.AddScoped<AidlyErp.Sal.Application.Services.ISalArLedgerService, AidlyErp.Sal.Application.Services.SalArLedgerService>();
builder.Services.AddScoped<AidlyErp.Sal.Application.Services.ISal1001Service, AidlyErp.Sal.Application.Services.Sal1001Service>();
builder.Services.AddScoped<AidlyErp.Sal.Application.Services.ISal1101Service, AidlyErp.Sal.Application.Services.Sal1101Service>();
builder.Services.AddScoped<AidlyErp.Sal.Application.Services.ISal1102Service, AidlyErp.Sal.Application.Services.Sal1102Service>();
builder.Services.AddScoped<AidlyErp.Sal.Application.Services.ISal1103Service, AidlyErp.Sal.Application.Services.Sal1103Service>();
builder.Services.AddScoped<AidlyErp.Sal.Application.Services.ISalApprovalListener, AidlyErp.Sal.Application.Services.SalApprovalListener>();

// INV Module Services
builder.Services.AddScoped<AidlyErp.Inv.Contracts.IInvStockPostingService, AidlyErp.Inv.Application.Engine.InvStockPostingService>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInvDocSequenceService, AidlyErp.Inv.Application.Services.InvDocSequenceService>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInvPeriodResolver, AidlyErp.Inv.Application.Services.InvPeriodResolver>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv1001Service, AidlyErp.Inv.Application.Services.Inv1001Service>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv1003Service, AidlyErp.Inv.Application.Services.Inv1003Service>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv1004Service, AidlyErp.Inv.Application.Services.Inv1004Service>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv1005Service, AidlyErp.Inv.Application.Services.Inv1005Service>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv1101Service, AidlyErp.Inv.Application.Services.Inv1101Service>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv1102Service, AidlyErp.Inv.Application.Services.Inv1102Service>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv2001Service, AidlyErp.Inv.Application.Services.Inv2001Service>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv2003Service, AidlyErp.Inv.Application.Services.Inv2003Service>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv1006Service, AidlyErp.Inv.Application.Services.Inv1006Service>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv2002Service, AidlyErp.Inv.Application.Services.Inv2002Service>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv2004Service, AidlyErp.Inv.Application.Services.Inv2004Service>();
builder.Services.AddScoped<AidlyErp.Inv.Application.Services.IInv2005Service, AidlyErp.Inv.Application.Services.Inv2005Service>();

// PUR Module Services
builder.Services.AddScoped<AidlyErp.Pur.Application.Services.IPurApLedgerService, AidlyErp.Pur.Application.Services.PurApLedgerService>();
builder.Services.AddScoped<AidlyErp.Pur.Application.Services.IPur1001Service, AidlyErp.Pur.Application.Services.Pur1001Service>();
builder.Services.AddScoped<AidlyErp.Pur.Application.Services.IPur1102Service, AidlyErp.Pur.Application.Services.Pur1102Service>();
builder.Services.AddScoped<AidlyErp.Pur.Application.Services.IPur1104Service, AidlyErp.Pur.Application.Services.Pur1104Service>();
builder.Services.AddScoped<AidlyErp.Pur.Application.Services.IPur1002Service, AidlyErp.Pur.Application.Services.Pur1002Service>();
builder.Services.AddScoped<AidlyErp.Pur.Application.Services.IPur1101Service, AidlyErp.Pur.Application.Services.Pur1101Service>();
builder.Services.AddScoped<AidlyErp.Pur.Application.Services.IPur1103Service, AidlyErp.Pur.Application.Services.Pur1103Service>();
builder.Services.AddScoped<AidlyErp.Pur.Application.Services.IPur1105Service, AidlyErp.Pur.Application.Services.Pur1105Service>();
builder.Services.AddScoped<AidlyErp.Pur.Application.Services.IPur1106Service, AidlyErp.Pur.Application.Services.Pur1106Service>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=aidly_sme;Username=postgres;Password=postgres";

var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD")
              ?? Environment.GetEnvironmentVariable("Aidly__Db__Password");
if (!string.IsNullOrEmpty(dbPassword) && connectionString.Contains("__SET_VIA_ENV__"))
{
    connectionString = connectionString.Replace("__SET_VIA_ENV__", dbPassword);
}

// ---------------------------------------------------------------------------
// Module persistence. Every module owns its own DbContext over the same physical
// database, so a module can only reach the tables it declares. They share the
// connection string and the auditing/tenant interceptor.
// ---------------------------------------------------------------------------
builder.Services
    .AddSysModule(connectionString)
    .AddHrmModule(connectionString)
    .AddFinModule(connectionString)
    .AddInvModule(connectionString)
    .AddPurModule(connectionString)
    .AddSalModule(connectionString);

// ---------------------------------------------------------------------------
// Authentication — JWT bearer.
//
// Previously UseAuthentication() ran with no scheme registered, so every request was
// anonymous: the tenant middleware never populated company/branch and the API was
// effectively unauthenticated. This registers the same HMAC-SHA256 validation the Java
// JwtAuthenticationFilter performs.
// ---------------------------------------------------------------------------
var jwtSecret = builder.Configuration["Aidly:Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
}
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    jwtSecret = Environment.GetEnvironmentVariable("Aidly__Jwt__Secret");
}
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    jwtSecret = "xww3jckXeNaHYDG9n5gUJllRuQV3HrAbMEGEtLSGoZF";
}

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

        // A browser cannot set an Authorization header on a WebSocket handshake, so the SignalR
        // JS client sends the token as ?access_token=... instead. Without this the [Authorize]
        // notification hub rejects every browser connection. Restricted to the hub path so a
        // token can never be accepted from the query string on a normal API call, where it would
        // leak into access logs, proxies and browser history.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
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

// Serve OpenAPI specification endpoints for all module groups
app.MapOpenApi("/v3/api-docs/{documentName}.json");

// Serve interactive Swagger UI at /swagger-ui.html matching Java springdoc
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger-ui.html";
    options.DocumentTitle = "Aidly ERP — API Documentation";
    foreach (var (groupName, groupTitle) in SwaggerGroups)
    {
        options.SwaggerEndpoint($"/v3/api-docs/{groupName}.json", groupTitle);
    }
});

// Redirect /swagger to /swagger-ui.html
app.MapGet("/swagger", () => Results.Redirect("/swagger-ui.html"));

if (!app.Environment.IsDevelopment())
{
    // includeSubDomains, max-age 31536000 — matches the Java HSTS configuration
    app.UseHsts();
}

app.UseHttpsRedirection();

// Java WebConfig.addResourceHandlers: serve the uploads directory at /uploads/**
var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
try
{
    if (!Directory.Exists(uploadPath))
    {
        Directory.CreateDirectory(uploadPath);
    }
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadPath),
        RequestPath = "/uploads"
    });
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not initialize static file provider for uploads path '{UploadPath}'", uploadPath);
}

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

// Sits outside the endpoint so it runs after the action — and therefore after the unit of work
// has committed — before pushing anything to connected clients.
app.UseMiddleware<NotificationRealtimeFlushMiddleware>();

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
app.MapHub<AidlyErp.Api.Hubs.NotificationHub>("/hubs/notification");

app.Run();
