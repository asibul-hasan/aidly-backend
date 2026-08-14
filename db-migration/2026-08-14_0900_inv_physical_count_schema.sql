-- ═══════════════════════════════════════════════════════════════════════════
-- INV — physical count / stocktake (INV_2006, Wave POS-6)
-- Generated: 2026-08-14   |   run in pgAdmin
--
-- WHAT THIS DOES
--   inv_physical_count      the count sheet: which warehouse, when, what state
--   inv_physical_count_dtl  one row per stock cell: system qty vs counted qty
--
--   Posting a sheet does NOT write stock directly. It builds an
--   inv_stock_adjustment (adjustment_type 5 = Count) and puts that through the
--   posting engine, so a stocktake lands in the ledger by exactly the same path
--   as every other quantity change and is reversible the same way.
--
-- ⚠ variance_qty IS A GENERATED COLUMN
--   counted − system, computed by the database. It cannot drift from its inputs
--   because nothing is allowed to write it. EF maps it read-only.
--
-- SAFETY
--   Idempotent. Pure DDL.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

CREATE TABLE IF NOT EXISTS inv_physical_count (
    count_no      BIGSERIAL PRIMARY KEY,
    company_no    BIGINT      NOT NULL,
    branch_no     BIGINT      NOT NULL,
    count_id      VARCHAR(40) NOT NULL,
    count_date    DATE        NOT NULL,
    warehouse_no  BIGINT      NOT NULL,
    count_type    SMALLINT    NOT NULL DEFAULT 1,   -- 1=Full 2=Cycle 3=Spot
    status        SMALLINT    NOT NULL DEFAULT 1,   -- 1=Draft 2=Counting 3=Review 4=Posted 5=Cancelled
    /* Advisory flag for the operator: movements on counted cells should be held
       while the sheet is open. Not enforced by the engine. */
    freeze_stock  SMALLINT    NOT NULL DEFAULT 0,
    variance_adjustment_no BIGINT,                  -- the adjustment posting created
    remarks       TEXT,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT   NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT,
    updated_at  TIMESTAMPTZ,
    deleted_by  BIGINT,
    deleted_at  TIMESTAMPTZ,
    row_version BIGINT   NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_count_status  CHECK (status IN (1, 2, 3, 4, 5)),
    CONSTRAINT chk_inv_count_type    CHECK (count_type IN (1, 2, 3)),
    CONSTRAINT chk_inv_count_active  CHECK (is_active  IN (0, 1)),
    CONSTRAINT chk_inv_count_deleted CHECK (is_deleted IN (0, 1))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_inv_count_id
    ON inv_physical_count (branch_no, count_id) WHERE is_deleted = 0;

CREATE INDEX IF NOT EXISTS idx_inv_count_warehouse
    ON inv_physical_count (company_no, warehouse_no, count_date) WHERE is_deleted = 0;

CREATE TABLE IF NOT EXISTS inv_physical_count_dtl (
    count_dtl_no BIGSERIAL PRIMARY KEY,
    count_no     BIGINT        NOT NULL REFERENCES inv_physical_count (count_no) ON DELETE CASCADE,
    line_no      INTEGER       NOT NULL,
    product_no   BIGINT        NOT NULL,
    variant_no   BIGINT,
    batch_no     BIGINT,
    /* Snapshot taken when the sheet was opened — what the system believed then.
       Kept as-is so the variance reflects the count, not later movements. */
    system_qty   NUMERIC(18,4) NOT NULL DEFAULT 0,
    counted_qty  NUMERIC(18,4) NOT NULL DEFAULT 0,
    variance_qty NUMERIC(18,4) GENERATED ALWAYS AS (counted_qty - system_qty) STORED,
    unit_cost    NUMERIC(20,6) NOT NULL DEFAULT 0,
    remarks      VARCHAR(250),
    row_version  BIGINT        NOT NULL DEFAULT 1,
    CONSTRAINT uq_inv_count_line UNIQUE (count_no, line_no)
);

CREATE INDEX IF NOT EXISTS idx_inv_countdtl_hdr ON inv_physical_count_dtl (count_no);

COMMIT;
