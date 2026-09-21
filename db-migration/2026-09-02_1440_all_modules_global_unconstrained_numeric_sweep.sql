-- ============================================================================
-- Migration: 2026-09-02_1440_all_modules_global_unconstrained_numeric_sweep.sql
-- Module: GLOBAL (All ERP Modules: FIN, SYS, HRM, PUR, SAL, INV, Shared)
-- Purpose: Global audit & sweep to ensure ZERO columns across the entire database
--          schema have fixed precision or fraction limits (e.g. NUMERIC(20,4),
--          NUMERIC(18,2), NUMERIC(18,4), NUMERIC(38,2), NUMERIC(5,2)).
--          Converts any remaining column of data_type 'numeric' to unconstrained
--          NUMERIC so PostgreSQL stores arbitrary decimal fractions as given,
--          without database-level truncation.
-- ============================================================================

BEGIN;

-- 1. Convert any generated column in inv_physical_count_dtl to plain NUMERIC
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'inv_physical_count_dtl') THEN
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

-- Global catch-all loop: Alters every numeric column across the entire public schema
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN (
        SELECT table_name, column_name
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND data_type = 'numeric'
          AND numeric_precision IS NOT NULL
          AND (is_generated IS NULL OR is_generated != 'ALWAYS')
    ) LOOP
        EXECUTE format('ALTER TABLE %I ALTER COLUMN %I TYPE NUMERIC;', r.table_name, r.column_name);
    END LOOP;
END $$;

COMMIT;

-- ═══════════════════════════════════════════════════════════════════════════
-- DATABASE-WIDE VERIFICATION QUERY
-- Expected result: 0 rows (No column in any table has precision/scale restrictions)
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
  AND data_type = 'numeric'
  AND numeric_precision IS NOT NULL
ORDER BY table_name, ordinal_position;
*/
