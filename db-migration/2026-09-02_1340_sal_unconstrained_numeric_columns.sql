-- ============================================================================
-- Migration: 2026-09-02_1340_sal_unconstrained_numeric_columns.sql
-- Module: SAL (Sales, Customers, Invoicing, POS & Promotions)
-- Purpose: Remove all fixed precision and fraction/scale limits (e.g. NUMERIC(20,4),
--          NUMERIC(18,4), NUMERIC(18,2), NUMERIC(5,2)) across all SAL module tables.
--          Converts all customer balances, invoice amounts, line discounts, tax amounts,
--          loyalty points, POS session drawer amounts, split tenders, credit notes,
--          receipts, and promotion parameters to unconstrained NUMERIC so PostgreSQL
--          stores arbitrary decimal fractions as given without database-level truncation,
--          leaving display formatting strictly to the frontend.
-- ============================================================================

BEGIN;

-- ── 1. sal_customer ────────────────────────────────────────────────────────
ALTER TABLE sal_customer
    ALTER COLUMN credit_limit TYPE NUMERIC,
    ALTER COLUMN opening_balance TYPE NUMERIC,
    ALTER COLUMN current_due TYPE NUMERIC,
    ALTER COLUMN loyalty_points TYPE NUMERIC;

-- ── 2. sal_customer_group ──────────────────────────────────────────────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'sal_customer_group') THEN
        ALTER TABLE sal_customer_group
            ALTER COLUMN default_discount_pct TYPE NUMERIC;
    END IF;
END $$;

-- ── 3. sal_customer_ledger ─────────────────────────────────────────────────
ALTER TABLE sal_customer_ledger
    ALTER COLUMN debit TYPE NUMERIC,
    ALTER COLUMN credit TYPE NUMERIC,
    ALTER COLUMN balance_after TYPE NUMERIC;

-- ── 4. sal_invoice ─────────────────────────────────────────────────────────
ALTER TABLE sal_invoice
    ALTER COLUMN exchange_rate TYPE NUMERIC,
    ALTER COLUMN sub_total TYPE NUMERIC,
    ALTER COLUMN line_discount_total TYPE NUMERIC,
    ALTER COLUMN taxable_amount TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN shipping_charge TYPE NUMERIC,
    ALTER COLUMN round_off TYPE NUMERIC,
    ALTER COLUMN grand_total TYPE NUMERIC,
    ALTER COLUMN paid_amount TYPE NUMERIC,
    ALTER COLUMN change_amount TYPE NUMERIC,
    ALTER COLUMN due_amount TYPE NUMERIC,
    ALTER COLUMN returned_amount TYPE NUMERIC,
    ALTER COLUMN total_cost TYPE NUMERIC,
    ALTER COLUMN bill_discount_value TYPE NUMERIC,
    ALTER COLUMN bill_discount_amount TYPE NUMERIC,
    ALTER COLUMN promotion_discount TYPE NUMERIC;

-- ── 5. sal_invoice_dtl ─────────────────────────────────────────────────────
ALTER TABLE sal_invoice_dtl
    ALTER COLUMN qty TYPE NUMERIC,
    ALTER COLUMN qty_base TYPE NUMERIC,
    ALTER COLUMN unit_price TYPE NUMERIC,
    ALTER COLUMN line_discount_pct TYPE NUMERIC,
    ALTER COLUMN line_discount_amount TYPE NUMERIC,
    ALTER COLUMN tax_rate_pct TYPE NUMERIC,
    ALTER COLUMN taxable_amount TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN line_total TYPE NUMERIC,
    ALTER COLUMN unit_cost TYPE NUMERIC,
    ALTER COLUMN line_cost TYPE NUMERIC,
    ALTER COLUMN returned_qty TYPE NUMERIC,
    ALTER COLUMN mrp TYPE NUMERIC,
    ALTER COLUMN line_discount_value TYPE NUMERIC,
    ALTER COLUMN promotion_discount TYPE NUMERIC;

