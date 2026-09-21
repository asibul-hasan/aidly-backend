-- ============================================================================
-- Migration: 2026-09-02_1300_fin_unconstrained_numeric_columns.sql
-- Module: FIN (Financial Accounting / General Ledger)
-- Purpose: Remove all fixed precision and fraction/scale limits (e.g. NUMERIC(20,4),
--          NUMERIC(18,2), NUMERIC(5,2), NUMERIC(12,6)) across all FIN module tables.
--          Converts all number/currency/rate fields to unconstrained NUMERIC so the
--          database saves any value/fraction as given without truncation or rounding,
--          leaving display formatting exclusively to the frontend.
-- ============================================================================

BEGIN;

-- ── 1. fin_account ─────────────────────────────────────────────────────────
-- Removes NUMERIC(20,4) from opening_balance
ALTER TABLE fin_account
    ALTER COLUMN opening_balance TYPE NUMERIC;

-- ── 2. fin_account_balance ──────────────────────────────────────────────────
-- Removes fixed scale limits from opening and period balance movements
ALTER TABLE fin_account_balance
    ALTER COLUMN opening_debit TYPE NUMERIC,
    ALTER COLUMN opening_credit TYPE NUMERIC,
    ALTER COLUMN period_debit TYPE NUMERIC,
    ALTER COLUMN period_credit TYPE NUMERIC;

-- ── 3. fin_voucher ──────────────────────────────────────────────────────────
-- Removes NUMERIC(12,6) on fx_rate and NUMERIC(20,4) / (18,2) on totals
ALTER TABLE fin_voucher
    ALTER COLUMN fx_rate TYPE NUMERIC,
    ALTER COLUMN total_debit TYPE NUMERIC,
    ALTER COLUMN total_credit TYPE NUMERIC;

-- ── 4. fin_voucher_dtl ──────────────────────────────────────────────────────
-- Removes fixed fraction limits from FC and base currency debit/credit & VAT rate
ALTER TABLE fin_voucher_dtl
    ALTER COLUMN debit_fc TYPE NUMERIC,
    ALTER COLUMN credit_fc TYPE NUMERIC,
    ALTER COLUMN debit TYPE NUMERIC,
    ALTER COLUMN credit TYPE NUMERIC;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'fin_voucher_dtl' AND column_name = 'tax_rate_pct'
    ) THEN
        ALTER TABLE fin_voucher_dtl ALTER COLUMN tax_rate_pct TYPE NUMERIC;
    END IF;
END $$;

-- ── 5. fin_ledger ───────────────────────────────────────────────────────────
-- Removes fixed fraction limits from immutable general ledger debit and credit
ALTER TABLE fin_ledger
    ALTER COLUMN debit TYPE NUMERIC,
    ALTER COLUMN credit TYPE NUMERIC;

-- ── 6. fin_bank_account ─────────────────────────────────────────────────────
-- Removes fixed fraction limits from bank opening balance
ALTER TABLE fin_bank_account
    ALTER COLUMN opening_balance TYPE NUMERIC;

-- ── 7. fin_bank_recon ───────────────────────────────────────────────────────
-- Removes NUMERIC(20,4) / (18,2) from statement and cleared balance columns
ALTER TABLE fin_bank_recon
    ALTER COLUMN statement_balance TYPE NUMERIC,
    ALTER COLUMN book_balance TYPE NUMERIC,
    ALTER COLUMN cleared_balance TYPE NUMERIC,
    ALTER COLUMN difference TYPE NUMERIC;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'fin_bank_recon' AND column_name = 'cleared_debits'
    ) THEN
        ALTER TABLE fin_bank_recon ALTER COLUMN cleared_debits TYPE NUMERIC;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'fin_bank_recon' AND column_name = 'cleared_credits'
    ) THEN
        ALTER TABLE fin_bank_recon ALTER COLUMN cleared_credits TYPE NUMERIC;
    END IF;
END $$;

-- ── 8. fin_bank_recon_line ──────────────────────────────────────────────────
-- Removes fixed fraction limits from reconciliation line items
ALTER TABLE fin_bank_recon_line
    ALTER COLUMN debit TYPE NUMERIC,
    ALTER COLUMN credit TYPE NUMERIC;

COMMIT;

-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION QUERY (Run this after migration to verify all columns are unconstrained)
-- When numeric_precision and numeric_scale are NULL, the column has NO scale or precision limit.
-- ═══════════════════════════════════════════════════════════════════════════
/*
SELECT 
    table_name, 
    column_name, 
    data_type, 
    numeric_precision, 
    numeric_scale
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN (
      'fin_account', 
      'fin_account_balance', 
      'fin_voucher', 
      'fin_voucher_dtl', 
      'fin_ledger', 
      'fin_bank_account', 
      'fin_bank_recon', 
      'fin_bank_recon_line'
  )
  AND data_type = 'numeric'
ORDER BY table_name, ordinal_position;
*/
