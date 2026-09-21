-- ============================================================================
-- Migration: 2026-09-02_1320_hrm_unconstrained_numeric_columns.sql
-- Module: HRM (Human Resource Management & Payroll)
-- Purpose: Remove all fixed precision and fraction/scale limits (e.g. NUMERIC(20,4),
--          NUMERIC(18,4), NUMERIC(9,4), NUMERIC(9,2)) across all HRM module tables.
--          Converts all salary, bonus, tax, overtime, leave balance, loan, and
--          settlement columns to unconstrained NUMERIC so PostgreSQL saves any
--          fraction or number exactly as provided without database-level truncation,
--          leaving display formatting exclusively to the frontend.
-- ============================================================================

BEGIN;

-- ── 1. hrm_employee ────────────────────────────────────────────────────────
ALTER TABLE hrm_employee
    ALTER COLUMN salary TYPE NUMERIC;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'hrm_employee' AND column_name = 'contract_amount') THEN
        ALTER TABLE hrm_employee ALTER COLUMN contract_amount TYPE NUMERIC;
    END IF;
END $$;

-- ── 1b. hrm_department ─────────────────────────────────────────────────────
ALTER TABLE hrm_department
    ALTER COLUMN max_leave_percentage TYPE NUMERIC;

-- ── 1c. hrm_designation ────────────────────────────────────────────────────
ALTER TABLE hrm_designation
    ALTER COLUMN min_salary TYPE NUMERIC,
    ALTER COLUMN max_salary TYPE NUMERIC;

-- ── 2. hrm_grade ───────────────────────────────────────────────────────────
ALTER TABLE hrm_grade
    ALTER COLUMN min_salary TYPE NUMERIC,
    ALTER COLUMN max_salary TYPE NUMERIC;

-- ── 3. hrm_grade_step ──────────────────────────────────────────────────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'hrm_grade_step') THEN
        ALTER TABLE hrm_grade_step ALTER COLUMN amount TYPE NUMERIC;
    END IF;
END $$;

-- ── 4. hrm_salary_component ────────────────────────────────────────────────
ALTER TABLE hrm_salary_component
    ALTER COLUMN calc_value TYPE NUMERIC;

-- ── 5. hrm_salary_structure ────────────────────────────────────────────────
ALTER TABLE hrm_salary_structure
    ALTER COLUMN basic_salary TYPE NUMERIC,
    ALTER COLUMN gross_salary TYPE NUMERIC;

-- ── 6. hrm_salary_structure_dtl ────────────────────────────────────────────
ALTER TABLE hrm_salary_structure_dtl
    ALTER COLUMN calc_value TYPE NUMERIC,
    ALTER COLUMN amount TYPE NUMERIC;

-- ── 7. hrm_payroll_run ─────────────────────────────────────────────────────
ALTER TABLE hrm_payroll_run
    ALTER COLUMN total_gross TYPE NUMERIC,
    ALTER COLUMN total_deduction TYPE NUMERIC,
    ALTER COLUMN total_net TYPE NUMERIC;

-- ── 8. hrm_payslip ─────────────────────────────────────────────────────────
ALTER TABLE hrm_payslip
    ALTER COLUMN basic_salary TYPE NUMERIC,
    ALTER COLUMN present_days TYPE NUMERIC,
    ALTER COLUMN absent_days TYPE NUMERIC,
    ALTER COLUMN leave_days TYPE NUMERIC,
    ALTER COLUMN lwp_days TYPE NUMERIC,
    ALTER COLUMN payable_days TYPE NUMERIC,
    ALTER COLUMN ot_hours TYPE NUMERIC,
    ALTER COLUMN ot_amount TYPE NUMERIC,
    ALTER COLUMN gross_earning TYPE NUMERIC,
    ALTER COLUMN total_deduction TYPE NUMERIC,
    ALTER COLUMN taxable_income TYPE NUMERIC,
    ALTER COLUMN employer_contribution TYPE NUMERIC,
    ALTER COLUMN net_pay TYPE NUMERIC;