-- ── 6. sal_invoice_payment ─────────────────────────────────────────────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'sal_invoice_payment') THEN
        ALTER TABLE sal_invoice_payment
            ALTER COLUMN amount TYPE NUMERIC,
            ALTER COLUMN tendered_amount TYPE NUMERIC,
            ALTER COLUMN points_redeemed TYPE NUMERIC;
    END IF;
END $$;

-- ── 7. sal_pos_session ─────────────────────────────────────────────────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'sal_pos_session') THEN
        ALTER TABLE sal_pos_session
            ALTER COLUMN opening_float TYPE NUMERIC,
            ALTER COLUMN expected_cash TYPE NUMERIC,
            ALTER COLUMN expected_card TYPE NUMERIC,
            ALTER COLUMN expected_mobile TYPE NUMERIC,
            ALTER COLUMN expected_other TYPE NUMERIC,
            ALTER COLUMN counted_cash TYPE NUMERIC,
            ALTER COLUMN cash_variance TYPE NUMERIC,
            ALTER COLUMN total_sales TYPE NUMERIC,
            ALTER COLUMN total_returns TYPE NUMERIC;
    END IF;
END $$;

-- ── 8. sal_receipt ─────────────────────────────────────────────────────────
ALTER TABLE sal_receipt
    ALTER COLUMN amount TYPE NUMERIC,
    ALTER COLUMN allocated_amount TYPE NUMERIC,
    ALTER COLUMN unallocated_amount TYPE NUMERIC;

-- ── 9. sal_receipt_alloc ───────────────────────────────────────────────────
ALTER TABLE sal_receipt_alloc
    ALTER COLUMN allocated_amount TYPE NUMERIC;

-- ── 10. sal_return ─────────────────────────────────────────────────────────
ALTER TABLE sal_return
    ALTER COLUMN sub_total TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN grand_total TYPE NUMERIC,
    ALTER COLUMN refund_amount TYPE NUMERIC,
    ALTER COLUMN total_cost TYPE NUMERIC;

-- ── 11. sal_return_dtl ─────────────────────────────────────────────────────
ALTER TABLE sal_return_dtl
    ALTER COLUMN qty TYPE NUMERIC,
    ALTER COLUMN qty_base TYPE NUMERIC,
    ALTER COLUMN unit_price TYPE NUMERIC,
    ALTER COLUMN net_unit_price TYPE NUMERIC,
    ALTER COLUMN tax_rate_pct TYPE NUMERIC,
    ALTER COLUMN tax_amount TYPE NUMERIC,
    ALTER COLUMN line_total TYPE NUMERIC,
    ALTER COLUMN unit_cost TYPE NUMERIC,
    ALTER COLUMN line_cost TYPE NUMERIC;

-- ── 12. sal_promotion ──────────────────────────────────────────────────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'sal_promotion') THEN
        ALTER TABLE sal_promotion
            ALTER COLUMN min_qty TYPE NUMERIC,
            ALTER COLUMN min_amount TYPE NUMERIC,
            ALTER COLUMN discount_pct TYPE NUMERIC,
            ALTER COLUMN discount_amount TYPE NUMERIC,
            ALTER COLUMN buy_qty TYPE NUMERIC,
            ALTER COLUMN get_qty TYPE NUMERIC,
            ALTER COLUMN max_discount_amount TYPE NUMERIC;
    END IF;
END $$;

-- ── 13. sal_promotion_dtl ──────────────────────────────────────────────────
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'sal_promotion_dtl') THEN
        ALTER TABLE sal_promotion_dtl
            ALTER COLUMN qty TYPE NUMERIC;
    END IF;
END $$;

-- ── 14. Catch-all: Convert any remaining numeric column in sal_% to NUMERIC ─
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN (
        SELECT table_name, column_name
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name LIKE 'sal_%'
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
  AND table_name LIKE 'sal_%'
  AND data_type = 'numeric'
ORDER BY table_name, ordinal_position;
*/
