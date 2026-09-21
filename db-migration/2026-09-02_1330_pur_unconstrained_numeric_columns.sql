-- ============================================================================
-- Migration: 2026-09-02_1330_pur_unconstrained_numeric_columns.sql
-- Module: PUR (Procurement / Purchase)
-- Purpose: Remove all fixed precision and fraction/scale limits (e.g. NUMERIC(20,4),
--          NUMERIC(18,2), NUMERIC(18,4), NUMERIC(12,4)) across all PUR module tables.
--          Converts all supplier balances, order quantities, unit prices, discounts,
--          taxes, invoice amounts, debit notes, receipts, payments, and landed costs
--          to unconstrained NUMERIC so PostgreSQL stores arbitrary decimal fractions
--          as given without database-level truncation, leaving display formatting
--          strictly to the frontend.
-- ============================================================================

BEGIN;

-- ── 1. pur_supplier ────────────────────────────────────────────────────────
ALTER TABLE pur_supplier
    ALTER COLUMN credit_limit TYPE NUMERIC,
    ALTER COLUMN opening_balance TYPE NUMERIC,
    ALTER COLUMN current_payable TYPE NUMERIC;

-- ── 2. pur_supplier_product ────────────────────────────────────────────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'pur_supplier_product') THEN
        ALTER TABLE pur_supplier_product
            ALTER COLUMN last_price TYPE NUMERIC,
            ALTER COLUMN discount_pct TYPE NUMERIC,
            ALTER COLUMN moq TYPE NUMERIC;
    END IF;
END $$;

-- ── 3. pur_supplier_ledger ─────────────────────────────────────────────────
ALTER TABLE pur_supplier_ledger
    ALTER COLUMN credit TYPE NUMERIC,
    ALTER COLUMN debit TYPE NUMERIC,
    ALTER COLUMN balance_after TYPE NUMERIC;

-- ── 4. pur_order ───────────────────────────────────────────────────────────
ALTER TABLE pur_order
    ALTER COLUMN exchange_rate TYPE NUMERIC,
    ALTER COLUMN sub_total TYPE NUMERIC,
    ALTER COLUMN discount_total TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN shipping_estimate TYPE NUMERIC,
    ALTER COLUMN grand_total TYPE NUMERIC,
    ALTER COLUMN received_value TYPE NUMERIC;

-- ── 5. pur_order_dtl ───────────────────────────────────────────────────────
ALTER TABLE pur_order_dtl
    ALTER COLUMN order_qty TYPE NUMERIC,
    ALTER COLUMN order_qty_base TYPE NUMERIC,
    ALTER COLUMN unit_price TYPE NUMERIC,
    ALTER COLUMN discount_pct TYPE NUMERIC,
    ALTER COLUMN discount_amount TYPE NUMERIC,
    ALTER COLUMN tax_rate_pct TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN line_total TYPE NUMERIC,
    ALTER COLUMN received_qty_base TYPE NUMERIC;

-- ── 6. pur_receipt ─────────────────────────────────────────────────────────
ALTER TABLE pur_receipt
    ALTER COLUMN sub_total TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN grand_total TYPE NUMERIC;

-- ── 7. pur_receipt_dtl ─────────────────────────────────────────────────────
ALTER TABLE pur_receipt_dtl
    ALTER COLUMN qty TYPE NUMERIC,
    ALTER COLUMN qty_base TYPE NUMERIC,
    ALTER COLUMN unit_cost TYPE NUMERIC,
    ALTER COLUMN tax_rate_pct TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN line_total TYPE NUMERIC;

-- ── 8. pur_invoice ─────────────────────────────────────────────────────────
ALTER TABLE pur_invoice
    ALTER COLUMN exchange_rate TYPE NUMERIC,
    ALTER COLUMN sub_total TYPE NUMERIC,
    ALTER COLUMN discount_total TYPE NUMERIC,
    ALTER COLUMN taxable_amount TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN shipping_charge TYPE NUMERIC,
    ALTER COLUMN other_charges TYPE NUMERIC,
    ALTER COLUMN round_off TYPE NUMERIC,
    ALTER COLUMN grand_total TYPE NUMERIC,
    ALTER COLUMN paid_amount TYPE NUMERIC,
    ALTER COLUMN due_amount TYPE NUMERIC,
    ALTER COLUMN landed_cost_total TYPE NUMERIC,
    ALTER COLUMN returned_amount TYPE NUMERIC;

-- ── 9. pur_invoice_dtl ─────────────────────────────────────────────────────
ALTER TABLE pur_invoice_dtl
    ALTER COLUMN qty TYPE NUMERIC,
    ALTER COLUMN qty_base TYPE NUMERIC,
    ALTER COLUMN unit_price TYPE NUMERIC,
    ALTER COLUMN discount_pct TYPE NUMERIC,
    ALTER COLUMN discount_amount TYPE NUMERIC,
    ALTER COLUMN tax_rate_pct TYPE NUMERIC,
    ALTER COLUMN taxable_amount TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN line_total TYPE NUMERIC,
    ALTER COLUMN landed_cost_alloc TYPE NUMERIC,
    ALTER COLUMN final_unit_cost TYPE NUMERIC,
    ALTER COLUMN returned_qty_base TYPE NUMERIC;

-- ── 10. pur_return ─────────────────────────────────────────────────────────
ALTER TABLE pur_return
    ALTER COLUMN sub_total TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN grand_total TYPE NUMERIC;

-- ── 11. pur_return_dtl ─────────────────────────────────────────────────────
ALTER TABLE pur_return_dtl
    ALTER COLUMN qty TYPE NUMERIC,
    ALTER COLUMN qty_base TYPE NUMERIC,
    ALTER COLUMN unit_cost TYPE NUMERIC,
    ALTER COLUMN tax_rate_pct TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN line_total TYPE NUMERIC;

-- ── 12. pur_payment ────────────────────────────────────────────────────────
ALTER TABLE pur_payment
    ALTER COLUMN amount TYPE NUMERIC,
    ALTER COLUMN exchange_rate TYPE NUMERIC,
    ALTER COLUMN allocated_amount TYPE NUMERIC,
    ALTER COLUMN unallocated_amount TYPE NUMERIC;

-- ── 13. pur_payment_alloc ──────────────────────────────────────────────────
ALTER TABLE pur_payment_alloc
    ALTER COLUMN allocated_amount TYPE NUMERIC;

-- ── 14. pur_landed_cost ────────────────────────────────────────────────────
ALTER TABLE pur_landed_cost
    ALTER COLUMN amount TYPE NUMERIC;

-- ── 15. pur_landed_cost_alloc ──────────────────────────────────────────────
ALTER TABLE pur_landed_cost_alloc
    ALTER COLUMN allocated_amount TYPE NUMERIC;

-- ── 16. Catch-all: Convert any remaining numeric column in pur_% to NUMERIC ─
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN (
        SELECT table_name, column_name
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name LIKE 'pur_%'
          AND data_type = 'numeric'
          AND numeric_precision IS NOT NULL
    ) LOOP
        EXECUTE format('ALTER TABLE %I ALTER COLUMN %I TYPE NUMERIC;', r.table_name, r.column_name);
    END LOOP;
END $$;

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
  AND table_name LIKE 'pur_%'
  AND data_type = 'numeric'
ORDER BY table_name, ordinal_position;
*/
