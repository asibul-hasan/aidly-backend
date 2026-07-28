using AidlyErp.Hrm.Domain;

namespace AidlyErp.Hrm.Application.Interfaces;

// ═══════════════════════════════════════════════════════════════════════════
// HRM Repository Interfaces — port of Spring Data JPA repositories
// Every method here has a 1:1 counterpart in the Java hrm/repository/ package.
// ═══════════════════════════════════════════════════════════════════════════

// ── Setup (1000-series) ──────────────────────────────────────────────────

public interface IHrmDepartmentRepository
{
    Task<List<HrmDepartment>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmDepartment>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmDepartment?> FindByIdAsync(long departmentNo, CancellationToken ct = default);
    Task<List<HrmDepartment>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<List<HrmDepartment>> FindByBranchPaginatedAsync(long branchNo, int skip, int take, CancellationToken ct = default);
    Task<HrmDepartment?> FindByCodeAsync(string departmentId, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string departmentId, CancellationToken ct = default);
    void Add(HrmDepartment entity);
    void Update(HrmDepartment entity);
}

public interface IHrmDesignationRepository
{
    Task<List<HrmDesignation>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmDesignation>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmDesignation?> FindByIdAsync(long designationNo, CancellationToken ct = default);
    Task<List<HrmDesignation>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<List<HrmDesignation>> FindByBranchPaginatedAsync(long branchNo, int skip, int take, CancellationToken ct = default);
    Task<List<HrmDesignation>> FindByDepartmentAsync(long departmentNo, CancellationToken ct = default);
    Task<HrmDesignation?> FindByCodeAsync(string designationId, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string designationId, CancellationToken ct = default);
    void Add(HrmDesignation entity);
    void Update(HrmDesignation entity);
}

public interface IHrmEmployeeRepository
{
    Task<List<HrmEmployee>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmEmployee>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmEmployee?> FindByIdAsync(long employeeNo, CancellationToken ct = default);
    Task<List<HrmEmployee>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<List<HrmEmployee>> FindByBranchPaginatedAsync(long branchNo, int skip, int take, CancellationToken ct = default);
    Task<List<HrmEmployee>> FindByDepartmentAsync(long departmentNo, CancellationToken ct = default);
    Task<List<HrmEmployee>> FindByDepartmentPaginatedAsync(long departmentNo, int skip, int take, CancellationToken ct = default);
    Task<HrmEmployee?> FindByCodeAsync(string employeeId, long branchNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string employeeId, long branchNo, CancellationToken ct = default);
    Task<List<HrmEmployee>> FindWithFiltersAsync(short? isActive, short? employmentType, long? departmentNo, CancellationToken ct = default);
    void Add(HrmEmployee entity);
    void Update(HrmEmployee entity);
}

public interface IHrmShiftRepository
{
    Task<List<HrmShift>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmShift>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmShift?> FindByIdAsync(long shiftNo, CancellationToken ct = default);
    Task<List<HrmShift>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<HrmShift?> FindByCodeAsync(string shiftId, long branchNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string shiftId, long branchNo, CancellationToken ct = default);
    void Add(HrmShift entity);
    void Update(HrmShift entity);
}

public interface IHrmGradeRepository
{
    Task<List<HrmGrade>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmGrade>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmGrade?> FindByIdAsync(long gradeNo, CancellationToken ct = default);
    Task<List<HrmGrade>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<HrmGrade?> FindByCodeAsync(string gradeId, long branchNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string gradeId, long branchNo, CancellationToken ct = default);
    void Add(HrmGrade entity);
    void Update(HrmGrade entity);
}

public interface IHrmGradeStepRepository
{
    Task<List<HrmGradeStep>> FindByGradeAsync(long gradeNo, CancellationToken ct = default);
    void Add(HrmGradeStep entity);
    void AddRange(IEnumerable<HrmGradeStep> entities);
    void RemoveRange(IEnumerable<HrmGradeStep> entities);
}

