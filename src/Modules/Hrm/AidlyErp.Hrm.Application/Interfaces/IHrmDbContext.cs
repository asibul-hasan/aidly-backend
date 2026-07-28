using AidlyErp.Hrm.Domain;
using Microsoft.EntityFrameworkCore;

namespace AidlyErp.Hrm.Application.Interfaces;

/// <summary>Persistence surface of the Hrm module. Only Hrm entities are reachable —
/// the compile-time expression of the isolated-schema rule.</summary>
public interface IHrmDbContext
{
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

    DbSet<AidlyErp.Shared.Core.EventOutbox> EventOutboxes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Raw-SQL surface: several queries are ported verbatim from Java
    /// <c>@Query(nativeQuery = true)</c> definitions whose GROUP BY / NULLS LAST
    /// semantics would risk silent drift if re-expressed in LINQ.</summary>
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
}
