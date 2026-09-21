-- ============================================================================
-- Migration: 2026-09-02_1310_sys_unconstrained_numeric_columns.sql
-- Module: SYS (System / Platform Settings / Currency & Tax Setup)
-- Purpose: Remove all fixed precision and fraction/scale limits (e.g. NUMERIC(20,4),
--          NUMERIC(18,2), NUMERIC(12,6), NUMERIC(5,2)) across all SYS module tables.
--          Converts all exchange rates, tax percentages, and financial amounts to
--          unconstrained NUMERIC so PostgreSQL saves any fraction or number exactly
--          as provided without truncation, leaving display formatting to the frontend.
-- ============================================================================

BEGIN;

-- ── 1. sys_currency ─────────────────────────────────────────────────────────
-- Removes precision/scale constraints from exchange_rate (e.g. NUMERIC(12,6) or NUMERIC(20,4))
ALTER TABLE sys_currency
    ALTER COLUMN exchange_rate TYPE NUMERIC;

-- ── 2. sys_exchange_rate ────────────────────────────────────────────────────
-- Removes precision/scale constraints from historical exchange rate
ALTER TABLE sys_exchange_rate
    ALTER COLUMN rate TYPE NUMERIC;

-- ── 3. sys_vat_tax ──────────────────────────────────────────────────────────
-- Removes precision/scale constraints from VAT / Tax rate percentage (e.g. NUMERIC(5,2))
ALTER TABLE sys_vat_tax
    ALTER COLUMN rate_percentage TYPE NUMERIC;

-- ── 4. sys_approval_request ─────────────────────────────────────────────────
-- Removes precision/scale constraints from approval workflow threshold amount
ALTER TABLE sys_approval_request
    ALTER COLUMN amount TYPE NUMERIC;

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
      'sys_currency', 
      'sys_exchange_rate', 
      'sys_vat_tax', 
      'sys_approval_request'
  )
  AND data_type = 'numeric'
ORDER BY table_name, ordinal_position;
*/
