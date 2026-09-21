-- ============================================================================
-- Migration: 2026-09-02_1325_hrm_remaining_unconstrained_numeric_columns.sql
-- Module: HRM (Human Resource Management & Payroll)
-- Purpose: Remove the remaining 12 fixed-precision constraints in HRM tables:
--          hrm_department.max_leave_percentage, hrm_designation (min_salary, max_salary),
--          hrm_final_settlement (pending amounts and other_payable),
--          hrm_leave_encashment (days, rate, amount), and
--          hrm_leave_policy_setup (min_balance_for_encashment).
-- ============================================================================

BEGIN;

-- ── 1. hrm_department ──────────────────────────────────────────────────────
ALTER TABLE hrm_department
    ALTER COLUMN max_leave_percentage TYPE NUMERIC;

-- ── 2. hrm_designation ─────────────────────────────────────────────────────
ALTER TABLE hrm_designation
    ALTER COLUMN min_salary TYPE NUMERIC,
    ALTER COLUMN max_salary TYPE NUMERIC;

-- ── 3. hrm_final_settlement ────────────────────────────────────────────────
ALTER TABLE hrm_final_settlement
    ALTER COLUMN pending_payroll_amount TYPE NUMERIC,
    ALTER COLUMN pending_bonus_amount TYPE NUMERIC,
    ALTER COLUMN pending_loan_amount TYPE NUMERIC,
    ALTER COLUMN other_payable TYPE NUMERIC;

-- ── 4. hrm_leave_encashment ────────────────────────────────────────────────
ALTER TABLE hrm_leave_encashment
    ALTER COLUMN requested_days TYPE NUMERIC,
    ALTER COLUMN approved_days TYPE NUMERIC,
    ALTER COLUMN rate_per_day TYPE NUMERIC,
    ALTER COLUMN amount TYPE NUMERIC;

-- ── 5. hrm_leave_policy_setup ─────────────────────────────────────────────
ALTER TABLE hrm_leave_policy_setup
    ALTER COLUMN min_balance_for_encashment TYPE NUMERIC;

COMMIT;
