using AidlyErp.Hrm.Application.Interfaces;
using AidlyErp.Hrm.Domain;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Hrm.Infrastructure;

public class HrmDbContext : ModuleDbContext, IHrmDbContext
{
    public HrmDbContext(DbContextOptions<HrmDbContext> options, ICompanyBranchContext tenantContext)
        : base(options, tenantContext) { }

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

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
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
    }
}