public interface IHrmLeaveTypeRepository
{
    Task<List<HrmLeaveType>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmLeaveType>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmLeaveType?> FindByIdAsync(long leaveTypeNo, CancellationToken ct = default);
    Task<List<HrmLeaveType>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<HrmLeaveType?> FindByCodeAsync(string leaveTypeId, long branchNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string leaveTypeId, long branchNo, CancellationToken ct = default);
    void Add(HrmLeaveType entity);
    void Update(HrmLeaveType entity);
}

public interface IHrmHolidayRepository
{
    Task<List<HrmHoliday>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmHoliday>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmHoliday?> FindByIdAsync(long holidayNo, CancellationToken ct = default);
    Task<List<HrmHoliday>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<HrmHoliday?> FindByDateAndBranchAsync(DateOnly holidayDate, long branchNo, CancellationToken ct = default);
    Task<bool> ExistsByDateAndBranchAsync(DateOnly holidayDate, long branchNo, CancellationToken ct = default);
    void Add(HrmHoliday entity);
    void Update(HrmHoliday entity);
}

public interface IHrmSalaryComponentRepository
{
    Task<List<HrmSalaryComponent>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmSalaryComponent>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmSalaryComponent?> FindByIdAsync(long componentNo, CancellationToken ct = default);
    Task<List<HrmSalaryComponent>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<HrmSalaryComponent?> FindByCodeAsync(string componentId, long branchNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string componentId, long branchNo, CancellationToken ct = default);
    Task<List<HrmSalaryComponent>> FindPreferredByComponentIdAsync(string componentId, long branchNo, CancellationToken ct = default);
    void Add(HrmSalaryComponent entity);
    void Update(HrmSalaryComponent entity);
}

public interface IHrmPayrollPolicyRepository
{
    Task<List<HrmPayrollPolicy>> FindAllAsync(CancellationToken ct = default);
    Task<HrmPayrollPolicy?> FindByIdAsync(long policyNo, CancellationToken ct = default);
    Task<List<HrmPayrollPolicy>> FindActivePoliciesAsync(string countryCode, DateOnly asOf, CancellationToken ct = default);
    void Add(HrmPayrollPolicy entity);
    void Update(HrmPayrollPolicy entity);
}

public interface IHrmTaxSlabRepository
{
    Task<List<HrmTaxSlab>> FindAllProfilesOrderedAsync(CancellationToken ct = default);
    Task<HrmTaxSlab?> FindByIdAsync(long taxSlabNo, CancellationToken ct = default);
    Task<List<HrmTaxSlab>> FindByProfileAsync(string countryCode, string fiscalYear, string taxpayerClass, CancellationToken ct = default);
    Task<bool> ExistsByProfileAsync(string countryCode, string fiscalYear, string taxpayerClass, CancellationToken ct = default);
    void Add(HrmTaxSlab entity);
    void Update(HrmTaxSlab entity);
    void RemoveRange(IEnumerable<HrmTaxSlab> entities);
}

// ── Attendance (1100-series) ─────────────────────────────────────────────

public interface IHrmAttendanceRepository
{
    Task<List<HrmAttendance>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmAttendance>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmAttendance?> FindByIdAsync(long attendanceNo, CancellationToken ct = default);
    Task<List<HrmAttendance>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<List<HrmAttendance>> FindByEmployeeAsync(long employeeNo, CancellationToken ct = default);
    Task<HrmAttendance?> FindByEmployeeAndDateAsync(long employeeNo, DateTime attDate, CancellationToken ct = default);
    Task<bool> ExistsByEmployeeAndDateAsync(long employeeNo, DateTime attDate, CancellationToken ct = default);
    void Add(HrmAttendance entity);
    void Update(HrmAttendance entity);
}

public interface IHrmAttendanceAdjustmentRepository
{
    Task<List<HrmAttendanceAdjustment>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmAttendanceAdjustment>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmAttendanceAdjustment?> FindByIdAsync(long adjustmentNo, CancellationToken ct = default);
    Task<List<HrmAttendanceAdjustment>> FindByEmployeeAsync(long employeeNo, CancellationToken ct = default);
    void Add(HrmAttendanceAdjustment entity);
    void Update(HrmAttendanceAdjustment entity);
}

