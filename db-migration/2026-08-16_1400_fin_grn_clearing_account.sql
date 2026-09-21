-- ═══════════════════════════════════════════════════════════════════════════
-- FIN — a real Goods-Received-Not-Invoiced account for GRN_CLEARING
-- Generated: 2026-08-16   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   GoodsReceiptPosted.GRN_CLEARING and GoodsReceiptReversed.GRN_CLEARING both
--   pointed at 2103 Salary Payable — the same account PayrollPosted.PAYABLE and
--   FinalSettlementPosted.PAYABLE credit. The seed picked the nearest existing
--   current liability because no GRNI account existed.
--
--   The consequence is not cosmetic. Receiving goods credited the payroll
--   liability, so Salary Payable carried a balance that was neither owed to an
--   employee nor cleared by a payroll run, and the GRN accrual could never be
--   reconciled against supplier invoices. Verified live: voucher JV000025 (GRN
--   GRN000007) credited 2103 for 8,000.
--
--   This adds 2105 Goods Received Not Invoiced under the same Current
--   Liabilities group and repoints both GRN_CLEARING legs at it.
--
-- SCOPE — FORWARD ONLY
--   Vouchers already posted against 2103 are NOT touched. Posted ledger rows are
--   immutable by design; correcting them is a journal entry an accountant makes,
--   not a migration. Find them with the query at the bottom.
--
-- SAFETY
--   Idempotent. Pure DML. Adds one account per company that has a GRN mapping.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. The account, for every company that posts goods receipts ──────────
--    Cloned from that company's own Accounts Payable so group, currency and
--    branch scoping match the chart it is joining.
INSERT INTO fin_account (
    company_no, branch_no, account_group_no, account_code, account_name,
    root_type, normal_balance, opening_balance, opening_dr_cr,
    is_postable, requires_party, requires_cost_center, control_type,
    currency_no, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    ap.company_no, ap.branch_no, ap.account_group_no, '2105',
    'Goods Received Not Invoiced',
    ap.root_type, 'cr', 0, 'cr',
    1, 0, 0, NULL,
    ap.currency_no, 1, 0, 1, now(), 1
FROM fin_account ap
WHERE ap.account_code = '2101'
  AND ap.is_deleted = 0
  AND EXISTS (
      SELECT 1 FROM fin_gl_map m
      WHERE m.company_no = ap.company_no
        AND m.leg_key = 'GRN_CLEARING'
        AND m.is_deleted = 0
  )
  AND NOT EXISTS (
      SELECT 1 FROM fin_account a
      WHERE a.company_no = ap.company_no
        AND a.account_code = '2105'
        AND a.is_deleted = 0
  );


-- ── 2. Repoint both GRN_CLEARING legs ────────────────────────────────────
UPDATE fin_gl_map m
SET account_no = grni.account_no,
    updated_by = 1,
    updated_at = now()
FROM fin_account grni
WHERE grni.company_no = m.company_no
  AND grni.account_code = '2105'
  AND grni.is_deleted = 0
  AND m.leg_key = 'GRN_CLEARING'
  AND m.event_type IN ('GoodsReceiptPosted', 'GoodsReceiptReversed')
  AND m.is_deleted = 0
  AND m.account_no <> grni.account_no;

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════
-- Both legs now point at 2105:
--
-- SELECT m.event_type, m.leg_key, a.account_code, a.account_name
-- FROM fin_gl_map m JOIN fin_account a ON a.account_no = m.account_no
-- WHERE m.leg_key = 'GRN_CLEARING' AND m.is_deleted = 0
-- ORDER BY m.event_type;
--   → GoodsReceiptPosted / GoodsReceiptReversed | GRN_CLEARING | 2105 | Goods Received Not Invoiced
--
-- Ledger rows already posted to Salary Payable by a goods receipt — these need a
-- correcting journal, this script does not move them:
--
-- SELECT v.voucher_id, v.voucher_date, v.narration, l.debit, l.credit
-- FROM fin_ledger l
-- JOIN fin_voucher v ON v.voucher_no = l.voucher_no
-- JOIN fin_account a ON a.account_no = l.account_no
-- WHERE a.account_code = '2103'
--   AND v.source_event_type IN ('GoodsReceiptPosted', 'GoodsReceiptReversed')
-- ORDER BY v.voucher_no;
