BEGIN;

-- ═══════════════════════════════════════════════════════════════════════════
-- Migration: Add cleared_debits / cleared_credits to fin_bank_recon
-- Date: 2026-08-06
-- Purpose: These columns were [NotMapped] — now persisted for audit and reporting.
-- ═══════════════════════════════════════════════════════════════════════════

ALTER TABLE fin_bank_recon
    ADD COLUMN IF NOT EXISTS cleared_debits  NUMERIC(20,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS cleared_credits NUMERIC(20,4) NOT NULL DEFAULT 0;

COMMIT;
