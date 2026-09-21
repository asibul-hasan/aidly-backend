BEGIN;

-- ═══════════════════════════════════════════════════════════════════════════
-- Migration: Add gl_voucher_no to source document tables
-- Date: 2026-08-10
-- Purpose: Store the GL voucher ID on each source document after auto-posting,
--          enabling drill-through from document to journal entry.
-- ═══════════════════════════════════════════════════════════════════════════

-- ── SAL module ───────────────────────────────────────────────────────────

ALTER TABLE sal_invoice
    ADD COLUMN IF NOT EXISTS gl_voucher_no VARCHAR(30);

COMMENT ON COLUMN sal_invoice.gl_voucher_no IS 'GL voucher ID from FIN auto-posting — links to fin_voucher.voucher_id';

ALTER TABLE sal_receipt
    ADD COLUMN IF NOT EXISTS gl_voucher_no VARCHAR(30);

COMMENT ON COLUMN sal_receipt.gl_voucher_no IS 'GL voucher ID from FIN auto-posting — links to fin_voucher.voucher_id';

ALTER TABLE sal_return
    ADD COLUMN IF NOT EXISTS gl_voucher_no VARCHAR(30);

COMMENT ON COLUMN sal_return.gl_voucher_no IS 'GL voucher ID from FIN auto-posting — links to fin_voucher.voucher_id';

-- ── PUR module ───────────────────────────────────────────────────────────

ALTER TABLE pur_invoice
    ADD COLUMN IF NOT EXISTS gl_voucher_no VARCHAR(30);

COMMENT ON COLUMN pur_invoice.gl_voucher_no IS 'GL voucher ID from FIN auto-posting — links to fin_voucher.voucher_id';

ALTER TABLE pur_payment
    ADD COLUMN IF NOT EXISTS gl_voucher_no VARCHAR(30);

COMMENT ON COLUMN pur_payment.gl_voucher_no IS 'GL voucher ID from FIN auto-posting — links to fin_voucher.voucher_id';

-- ── INV module ───────────────────────────────────────────────────────────

ALTER TABLE inv_stock_adjustment
    ADD COLUMN IF NOT EXISTS gl_voucher_no VARCHAR(30);

COMMENT ON COLUMN inv_stock_adjustment.gl_voucher_no IS 'GL voucher ID from FIN auto-posting — links to fin_voucher.voucher_id';

-- ── HRM module ───────────────────────────────────────────────────────────

ALTER TABLE hrm_payroll_run
    ADD COLUMN IF NOT EXISTS gl_voucher_no VARCHAR(30);

COMMENT ON COLUMN hrm_payroll_run.gl_voucher_no IS 'GL voucher ID from FIN auto-posting — links to fin_voucher.voucher_id';

COMMIT;