public interface IHrmShiftRosterRepository
{
    Task<List<HrmShiftRoster>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmShiftRoster>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmShiftRoster?> FindByIdAsync(long rosterNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string rosterId, long branchNo, CancellationToken ct = default);
    void Add(HrmShiftRoster entity);
    void Update(HrmShiftRoster entity);
}

public interface IHrmShiftRosterLineRepository
{
    Task<List<HrmShiftRosterLine>> FindByRosterAsync(long rosterNo, CancellationToken ct = default);
    void Add(HrmShiftRosterLine entity);
    void AddRange(IEnumerable<HrmShiftRosterLine> entities);
    void RemoveRange(IEnumerable<HrmShiftRosterLine> entities);
}

public interface IHrmOvertimeRepository
{
    Task<List<HrmOvertime>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmOvertime>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmOvertime?> FindByIdAsync(long overtimeNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string overtimeId, long branchNo, CancellationToken ct = default);
    void Add(HrmOvertime entity);
    void Update(HrmOvertime entity);
}

public interface IHrmEmployeeMovementRepository
{
    Task<List<HrmEmployeeMovement>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmEmployeeMovement>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmEmployeeMovement?> FindByIdAsync(long movementNo, CancellationToken ct = default);
    Task<List<HrmEmployeeMovement>> FindByEmployeeAsync(long employeeNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string movementId, long branchNo, CancellationToken ct = default);
    void Add(HrmEmployeeMovement entity);
    void Update(HrmEmployeeMovement entity);
}

// ── Payroll (1200-series) ────────────────────────────────────────────────

public interface IHrmSalaryStructureRepository
{
    Task<List<HrmSalaryStructure>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmSalaryStructure>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmSalaryStructure?> FindByIdAsync(long salaryStructureNo, CancellationToken ct = default);
    Task<List<HrmSalaryStructure>> FindByEmployeeAsync(long employeeNo, CancellationToken ct = default);
    Task<HrmSalaryStructure?> FindByEmployeeAndStatusAsync(long employeeNo, short status, CancellationToken ct = default);
    void Add(HrmSalaryStructure entity);
    void Update(HrmSalaryStructure entity);
}

public interface IHrmSalaryStructureDtlRepository
{
    Task<List<HrmSalaryStructureDtl>> FindByStructureAsync(long salaryStructureNo, CancellationToken ct = default);
    void Add(HrmSalaryStructureDtl entity);
    void AddRange(IEnumerable<HrmSalaryStructureDtl> entities);
    void RemoveRange(IEnumerable<HrmSalaryStructureDtl> entities);
}

public interface IHrmPayrollRunRepository
{
    Task<List<HrmPayrollRun>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmPayrollRun>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmPayrollRun?> FindByIdAsync(long payrollRunNo, CancellationToken ct = default);
    Task<bool> ExistsDuplicateAsync(long branchNo, string payPeriod, short runType, CancellationToken ct = default);
    Task<bool> ExistsDuplicateWithBonusAsync(long branchNo, string payPeriod, short runType, long bonusRunNo, CancellationToken ct = default);
    Task<HrmPayrollRun?> FindByBonusRunAsync(long bonusRunNo, CancellationToken ct = default);
    void Add(HrmPayrollRun entity);
    void Update(HrmPayrollRun entity);
}

public interface IHrmPayslipRepository
{
    Task<List<HrmPayslip>> FindByRunAsync(long payrollRunNo, CancellationToken ct = default);
    Task<HrmPayslip?> FindByIdAsync(long payslipNo, CancellationToken ct = default);
    Task<List<HrmSettlementPayrollDueView>> FindSettlementDueRowsAsync(long employeeNo, long branchNo, DateTime lastWorkingDay, CancellationToken ct = default);
    void Add(HrmPayslip entity);
    void AddRange(IEnumerable<HrmPayslip> entities);
    void Update(HrmPayslip entity);
}

