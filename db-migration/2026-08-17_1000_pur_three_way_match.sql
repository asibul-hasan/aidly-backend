-- ═══════════════════════════════════════════════════════════════════════════
-- PUR + FIN — three-way match: stop the invoice re-receiving GRN goods
-- Generated: 2026-08-17   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   PUR_1105 (GRN) and PUR_1102 (invoice) were two independent receiving
--   paths, and nothing linked or excluded them. Running the natural sequence
--   PO -> GRN -> Invoice -> Payment therefore took the SAME physical goods
--   into stock twice.
--
--   Verified live before the fix: GRN000012 and PINV000009 each covered the
--   same 6 units, and inv_stock_ledger recorded both (balance 268 -> 274 ->
--   280). The GL agreed with the error:
--       JV000028  GRN      Dr Inventory 9,000 / Cr GRN clearing 9,000
--       JV000029  Invoice  Dr Inventory 9,000 / Cr Accounts payable 9,000
--   Inventory was debited 18,000 for a 9,000 purchase, and the 9,000 in GRN
--   clearing had nothing that would ever clear it.
--
--   pur_invoice_dtl.receipt_dtl_no now links an invoice line to the receipt
--   line it bills. Pur1102Service reads it:
--       linked   -> no stock movement, Dr GRN clearing / Cr AP  (clears the accrual)
--       null     -> unchanged one-step path, Dr Inventory / Cr AP
--   so both the two-step and one-step flows stay available and neither
--   double-counts.
--
--   The GL map needs GRN_CLEARING on the invoice events for the linked path.
--
-- SAFETY
--   Idempotent. Pure DDL/DML. Additive — existing invoices keep receipt_dtl_no
--   NULL and behave exactly as before.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. The link ──────────────────────────────────────────────────────────
ALTER TABLE pur_invoice_dtl ADD COLUMN IF NOT EXISTS receipt_dtl_no BIGINT;

COMMENT ON COLUMN pur_invoice_dtl.receipt_dtl_no IS
    'Goods-receipt line this invoice line bills. Set = three-way match: clears GRN clearing, posts no stock. NULL = one-step invoice that receives the goods itself.';

-- Looking up "is this receipt line already billed?" is the guard on every submit.
CREATE INDEX IF NOT EXISTS idx_pur_invoice_dtl_receipt
    ON pur_invoice_dtl (receipt_dtl_no)
    WHERE receipt_dtl_no IS NOT NULL AND is_deleted = 0;

-- Referential integrity: a billed receipt line must exist.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_pur_invoice_dtl_receipt_dtl'
    ) THEN
        ALTER TABLE pur_invoice_dtl
            ADD CONSTRAINT fk_pur_invoice_dtl_receipt_dtl
            FOREIGN KEY (receipt_dtl_no) REFERENCES pur_receipt_dtl (receipt_dtl_no);
    END IF;
END $$;


-- ── 2. GL map — the invoice's GRN_CLEARING leg ───────────────────────────
--    Same account the goods receipt credits, so the pair nets to zero once the
--    supplier bills for what was received.
INSERT INTO fin_gl_map (
    company_no, branch_no, event_type, leg_key, sub_key, account_no,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT src.company_no, src.branch_no, ev.event_type, 'GRN_CLEARING', NULL, src.account_no,
       1, 0, 1, now(), 1
FROM fin_gl_map src
CROSS JOIN (VALUES ('PurchaseInvoicePosted'), ('PurchaseInvoiceReversed')) AS ev(event_type)
WHERE src.event_type = 'GoodsReceiptPosted'
  AND src.leg_key = 'GRN_CLEARING'
  AND src.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM fin_gl_map m
      WHERE m.company_no = src.company_no
        AND m.event_type = ev.event_type
        AND m.leg_key = 'GRN_CLEARING'
        AND COALESCE(m.sub_key, '') = ''
        AND m.is_deleted = 0
  );

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════
-- 1. Column + index + FK:
-- SELECT column_name FROM information_schema.columns
--  WHERE table_name='pur_invoice_dtl' AND column_name='receipt_dtl_no';
--
-- 2. Both invoice events can now resolve a GRN_CLEARING leg:
-- SELECT m.event_type, m.leg_key, a.account_code, a.account_name
--   FROM fin_gl_map m JOIN fin_account a ON a.account_no = m.account_no
--  WHERE m.leg_key = 'GRN_CLEARING' AND m.is_deleted = 0
--  ORDER BY m.event_type;
--   -> GoodsReceiptPosted / GoodsReceiptReversed / PurchaseInvoicePosted /
--      PurchaseInvoiceReversed, all on 2105.
--
-- 3. After receiving on a GRN and billing it with the link set, GRN clearing
--    nets to zero for that delivery:
-- SELECT sum(l.credit) - sum(l.debit) AS grni_balance
--   FROM fin_ledger l JOIN fin_account a ON a.account_no = l.account_no
--  WHERE a.account_code = '2105' AND l.is_deleted = 0;
--
-- NOTE on existing data: invoices posted before this migration double-counted
-- stock. They are NOT corrected here — that needs a stock adjustment and a
-- correcting journal, which is an accountant's decision, not a migration's.
