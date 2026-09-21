BEGIN;

-- ═══════════════════════════════════════════════════════════════════════════
-- Migration: Add VatTaxNo to sal_invoice_dtl and pur_invoice_dtl
-- Date: 2026-08-06
-- Purpose: Support per-rate VAT segregation in sales and purchase invoices
-- ═══════════════════════════════════════════════════════════════════════════

-- ── sal_invoice_dtl ──────────────────────────────────────────────────────

ALTER TABLE sal_invoice_dtl
    ADD COLUMN IF NOT EXISTS vat_tax_no BIGINT;

COMMENT ON COLUMN sal_invoice_dtl.vat_tax_no IS 'FK to sys_vat_tax — identifies which tax rate applies to this line';

-- ── pur_invoice_dtl ──────────────────────────────────────────────────────

ALTER TABLE pur_invoice_dtl
    ADD COLUMN IF NOT EXISTS vat_tax_no BIGINT;

COMMENT ON COLUMN pur_invoice_dtl.vat_tax_no IS 'FK to sys_vat_tax — identifies which tax rate applies to this line';

COMMIT;
