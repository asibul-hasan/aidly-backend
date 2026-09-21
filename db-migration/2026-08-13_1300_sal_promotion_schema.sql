-- ═══════════════════════════════════════════════════════════════════════════
-- SAL — promotions and customer groups (Wave POS-5)
-- Generated: 2026-08-13   |   run in pgAdmin
--
-- WHAT THIS DOES
--   sal_customer_group   pricing tier / default terms a customer inherits
--   sal_promotion        the rule: what discount, to whom, when, how often
--   sal_promotion_dtl    the targets: which products/categories/brands, and for
--                        BuyXGetY which items are the "buy" and which the "get"
--
-- ⚠ PROMO TYPES THE ENGINE HANDLES TODAY
--   1 LinePct · 2 LineAmount · 3 BillPct · 4 BillAmount · 5 BuyXGetY
--
--   Types 6 QtyBreak, 7 Coupon and 8 Bundle are accepted by the schema but the
--   pricing engine SKIPS them — deliberately, so a half-implemented rule can
--   never silently under- or over-discount a real sale. Creating one is allowed
--   (the setup form warns); it simply will not fire until the engine learns it.
--
-- SAFETY
--   Idempotent. Pure DDL. Wrapped in BEGIN/COMMIT.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. sal_customer_group ────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS sal_customer_group (
    customer_group_no BIGSERIAL PRIMARY KEY,
    company_no        BIGINT       NOT NULL,
    group_id          VARCHAR(30)  NOT NULL,
    group_name        VARCHAR(150) NOT NULL,
    price_tier        SMALLINT     NOT NULL DEFAULT 1,   -- 1=Retail 2=Wholesale 3=Special
    default_credit_days INTEGER    NOT NULL DEFAULT 0,
    default_discount_pct NUMERIC(5,2) NOT NULL DEFAULT 0,
    remarks           TEXT,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT   NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT,
    updated_at  TIMESTAMPTZ,
    deleted_by  BIGINT,
    deleted_at  TIMESTAMPTZ,
    row_version BIGINT   NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_custgrp_active  CHECK (is_active  IN (0, 1)),
    CONSTRAINT chk_sal_custgrp_deleted CHECK (is_deleted IN (0, 1))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_sal_custgrp_id
    ON sal_customer_group (company_no, group_id) WHERE is_deleted = 0;


-- ── 2. sal_promotion ─────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS sal_promotion (
    promotion_no   BIGSERIAL PRIMARY KEY,
    company_no     BIGINT       NOT NULL,
    branch_no      BIGINT,                              -- NULL = every branch
    promotion_id   VARCHAR(30)  NOT NULL,
    promotion_name VARCHAR(150) NOT NULL,
    -- 1=LinePct 2=LineAmount 3=BillPct 4=BillAmount 5=BuyXGetY 6=QtyBreak 7=Coupon 8=Bundle
    promo_type     SMALLINT     NOT NULL,
    -- 1=Product 2=Category 3=Brand 4=All 5=Customer/Group
    scope_type     SMALLINT     NOT NULL DEFAULT 1,
    coupon_code    VARCHAR(40),
    /* Higher runs first. A non-stackable promo that lands on a line stops any
       further promo touching that line, so order decides the outcome. */
    priority       SMALLINT     NOT NULL DEFAULT 0,
    is_stackable   SMALLINT     NOT NULL DEFAULT 0,
    -- conditions
    min_qty        NUMERIC(18,4),
    min_amount     NUMERIC(20,4),
    discount_pct   NUMERIC(5,2),
    discount_amount NUMERIC(20,4),
    buy_qty        NUMERIC(18,4),
    get_qty        NUMERIC(18,4),
    max_discount_amount NUMERIC(20,4),
    customer_type  SMALLINT,
    price_tier     SMALLINT,
    start_date     DATE         NOT NULL,
    end_date       DATE         NOT NULL,
    start_time     TIME,                                -- happy-hour window
    end_time       TIME,
    weekday_mask   SMALLINT,                            -- bit0=Mon … bit6=Sun; NULL = every day
    usage_limit    INTEGER,
    used_count     INTEGER      NOT NULL DEFAULT 0,
    remarks        TEXT,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT   NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT,
    updated_at  TIMESTAMPTZ,
    deleted_by  BIGINT,
    deleted_at  TIMESTAMPTZ,
    row_version BIGINT   NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_promo_type    CHECK (promo_type IN (1,2,3,4,5,6,7,8)),
    CONSTRAINT chk_sal_promo_scope   CHECK (scope_type IN (1,2,3,4,5)),
    CONSTRAINT chk_sal_promo_dates   CHECK (end_date >= start_date),
    CONSTRAINT chk_sal_promo_active  CHECK (is_active  IN (0, 1)),
    CONSTRAINT chk_sal_promo_deleted CHECK (is_deleted IN (0, 1))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_sal_promo_id
    ON sal_promotion (company_no, promotion_id) WHERE is_deleted = 0;

-- The till reads this on every re-price, so the live window is the index.
CREATE INDEX IF NOT EXISTS idx_sal_promo_active_window
    ON sal_promotion (company_no, start_date, end_date)
    WHERE is_active = 1 AND is_deleted = 0;


-- ── 3. sal_promotion_dtl ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS sal_promotion_dtl (
    promotion_dtl_no BIGSERIAL PRIMARY KEY,
    promotion_no BIGINT   NOT NULL REFERENCES sal_promotion (promotion_no) ON DELETE CASCADE,
    /* 1=Condition — the items that must be bought.
       2=Reward    — the items given, for BuyXGetY. */
    target_role  SMALLINT NOT NULL DEFAULT 1,
    product_no   BIGINT,
    variant_no   BIGINT,
    category_no  BIGINT,
    brand_no     BIGINT,
    qty          NUMERIC(18,4),
    row_version  BIGINT   NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_promodtl_role CHECK (target_role IN (1, 2))
);

CREATE INDEX IF NOT EXISTS idx_sal_promodtl_hdr ON sal_promotion_dtl (promotion_no);


-- ── 4. Link customers to their group ─────────────────────────────────────
-- sal_customer.customer_group_no already exists; nothing to add.

COMMIT;
