-- ═══════════════════════════════════════════════════════════════════════════
-- SYS — retire the duplicate overlapping financial year (company 2)
-- Generated: 2026-08-14   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   Company 2 had TWO open financial years covering the same dates:
--
--     no  fin_year_id       start        end          periods  created
--     ──────────────────────────────────────────────────────────────────────
--      9  FY-JUN26-DEC26    2026-06-01   2026-12-31      7      2026-06-11
--     10  FY-JUL26-DEC26    2026-07-01   2026-12-31      6      2026-07-15
--
--   Every posting path resolves its year with a single-row lookup for the year
--   covering a date. With two matches that lookup returns whichever row the
--   planner happens to hand back first, so the SAME transaction could post to
--   either year depending on row order. That affects stock postings, GL
--   vouchers and every AR/AP movement — not just POS.
--
-- WHICH ONE GOES, AND WHY
--   Year 9 carries all the data:
--     fin_account_balance 6 · fin_ledger 15 · fin_voucher 6
--     inv_stock_ledger 1 · sal_invoice 1 · periods 7
--   Year 10 carries none — only its own 6 period rows, and it was created a
--   month after year 9. It is an empty duplicate, so year 10 is retired.
--
-- SOFT DELETE, NOT DROP
--   House rule: nothing is hard-deleted. Both the year and its periods are
--   flagged is_deleted = 1, which is what every query already filters on. The
--   rows stay auditable and the change is reversible by setting the flag back.
--
-- SAFETY
--   Idempotent. Pure DML. The guard means it can only ever touch a year with
--   no transactional rows pointing at it — if year 10 somehow acquired data
--   between writing and running this, it updates nothing rather than
--   orphaning that data.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. The periods belonging to the duplicate year ───────────────────────
UPDATE sys_fin_year_dtl d
SET    is_deleted = 1,
       is_active  = 0,
       deleted_at = now(),
       deleted_by = 1
WHERE  d.fin_year_no = 10
  AND  d.is_deleted = 0
  AND  NOT EXISTS (SELECT 1 FROM fin_ledger      x WHERE x.fin_year_no = 10)
  AND  NOT EXISTS (SELECT 1 FROM fin_voucher     x WHERE x.fin_year_no = 10)
  AND  NOT EXISTS (SELECT 1 FROM sal_invoice     x WHERE x.fin_year_no = 10)
  AND  NOT EXISTS (SELECT 1 FROM inv_stock_ledger x WHERE x.fin_year_no = 10);

-- ── 2. The year itself ───────────────────────────────────────────────────
UPDATE sys_fin_year y
SET    is_deleted = 1,
       is_active  = 0,
       deleted_at = now(),
       deleted_by = 1
WHERE  y.fin_year_no = 10
  AND  y.is_deleted = 0
  AND  NOT EXISTS (SELECT 1 FROM fin_ledger       x WHERE x.fin_year_no = 10)
  AND  NOT EXISTS (SELECT 1 FROM fin_voucher      x WHERE x.fin_year_no = 10)
  AND  NOT EXISTS (SELECT 1 FROM sal_invoice      x WHERE x.fin_year_no = 10)
  AND  NOT EXISTS (SELECT 1 FROM inv_stock_ledger x WHERE x.fin_year_no = 10);

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════

-- Exactly ONE open year may cover any given date:
-- SELECT count(*) FROM sys_fin_year
-- WHERE company_no = 2 AND is_deleted = 0
--   AND CURRENT_DATE BETWEEN start_date AND end_date;
--   → must be 1.

-- Nothing else overlaps either — run this whenever a year is added:
-- SELECT a.fin_year_no, a.fin_year_id, b.fin_year_no, b.fin_year_id
-- FROM sys_fin_year a
-- JOIN sys_fin_year b
--   ON b.company_no = a.company_no AND b.fin_year_no > a.fin_year_no
--  AND a.start_date <= b.end_date AND b.start_date <= a.end_date
-- WHERE a.is_deleted = 0 AND b.is_deleted = 0
-- ORDER BY a.company_no;
--   → must return no rows.