public interface IHrmPayslipDtlRepository
{
    Task<List<HrmPayslipDtl>> FindByPayslipAsync(long payslipNo, CancellationToken ct = default);
    void Add(HrmPayslipDtl entity);
    void AddRange(IEnumerable<HrmPayslipDtl> entities);
    void RemoveRange(IEnumerable<HrmPayslipDtl> entities);
}

public interface IHrmBonusRunRepository
{
    Task<List<HrmBonusRun>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmBonusRun>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmBonusRun?> FindByIdAsync(long bonusRunNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string bonusId, long branchNo, CancellationToken ct = default);
    void Add(HrmBonusRun entity);
    void Update(HrmBonusRun entity);
}

public interface IHrmBonusLineRepository
{
    Task<List<HrmBonusLine>> FindByRunAsync(long bonusRunNo, CancellationToken ct = default);
    Task<HrmBonusLine?> FindByRunAndEmployeeAsync(long bonusRunNo, long employeeNo, CancellationToken ct = default);
    void Add(HrmBonusLine entity);
    void AddRange(IEnumerable<HrmBonusLine> entities);
    void RemoveRange(IEnumerable<HrmBonusLine> entities);
}

public interface IHrmBonusScopeDesignationRepository
{
    Task<List<HrmBonusScopeDesignation>> FindByRunAsync(long bonusRunNo, CancellationToken ct = default);
    void Add(HrmBonusScopeDesignation entity);
    void AddRange(IEnumerable<HrmBonusScopeDesignation> entities);
    void RemoveRange(IEnumerable<HrmBonusScopeDesignation> entities);
}

public interface IHrmBonusScopeEmployeeRepository
{
    Task<List<HrmBonusScopeEmployee>> FindByRunAsync(long bonusRunNo, CancellationToken ct = default);
    void Add(HrmBonusScopeEmployee entity);
    void AddRange(IEnumerable<HrmBonusScopeEmployee> entities);
    void RemoveRange(IEnumerable<HrmBonusScopeEmployee> entities);
}

public interface IHrmLoanAdvanceRepository
{
    Task<List<HrmLoanAdvance>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmLoanAdvance>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmLoanAdvance?> FindByIdAsync(long loanNo, CancellationToken ct = default);
    Task<HrmLoanAdvance?> FindByCodeAsync(string loanId, long branchNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string loanId, long branchNo, CancellationToken ct = default);
    void Add(HrmLoanAdvance entity);
    void Update(HrmLoanAdvance entity);
}

public interface IHrmFinalSettlementRepository
{
    Task<List<HrmFinalSettlement>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmFinalSettlement>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmFinalSettlement?> FindByIdAsync(long settlementNo, CancellationToken ct = default);
    Task<HrmFinalSettlement?> FindByCodeAsync(string settlementId, long branchNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string settlementId, long branchNo, CancellationToken ct = default);
    void Add(HrmFinalSettlement entity);
    void Update(HrmFinalSettlement entity);
}

// ── Leave (1300-series) ──────────────────────────────────────────────────

public interface IHrmLeaveApplicationRepository
{
    Task<List<HrmLeaveApplication>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmLeaveApplication>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmLeaveApplication?> FindByIdAsync(long leaveApplicationNo, CancellationToken ct = default);
    Task<List<HrmLeaveApplication>> FindByEmployeeAsync(long employeeNo, CancellationToken ct = default);
    Task<List<HrmLeaveApplication>> FindFilteredHistoryAsync(long employeeNo, long? leaveTypeNo, DateTime? fromDate, DateTime? toDate, CancellationToken ct = default);
    Task<List<HrmLeaveApplication>> FindOverlappingAsync(long employeeNo, DateTime fromDate, DateTime toDate, List<short> blockingStatuses, long? excludeNo, CancellationToken ct = default);
    Task<long> CountEmployeesOnLeaveInDepartmentAsync(long departmentNo, DateTime fromDate, DateTime toDate, List<short> blockingStatuses, long? excludeNo, CancellationToken ct = default);
    Task<long> CountActiveEmployeesInDepartmentAsync(long departmentNo, CancellationToken ct = default);
    Task<List<HrmLeaveApplication>> FindPendingForApprovalAsync(short status, long? branchNo, CancellationToken ct = default);
    void Add(HrmLeaveApplication entity);
    void Update(HrmLeaveApplication entity);
}

