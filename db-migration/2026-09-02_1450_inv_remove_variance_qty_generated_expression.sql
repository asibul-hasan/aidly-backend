-- ============================================================================
-- Migration: 2026-09-02_1450_inv_remove_variance_qty_generated_expression.sql
-- Table: inv_physical_count_dtl
-- Column: variance_qty
-- Purpose: Remove the database-level generated expression (counted_qty - system_qty)
--          and convert variance_qty into a standard, regular unconstrained NUMERIC column.
--          This eliminates all mathematical operations from the database engine,
--          allowing values to be saved directly from the application/frontend.
-- ============================================================================

BEGIN;

DO $$
BEGIN
    -- Try PostgreSQL 13+ native DROP EXPRESSION (preserves existing data)
    BEGIN
        ALTER TABLE inv_physical_count_dtl ALTER COLUMN variance_qty DROP EXPRESSION;
    EXCEPTION WHEN OTHERS THEN
        -- Fallback for PostgreSQL 12 or earlier: swap via temporary column
        IF EXISTS (
            SELECT 1 
            FROM information_schema.columns 
            WHERE table_name = 'inv_physical_count_dtl' 
              AND column_name = 'variance_qty' 
              AND is_generated = 'ALWAYS'
        ) THEN
            ALTER TABLE inv_physical_count_dtl ADD COLUMN variance_qty_regular NUMERIC DEFAULT 0;
            UPDATE inv_physical_count_dtl SET variance_qty_regular = COALESCE(counted_qty - system_qty, 0);
            ALTER TABLE inv_physical_count_dtl DROP COLUMN variance_qty;
            ALTER TABLE inv_physical_count_dtl RENAME COLUMN variance_qty_regular TO variance_qty;
        END IF;
    END;
END $$;

-- Ensure the column is unconstrained NUMERIC with default 0
ALTER TABLE inv_physical_count_dtl 
    ALTER COLUMN variance_qty TYPE NUMERIC,
    ALTER COLUMN variance_qty SET DEFAULT 0;

COMMIT;

-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION QUERY
-- Running this should now return 0 rows across the entire database.
-- ═══════════════════════════════════════════════════════════════════════════
/*
SELECT 
    table_name, 
    column_name, 
    generation_expression, 
    is_generated
FROM information_schema.columns
WHERE table_schema = 'public'
  AND is_generated = 'ALWAYS';
*/
