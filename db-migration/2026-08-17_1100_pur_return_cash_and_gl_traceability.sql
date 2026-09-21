-- ═══════════════════════════════════════════════════════════════════════════
-- PUR + FIN — cash-refund returns, and GL traceability on every posting doc
-- Generated: 2026-08-17   |   run in pgAdmin
--
-- WHAT THIS FIXES
--
-- 1. PURCHASE RETURN SETTLEMENT MODE WAS IGNORED
--    PUR_1103 offers Adjust Payable / Cash Refund / Replacement, and the DTO
--    carries the choice, but Pur1103Service hard-coded settlement_mode = 1 and
--    always debited PAYABLE. A cash refund therefore reduced the supplier's
--    payable as though the debt had been netted off, while the money actually
--    handed back was recorded nowhere.
--
--    The service now honours the mode and emits a CASH leg for mode 2, so the
--    GL map needs PurchaseReturnPosted/Reversed -> CASH. Verified missing by
--    a coverage query over every event x leg the PUR code can emit.
--
-- 2. NO GL TRACEABILITY ON RECEIPT / RETURN / LANDED COST
--    pur_invoice and pur_payment carry gl_voucher_no and get it stamped by the
--    GlPosted listener. pur_receipt, pur_return and pur_landed_cost have no
--    such column, so a posted GRN, return or landed cost cannot be traced to
--    the voucher it produced from the document itself.
--
-- SAFETY
--   Idempotent. Pure DDL/DML. Additive.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. CASH leg for purchase returns ─────────────────────────────────────
--    Cloned from the company's own SupplierPaymentPosted CASH mapping, so a
--    refund lands in the same cash/bank account payments settle from.
INSERT INTO fin_gl_map (
    company_no, branch_no, event_type, leg_key, sub_key, account_no,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT src.company_no, src.branch_no, ev.event_type, 'CASH', NULL, src.account_no,
       1, 0, 1, now(), 1
FROM fin_gl_map src
CROSS JOIN (VALUES ('PurchaseReturnPosted'), ('PurchaseReturnReversed')) AS ev(event_type)
WHERE src.event_type = 'SupplierPaymentPosted'
  AND src.leg_key = 'CASH'
  AND src.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM fin_gl_map m
      WHERE m.company_no = src.company_no
        AND m.event_type = ev.event_type
        AND m.leg_key = 'CASH'
        AND COALESCE(m.sub_key, '') = ''
        AND m.is_deleted = 0
  );


-- ── 2. gl_voucher_no on the remaining posting documents ──────────────────
ALTER TABLE pur_receipt     ADD COLUMN IF NOT EXISTS gl_voucher_no VARCHAR(40);
ALTER TABLE pur_return      ADD COLUMN IF NOT EXISTS gl_voucher_no VARCHAR(40);
ALTER TABLE pur_landed_cost ADD COLUMN IF NOT EXISTS gl_voucher_no VARCHAR(40);

COMMENT ON COLUMN pur_receipt.gl_voucher_no     IS 'Voucher id stamped back by the GlPosted listener.';
COMMENT ON COLUMN pur_return.gl_voucher_no      IS 'Voucher id stamped back by the GlPosted listener.';
COMMENT ON COLUMN pur_landed_cost.gl_voucher_no IS 'Voucher id stamped back by the GlPosted listener.';

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════
-- Every leg the PUR code can emit now resolves:
--
-- WITH emitted(event_type, leg_key) AS (VALUES
--   ('PurchaseReturnPosted','CASH'),('PurchaseReturnReversed','CASH'))
-- SELECT e.event_type, e.leg_key, a.account_code, a.account_name
--   FROM emitted e
--   JOIN fin_gl_map m ON m.event_type = e.event_type AND m.leg_key = e.leg_key
--        AND m.is_deleted = 0 AND COALESCE(m.sub_key,'') = ''
--   JOIN fin_account a ON a.account_no = m.account_no;
--
-- Traceability columns present:
-- SELECT table_name, column_name FROM information_schema.columns
--  WHERE column_name = 'gl_voucher_no' AND table_name LIKE 'pur_%' ORDER BY table_name;
--   -> pur_invoice, pur_landed_cost, pur_payment, pur_receipt, pur_return