-- ── 9. hrm_payslip_dtl ─────────────────────────────────────────────────────
ALTER TABLE hrm_payslip_dtl
    ALTER COLUMN calc_value TYPE NUMERIC,
    ALTER COLUMN base_amount TYPE NUMERIC,
    ALTER COLUMN amount TYPE NUMERIC;

-- ── 10. hrm_bonus_run ──────────────────────────────────────────────────────
ALTER TABLE hrm_bonus_run
    ALTER COLUMN percentage_of_basic TYPE NUMERIC,
    ALTER COLUMN fixed_amount TYPE NUMERIC,
    ALTER COLUMN total_amount TYPE NUMERIC;

-- ── 11. hrm_bonus_line ─────────────────────────────────────────────────────
ALTER TABLE hrm_bonus_line
    ALTER COLUMN base_salary TYPE NUMERIC,
    ALTER COLUMN bonus_amount TYPE NUMERIC;

-- ── 12. hrm_loan_advance ───────────────────────────────────────────────────
ALTER TABLE hrm_loan_advance
    ALTER COLUMN principal_amount TYPE NUMERIC,
    ALTER COLUMN installment_amount TYPE NUMERIC,
    ALTER COLUMN recovered_amount TYPE NUMERIC,
    ALTER COLUMN outstanding_amount TYPE NUMERIC;

-- ── 13. hrm_tax_slab ───────────────────────────────────────────────────────
ALTER TABLE hrm_tax_slab
    ALTER COLUMN from_amount TYPE NUMERIC,
    ALTER COLUMN to_amount TYPE NUMERIC,
    ALTER COLUMN rate_percent TYPE NUMERIC,
    ALTER COLUMN fixed_amount TYPE NUMERIC;

-- ── 14. hrm_attendance ─────────────────────────────────────────────────────
ALTER TABLE hrm_attendance
    ALTER COLUMN worked_hours TYPE NUMERIC,
    ALTER COLUMN ot_hours TYPE NUMERIC;

-- ── 15. hrm_overtime ───────────────────────────────────────────────────────
ALTER TABLE hrm_overtime
    ALTER COLUMN hours TYPE NUMERIC,
    ALTER COLUMN rate_multiplier TYPE NUMERIC,
    ALTER COLUMN hourly_rate TYPE NUMERIC,
    ALTER COLUMN ot_amount TYPE NUMERIC;

-- ── 16. hrm_leave_balance ──────────────────────────────────────────────────
ALTER TABLE hrm_leave_balance
    ALTER COLUMN opening_balance TYPE NUMERIC,
    ALTER COLUMN entitled_days TYPE NUMERIC,
    ALTER COLUMN accrued_days TYPE NUMERIC,
    ALTER COLUMN consumed_days TYPE NUMERIC,
    ALTER COLUMN pending_days TYPE NUMERIC,
    ALTER COLUMN encashed_days TYPE NUMERIC,
    ALTER COLUMN lapsed_days TYPE NUMERIC,
    ALTER COLUMN carried_forward TYPE NUMERIC;

-- ── 17. hrm_leave_ledger ───────────────────────────────────────────────────
ALTER TABLE hrm_leave_ledger
    ALTER COLUMN days TYPE NUMERIC,
    ALTER COLUMN balance_after TYPE NUMERIC;

-- ── 18. hrm_leave_type ─────────────────────────────────────────────────────
ALTER TABLE hrm_leave_type
    ALTER COLUMN default_days_per_year TYPE NUMERIC,
    ALTER COLUMN max_carry_forward_days TYPE NUMERIC,
    ALTER COLUMN min_duration_days TYPE NUMERIC,
    ALTER COLUMN max_duration_days TYPE NUMERIC;

