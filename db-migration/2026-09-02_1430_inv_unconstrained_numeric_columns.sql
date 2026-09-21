-- ============================================================================
-- Migration: 2026-09-02_1430_inv_unconstrained_numeric_columns.sql
-- Module: INV (Inventory, Product Catalog, Stock, Warehousing, Physical Count)
-- Purpose: Remove all fixed precision and fraction/scale limits (e.g. NUMERIC(20,6),
--          NUMERIC(20,4), NUMERIC(18,6), NUMERIC(18,4), NUMERIC(5,2)) across all INV
--          module tables.
--          Converts all product costs, prices, MRPs, conversion factors, stock levels,
--          reorder levels, valuation layers, ledger movement costs, and physical count
--          quantities to unconstrained NUMERIC so PostgreSQL stores arbitrary decimal
--          fractions as given without database-level truncation, leaving formatting
--          strictly to the frontend.
-- ============================================================================

BEGIN;

-- ── 1. inv_product ─────────────────────────────────────────────────────────
ALTER TABLE inv_product
    ALTER COLUMN cost_price TYPE NUMERIC,
    ALTER COLUMN purchase_price TYPE NUMERIC,
    ALTER COLUMN sale_price TYPE NUMERIC,
    ALTER COLUMN mrp TYPE NUMERIC,
    ALTER COLUMN min_sale_price TYPE NUMERIC,
    ALTER COLUMN default_margin_pct TYPE NUMERIC,
    ALTER COLUMN reorder_level TYPE NUMERIC,
    ALTER COLUMN reorder_qty TYPE NUMERIC,
    ALTER COLUMN min_stock TYPE NUMERIC,
    ALTER COLUMN max_stock TYPE NUMERIC,
    ALTER COLUMN weight_gm TYPE NUMERIC;

-- ── 2. inv_product_variant ─────────────────────────────────────────────────
ALTER TABLE inv_product_variant
    ALTER COLUMN purchase_price TYPE NUMERIC,
    ALTER COLUMN sale_price TYPE NUMERIC,
    ALTER COLUMN mrp TYPE NUMERIC;

-- ── 3. inv_product_barcode ─────────────────────────────────────────────────
ALTER TABLE inv_product_barcode
    ALTER COLUMN pack_qty TYPE NUMERIC;

-- ── 4. inv_uom_conversion ──────────────────────────────────────────────────
ALTER TABLE inv_uom_conversion
    ALTER COLUMN to_base_factor TYPE NUMERIC;

-- ── 5. inv_batch ───────────────────────────────────────────────────────────
ALTER TABLE inv_batch
    ALTER COLUMN received_cost TYPE NUMERIC,
    ALTER COLUMN mrp TYPE NUMERIC;

-- ── 6. inv_stock ───────────────────────────────────────────────────────────
ALTER TABLE inv_stock
    ALTER COLUMN qty_on_hand TYPE NUMERIC,
    ALTER COLUMN qty_reserved TYPE NUMERIC,
    ALTER COLUMN qty_available TYPE NUMERIC,
    ALTER COLUMN avg_cost TYPE NUMERIC,
    ALTER COLUMN last_cost TYPE NUMERIC,
    ALTER COLUMN stock_value TYPE NUMERIC;

-- ── 7. inv_stock_ledger ────────────────────────────────────────────────────
ALTER TABLE inv_stock_ledger
    ALTER COLUMN qty_base TYPE NUMERIC,
    ALTER COLUMN unit_cost TYPE NUMERIC,
    ALTER COLUMN total_cost TYPE NUMERIC,
    ALTER COLUMN balance_after TYPE NUMERIC,
    ALTER COLUMN avg_cost_after TYPE NUMERIC;

-- ── 8. inv_valuation_layer ─────────────────────────────────────────────────
ALTER TABLE inv_valuation_layer
    ALTER COLUMN original_qty TYPE NUMERIC,
    ALTER COLUMN remaining_qty TYPE NUMERIC,
    ALTER COLUMN unit_cost TYPE NUMERIC;

-- ── 9. inv_reorder ─────────────────────────────────────────────────────────
ALTER TABLE inv_reorder
    ALTER COLUMN reorder_level TYPE NUMERIC,
    ALTER COLUMN reorder_qty TYPE NUMERIC,
    ALTER COLUMN min_stock TYPE NUMERIC,
    ALTER COLUMN max_stock TYPE NUMERIC;

-- ── 10. inv_stock_adjustment ───────────────────────────────────────────────
ALTER TABLE inv_stock_adjustment
    ALTER COLUMN total_in_value TYPE NUMERIC,
    ALTER COLUMN total_out_value TYPE NUMERIC;

-- ── 11. inv_stock_adjustment_dtl ───────────────────────────────────────────
ALTER TABLE inv_stock_adjustment_dtl
    ALTER COLUMN qty TYPE NUMERIC,
    ALTER COLUMN qty_base TYPE NUMERIC,
    ALTER COLUMN unit_cost TYPE NUMERIC,
    ALTER COLUMN line_value TYPE NUMERIC,
    ALTER COLUMN system_qty TYPE NUMERIC;

-- ── 12. inv_stock_transfer ─────────────────────────────────────────────────
ALTER TABLE inv_stock_transfer
    ALTER COLUMN total_qty TYPE NUMERIC,
    ALTER COLUMN total_value TYPE NUMERIC;

-- ── 13. inv_stock_transfer_dtl ─────────────────────────────────────────────
ALTER TABLE inv_stock_transfer_dtl
    ALTER COLUMN qty_sent TYPE NUMERIC,
    ALTER COLUMN qty_sent_base TYPE NUMERIC,
    ALTER COLUMN qty_received TYPE NUMERIC,
    ALTER COLUMN qty_received_base TYPE NUMERIC,
    ALTER COLUMN unit_cost TYPE NUMERIC,
    ALTER COLUMN line_value TYPE NUMERIC;

-- ── 14. inv_physical_count_dtl (Convert generated variance_qty to plain NUMERIC) ─
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'inv_physical_count_dtl') THEN
        -- Drop generated column so base columns can be altered and database runs no math
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'inv_physical_count_dtl' AND column_name = 'variance_qty') THEN
            ALTER TABLE inv_physical_count_dtl DROP COLUMN variance_qty;
        END IF;

        ALTER TABLE inv_physical_count_dtl
            ALTER COLUMN system_qty TYPE NUMERIC,
            ALTER COLUMN counted_qty TYPE NUMERIC,
            ALTER COLUMN unit_cost TYPE NUMERIC;

        -- Re-add variance_qty as a plain, regular NUMERIC column (no database-level math)
        ALTER TABLE inv_physical_count_dtl
            ADD COLUMN variance_qty NUMERIC DEFAULT 0;

        UPDATE inv_physical_count_dtl
            SET variance_qty = counted_qty - system_qty;
    END IF;
END $$;

-- ── 15. Catch-all: Convert any remaining numeric column in inv_% to NUMERIC ─
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN (
        SELECT table_name, column_name
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name LIKE 'inv_%'
          AND data_type = 'numeric'
          AND numeric_precision IS NOT NULL
          AND (is_generated IS NULL OR is_generated != 'ALWAYS')
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
  AND table_name LIKE 'inv_%'
  AND data_type = 'numeric'
ORDER BY table_name, ordinal_position;
*/
