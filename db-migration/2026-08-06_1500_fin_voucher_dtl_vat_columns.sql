BEGIN;

-- ═══════════════════════════════════════════════════════════════════════════
-- Migration: Add VAT per-rate columns to fin_voucher_dtl and fin_ledger
-- Date: 2026-08-06
-- Purpose: Support per-rate VAT segregation for multi-rate tax scenarios
-- ═══════════════════════════════════════════════════════════════════════════

-- ── fin_voucher_dtl ──────────────────────────────────────────────────────

ALTER TABLE fin_voucher_dtl
    ADD COLUMN IF NOT EXISTS vat_tax_no BIGINT,
    ADD COLUMN IF NOT EXISTS tax_rate_pct NUMERIC(5,2);

COMMENT ON COLUMN fin_voucher_dtl.vat_tax_no IS 'FK to sys_vat_tax — identifies which tax rate applies to this line';
COMMENT ON COLUMN fin_voucher_dtl.tax_rate_pct IS 'Denormalized tax rate percentage at time of posting';

-- ── fin_ledger ───────────────────────────────────────────────────────────

ALTER TABLE fin_ledger
    ADD COLUMN IF NOT EXISTS vat_tax_no BIGINT;

COMMENT ON COLUMN fin_ledger.vat_tax_no IS 'FK to sys_vat_tax — carried from the voucher line for per-rate GL segregation';

COMMIT;