public interface IHrmLeaveBalanceRepository
{
    Task<HrmLeaveBalance?> FindByEmployeeTypeYearAsync(long employeeNo, long leaveTypeNo, int leaveYear, CancellationToken ct = default);
    Task<List<HrmLeaveBalance>> FindByEmployeeYearAsync(long employeeNo, int leaveYear, CancellationToken ct = default);
    Task<List<HrmLeaveBalance>> FindByYearAsync(int leaveYear, CancellationToken ct = default);
    Task<List<HrmLeaveBalance>> FindByYearAndBranchAsync(int leaveYear, long branchNo, CancellationToken ct = default);
    void Add(HrmLeaveBalance entity);
    void Update(HrmLeaveBalance entity);
}

public interface IHrmLeaveLedgerRepository
{
    Task<List<HrmLeaveLedger>> FindByEmployeeTypeYearAsync(long employeeNo, long leaveTypeNo, int leaveYear, CancellationToken ct = default);
    void Add(HrmLeaveLedger entity);
}

public interface IHrmLeavePolicySetupRepository
{
    Task<List<HrmLeavePolicySetup>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmLeavePolicySetup>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<HrmLeavePolicySetup?> FindByTypeAndGroupAsync(long leaveTypeNo, long employeeGroupNo, CancellationToken ct = default);
    void Add(HrmLeavePolicySetup entity);
    void Update(HrmLeavePolicySetup entity);
}

public interface IHrmLeaveApplicationRuleRepository
{
    Task<HrmLeaveApplicationRule?> FindByPolicyAsync(long policyNo, CancellationToken ct = default);
    void Add(HrmLeaveApplicationRule entity);
    void Update(HrmLeaveApplicationRule entity);
}

// ── Recruitment (1400-series) ────────────────────────────────────────────

public interface IHrmJobRequisitionRepository
{
    Task<List<HrmJobRequisition>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmJobRequisition>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmJobRequisition?> FindByIdAsync(long requisitionNo, CancellationToken ct = default);
    Task<List<HrmJobRequisition>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string requisitionId, long branchNo, CancellationToken ct = default);
    void Add(HrmJobRequisition entity);
    void Update(HrmJobRequisition entity);
}

public interface IHrmCandidateRepository
{
    Task<List<HrmCandidate>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmCandidate>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmCandidate?> FindByIdAsync(long candidateNo, CancellationToken ct = default);
    Task<List<HrmCandidate>> FindByBranchAsync(long branchNo, CancellationToken ct = default);
    Task<List<HrmCandidate>> FindByStatusInAsync(List<short> statuses, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string candidateId, long branchNo, CancellationToken ct = default);
    void Add(HrmCandidate entity);
    void Update(HrmCandidate entity);
}

public interface IHrmOfferRepository
{
    Task<List<HrmOffer>> FindAllAsync(CancellationToken ct = default);
    Task<List<HrmOffer>> FindAllPaginatedAsync(int skip, int take, CancellationToken ct = default);
    Task<HrmOffer?> FindByIdAsync(long offerNo, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string offerId, long branchNo, CancellationToken ct = default);
    void Add(HrmOffer entity);
    void Update(HrmOffer entity);
}

// ── Projection interface (port of Spring Data interface projection) ─────

public interface IHrmSettlementPayrollDueView
{
    long PayslipNo { get; }
    long PayrollRunNo { get; }
    string PayPeriod { get; }
    short RunType { get; }
    short RunStatus { get; }
    DateOnly PeriodEnd { get; }
    decimal NetPay { get; }
    short PaymentStatus { get; }
}

public class HrmSettlementPayrollDueView : IHrmSettlementPayrollDueView
{
    public long PayslipNo { get; set; }
    public long PayrollRunNo { get; set; }
    public string PayPeriod { get; set; } = "";
    public short RunType { get; set; }
    public short RunStatus { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal NetPay { get; set; }
    public short PaymentStatus { get; set; }
}
