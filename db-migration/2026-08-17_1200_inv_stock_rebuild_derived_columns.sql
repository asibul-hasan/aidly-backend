-- ═══════════════════════════════════════════════════════════════════════════
-- INV — rebuild the derived columns on inv_stock
-- Generated: 2026-08-17   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   InvEngine maintained qty_on_hand and avg_cost on every movement but never
--   qty_available or stock_value. Those two were written when the row was
--   first created and never again, so they fossilised:
--
--     stock_no 1: on_hand 286, reserved 5 -> available should be 281, was 95
--                 stock_value 80,000 against qty x avg_cost of 168,050
--     stock_no 2: on_hand  47, reserved 2 -> available should be  45, was 48
--                 -- MORE AVAILABLE THAN ON HAND, i.e. sellable stock that
--                 does not exist
--
--   The engine now recomputes both after every movement. This backfills the
--   rows that were already wrong.
--
-- WHY THIS IS SAFE TO REWRITE
--   Unlike fin_ledger or inv_stock_ledger, these two columns are a CACHE:
--   both are pure functions of qty_on_hand, qty_reserved and avg_cost, which
--   the engine does maintain. Recomputing them destroys no history — it makes
--   them agree with the history that is already there. No ledger row is
--   touched.
--
-- SAFETY
--   Idempotent (recompute is deterministic). Pure DML.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

UPDATE inv_stock
SET qty_available = qty_on_hand - COALESCE(qty_reserved, 0),
    stock_value   = ROUND(qty_on_hand * COALESCE(avg_cost, 0), 4)
WHERE qty_available IS DISTINCT FROM (qty_on_hand - COALESCE(qty_reserved, 0))
   OR stock_value   IS DISTINCT FROM ROUND(qty_on_hand * COALESCE(avg_cost, 0), 4);

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════
-- Both invariants hold for every row (expect 0):
--
-- SELECT count(*) AS broken_rows
--   FROM inv_stock
--  WHERE qty_available IS DISTINCT FROM (qty_on_hand - COALESCE(qty_reserved,0))
--     OR stock_value   IS DISTINCT FROM ROUND(qty_on_hand * COALESCE(avg_cost,0), 4);
--
-- And nothing is sellable that is not on hand (expect 0):
--
-- SELECT count(*) FROM inv_stock WHERE qty_available > qty_on_hand;