-- ── 19. hrm_leave_policy_setup ─────────────────────────────────────────────
ALTER TABLE hrm_leave_policy_setup
    ALTER COLUMN default_days TYPE NUMERIC,
    ALTER COLUMN max_carry_forward_days TYPE NUMERIC,
    ALTER COLUMN max_encashable_days TYPE NUMERIC,
    ALTER COLUMN max_dept_leave_percentage TYPE NUMERIC;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'hrm_leave_policy_setup' AND column_name = 'min_balance_for_encashment') THEN
        ALTER TABLE hrm_leave_policy_setup ALTER COLUMN min_balance_for_encashment TYPE NUMERIC;
    END IF;
END $$;

-- ── 19b. hrm_leave_encashment ──────────────────────────────────────────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'hrm_leave_encashment') THEN
        ALTER TABLE hrm_leave_encashment
            ALTER COLUMN requested_days TYPE NUMERIC,
            ALTER COLUMN approved_days TYPE NUMERIC,
            ALTER COLUMN rate_per_day TYPE NUMERIC,
            ALTER COLUMN amount TYPE NUMERIC;
    END IF;
END $$;

-- ── 20. hrm_leave_application_rule ─────────────────────────────────────────
ALTER TABLE hrm_leave_application_rule
    ALTER COLUMN min_duration_per_app TYPE NUMERIC,
    ALTER COLUMN max_duration_per_app TYPE NUMERIC,
    ALTER COLUMN max_overdraft_days TYPE NUMERIC,
    ALTER COLUMN attachment_required_after_days TYPE NUMERIC;

-- ── 21. hrm_leave_application ──────────────────────────────────────────────
ALTER TABLE hrm_leave_application
    ALTER COLUMN total_days TYPE NUMERIC;

-- ── 22. hrm_job_requisition ────────────────────────────────────────────────
ALTER TABLE hrm_job_requisition
    ALTER COLUMN budget_min TYPE NUMERIC,
    ALTER COLUMN budget_max TYPE NUMERIC;

-- ── 23. hrm_offer ──────────────────────────────────────────────────────────
ALTER TABLE hrm_offer
    ALTER COLUMN offered_salary TYPE NUMERIC;

-- ── 24. hrm_employee_movement ──────────────────────────────────────────────
ALTER TABLE hrm_employee_movement
    ALTER COLUMN new_salary TYPE NUMERIC;

-- ── 25. hrm_final_settlement ───────────────────────────────────────────────
ALTER TABLE hrm_final_settlement
    ALTER COLUMN last_salary TYPE NUMERIC,
    ALTER COLUMN leave_encashment TYPE NUMERIC,
    ALTER COLUMN gratuity TYPE NUMERIC,
    ALTER COLUMN bonus_payable TYPE NUMERIC,
    ALTER COLUMN loan_recovery TYPE NUMERIC,
    ALTER COLUMN other_deduction TYPE NUMERIC,
    ALTER COLUMN net_payable TYPE NUMERIC;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'hrm_final_settlement' AND column_name = 'pending_payroll_amount') THEN
        ALTER TABLE hrm_final_settlement
            ALTER COLUMN pending_payroll_amount TYPE NUMERIC,
            ALTER COLUMN pending_bonus_amount TYPE NUMERIC,
            ALTER COLUMN pending_loan_amount TYPE NUMERIC,
            ALTER COLUMN other_payable TYPE NUMERIC;
    END IF;
END $$;

COMMIT;

-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION QUERY (Run this after migration to verify all columns are unconstrained)
-- When numeric_precision and numeric_scale are NULL, the column has NO scale or precision limit.
-- ═══════════════════════════════════════════════════════════════════════════
/*
SELECT 
    table_name, 
    column_name, 
    data_type, 
    numeric_precision, 
    numeric_scale
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name LIKE 'hrm_%'
  AND data_type = 'numeric'
ORDER BY table_name, ordinal_position;
*/
