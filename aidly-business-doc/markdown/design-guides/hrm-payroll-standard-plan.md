# Aidly ERP HRM Payroll Standard Plan

## Goal

Build payroll as a small but proper SME payroll engine, not a single salary field on the employee. The employee keeps the job identity; payroll keeps versioned pay rules and monthly snapshots.

## Core Model

1. Grade defines the salary band.
2. Grade Step defines the progression amount inside the grade.
3. Designation maps to one or more grades.
4. Salary Structure assigns the employee's effective salary by designation, grade, and grade step.
5. Salary Components define earnings, deductions, statutory deductions, employer contributions, and formulas.
6. Payroll Run calculates and freezes payslips for a period.
7. Payslip stores the frozen employee result and component lines.
8. Final Settlement uses unpaid salary, leave encashment, bonus, loan/advance, and other dues.

## Bangladesh SME Defaults

- Currency: `BDT`
- Weekend default: `FRI,SAT`
- Overtime default formula: `BASIC_SALARY / 104`
- Tax regime: `BD_NBR`
- Festival bonus enabled
- Provident fund enabled as a configurable component
- Gratuity/final-settlement calculation enabled as a configurable policy

These must stay configurable through HRM settings/policy rows. Do not hard-code Bangladesh-specific values inside calculation services.

## Build Steps

1. Payroll foundation DB

Manual script: `sme-software-backend/db-migration/2026-06_hrm_payroll_standard_enhancements.sql`

This adds grade steps, salary assignment references, formula fields, payroll snapshots, BD policy seeds, and final-settlement support.

2. Grade / Pay-Scale Setup

Use `hrm_grade_step` as the child table. Each step amount must be inside the grade minimum and maximum salary range.

3. Designation Setup

Designation should optionally map to grade. Job Type replaces the old Job Category label, while the current numeric `job_category` field can remain as the stored enum unless a later migration renames it.

4. Salary Component Setup

Add formula support:

- Fixed amount
- Percent of basic
- Percent of gross
- Formula expression
- Statutory flag
- Taxable flag
- Affects net flag
- Proratable flag
- Attendance-dependent flag

5. Salary Structure Setup

Salary assignment should be based on employee, designation, grade, and grade step. Only one active salary structure is allowed per employee. Every revision creates a new version and supersedes the old one.

6. Payroll Processing

The calculation flow should be:

```text
employee + active salary structure
+ attendance and leave
+ overtime
+ bonus
- loan/advance recovery
- tax/statutory deductions
= payslip snapshot
```

Payroll states:

```text
Draft -> Calculated -> Approved -> Paid
```

After approval, attendance rows for the payroll period should be locked. After paid, the run must be immutable.

7. Salary Sheet and Payslip

Reports must read from `hrm_payslip` and `hrm_payslip_dtl`, not recalculate live. This preserves historical accuracy if components or formulas change later.

8. Final Settlement

Final settlement should auto-read unpaid months, pending payroll, pending bonus, pending loan/advance, leave encashment, gratuity, and other dues. The result should be stored as a calculation snapshot.

## Current Gaps Closed By The Script

- Grade steps now have a real table.
- Salary structures can point to designation, grade, and step.
- Salary components can carry formulas and policy flags.
- Payroll runs can track calculation lifecycle metadata.
- Payslips can snapshot salary structure, grade, step, basic salary, OT, tax, and calculation details.
- Final settlement can store unpaid months and calculation snapshots.
- BD payroll defaults are seeded into policy/settings data.

## Next Implementation Order

1. Update backend entities/DTOs for the new columns.
2. Complete Grade Step save/load in HRM_1004.
3. Update HRM_1201 Salary Structure UI to assign salary by designation, grade, and step.
4. Update HRM_1007 Salary Component UI for formulas and flags.
5. Refactor HRM_1202 calculation service to create immutable payslip snapshots.
6. Update HRM_1207 Final Settlement to calculate from payroll facts.
