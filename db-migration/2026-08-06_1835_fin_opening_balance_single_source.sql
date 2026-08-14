BEGIN;

-- ═══════════════════════════════════════════════════════════════════════════
-- Migration: Zero orphaned opening_balance seeds after FIN_1004 posting
-- Date: 2026-08-06
-- Purpose: fin_account.opening_balance is a data-entry seed only. After FIN_1004
--          posts a real Opening voucher into fin_ledger, the seed must be zeroed
--          so reports never double-count.
-- ═══════════════════════════════════════════════════════════════════════════

-- ── Preview (run this SELECT first to see what will change) ──────────────
-- SELECT a.account_no, a.account_code, a.account_name, a.opening_balance, a.opening_dr_cr
-- FROM fin_account a
-- WHERE a.is_deleted = 0
--   AND a.opening_balance != 0
--   AND EXISTS (SELECT 1 FROM fin_voucher v
--               WHERE v.company_no = a.company_no
--                 AND v.source_doc_type = 'FIN_OPENING'
--                 AND v.status = 2 AND v.is_deleted = 0);

-- ── Apply the fix ────────────────────────────────────────────────────────
UPDATE fin_account a
   SET opening_balance = 0,
       opening_dr_cr = 'dr'
 WHERE a.is_deleted = 0
   AND a.opening_balance != 0
   AND EXISTS (SELECT 1 FROM fin_voucher v
               WHERE v.company_no = a.company_no
                 AND v.source_doc_type = 'FIN_OPENING'
                 AND v.status = 2
                 AND v.is_deleted = 0);

COMMIT;
