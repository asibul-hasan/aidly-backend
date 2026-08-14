-- =====================================================================================
-- SAL — POS data model (Wave POS-1)
--
-- Adds the register / drawer-session / split-tender model that turns the existing
-- sal_invoice document into a real point-of-sale sale:
--
--   sal_pos_terminal      the physical register
--   sal_pos_session       one cashier's drawer period on a terminal
--   sal_invoice_payment   split tender — how a sale was paid, not just how much
--
-- plus the sal_invoice / sal_invoice_dtl columns POS needs (idempotency key, session
-- binding, time of day, bill-level discount).
--
-- Held carts are deliberately NOT modelled here: the POS screen parks them in
-- localStorage, so there is no hold document and no stock reservation.
--
-- Idempotent — safe to re-run. Pure DDL/DML only.
-- =====================================================================================

BEGIN;

-- ─────────────────────────────────────────────────────────────────────────────────────
-- 1. sal_pos_terminal — the register
-- ─────────────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS sal_pos_terminal (
    terminal_no        BIGSERIAL PRIMARY KEY,
    company_no         BIGINT       NOT NULL,
    branch_no          BIGINT       NOT NULL,
    warehouse_no       BIGINT       NOT NULL,               -- stock this till sells from
    terminal_id        VARCHAR(30)  NOT NULL,
    terminal_name      VARCHAR(100) NOT NULL,
    device_uuid        VARCHAR(80),                         -- bound device, for offline sync later
    receipt_prefix     VARCHAR(20),
    cash_gl_account_no BIGINT,                              -- drawer cash account (fin_account)
    remarks            TEXT,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT   NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT,
    updated_at  TIMESTAMPTZ,
    deleted_by  BIGINT,
    deleted_at  TIMESTAMPTZ,
    row_version BIGINT   NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_term_active  CHECK (is_active  IN (0, 1)),
    CONSTRAINT chk_sal_term_deleted CHECK (is_deleted IN (0, 1))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_sal_term_id
    ON sal_pos_terminal (branch_no, terminal_id) WHERE is_deleted = 0;

CREATE INDEX IF NOT EXISTS idx_sal_term_branch
    ON sal_pos_terminal (company_no, branch_no) WHERE is_deleted = 0;

-- ─────────────────────────────────────────────────────────────────────────────────────
-- 2. sal_pos_session — one cashier's drawer period
-- ─────────────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS sal_pos_session (
    session_no      BIGSERIAL PRIMARY KEY,
    company_no      BIGINT       NOT NULL,
    branch_no       BIGINT       NOT NULL,
    terminal_no     BIGINT       NOT NULL,
    session_id      VARCHAR(40)  NOT NULL,
    cashier_user_no BIGINT       NOT NULL,
    opened_at       TIMESTAMPTZ  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    opening_float   NUMERIC(20,4) NOT NULL DEFAULT 0,
    closed_at       TIMESTAMPTZ,
    -- expected (system) tender totals, computed at close from sal_invoice_payment
    expected_cash   NUMERIC(20,4),
    expected_card   NUMERIC(20,4),
    expected_mobile NUMERIC(20,4),
    expected_other  NUMERIC(20,4),
    counted_cash    NUMERIC(20,4),
    cash_variance   NUMERIC(20,4),                          -- counted - (float + expected_cash)
    total_sales     NUMERIC(20,4) NOT NULL DEFAULT 0,
    total_returns   NUMERIC(20,4) NOT NULL DEFAULT 0,
    invoice_count   INTEGER       NOT NULL DEFAULT 0,
    status          SMALLINT      NOT NULL DEFAULT 1,       -- 1=Open 2=Closing 3=Closed
    variance_approved_by BIGINT,
    variance_remarks TEXT,
    gl_voucher_no   BIGINT,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT   NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT,
    updated_at  TIMESTAMPTZ,
    deleted_by  BIGINT,
    deleted_at  TIMESTAMPTZ,
    row_version BIGINT   NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_sess_status  CHECK (status     IN (1, 2, 3)),
    CONSTRAINT chk_sal_sess_active  CHECK (is_active  IN (0, 1)),
    CONSTRAINT chk_sal_sess_deleted CHECK (is_deleted IN (0, 1))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_sal_sess_id
    ON sal_pos_session (branch_no, session_id) WHERE is_deleted = 0;

-- The database, not the service, is what guarantees one open drawer per register.
CREATE UNIQUE INDEX IF NOT EXISTS uq_sal_sess_open
    ON sal_pos_session (terminal_no) WHERE status = 1 AND is_deleted = 0;

CREATE INDEX IF NOT EXISTS idx_sal_sess_cashier
    ON sal_pos_session (cashier_user_no, opened_at);

-- ─────────────────────────────────────────────────────────────────────────────────────
-- 3. sal_invoice_payment — split tender
-- ─────────────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS sal_invoice_payment (
    invoice_payment_no BIGSERIAL PRIMARY KEY,
    invoice_no      BIGINT       NOT NULL REFERENCES sal_invoice (invoice_no) ON DELETE CASCADE,
    line_no         INTEGER      NOT NULL,
    -- 1=Cash 2=Card 3=MobileBanking 4=BankTransfer 5=Cheque 6=Credit/Due
    -- 7=LoyaltyPoints 8=GiftCard 9=StoreCredit
    payment_method  SMALLINT     NOT NULL,
    amount          NUMERIC(20,4) NOT NULL,
    tendered_amount NUMERIC(20,4),                          -- cash handed over, for change
    card_last4      VARCHAR(4),
    card_type       VARCHAR(20),
    approval_code   VARCHAR(40),
    mobile_provider VARCHAR(30),
    txn_ref         VARCHAR(80),
    bank_no         BIGINT,
    cheque_no       VARCHAR(40),
    cheque_date     DATE,
    points_redeemed NUMERIC(18,4),
    gl_account_no   BIGINT,                                 -- resolved cash/bank/card-clearing account
    remarks         VARCHAR(250),
    row_version     BIGINT       NOT NULL DEFAULT 1,
    CONSTRAINT uq_sal_invpay_line    UNIQUE (invoice_no, line_no),
    CONSTRAINT chk_sal_invpay_method CHECK (payment_method IN (1,2,3,4,5,6,7,8,9)),
    CONSTRAINT chk_sal_invpay_amt    CHECK (amount <> 0)
);

CREATE INDEX IF NOT EXISTS idx_sal_invpay_hdr    ON sal_invoice_payment (invoice_no);
CREATE INDEX IF NOT EXISTS idx_sal_invpay_method ON sal_invoice_payment (payment_method);

-- ─────────────────────────────────────────────────────────────────────────────────────
-- 4. sal_invoice — POS columns
-- ─────────────────────────────────────────────────────────────────────────────────────
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS client_uuid    UUID;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS invoice_time   TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS terminal_no    BIGINT;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS pos_session_no BIGINT;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS bill_discount_type   SMALLINT      NOT NULL DEFAULT 1;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS bill_discount_value  NUMERIC(20,4) NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS bill_discount_amount NUMERIC(20,4) NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS promotion_discount   NUMERIC(20,4) NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS salesperson_employee_no BIGINT;

-- Existing rows need a key before the unique index can be created.
UPDATE sal_invoice SET client_uuid = gen_random_uuid() WHERE client_uuid IS NULL;
ALTER TABLE sal_invoice ALTER COLUMN client_uuid SET NOT NULL;

-- Deliberately NOT filtered on is_deleted: a replayed request must collide with the
-- original even after that sale was voided, or the replay creates a second sale.
CREATE UNIQUE INDEX IF NOT EXISTS uq_sal_inv_uuid ON sal_invoice (client_uuid);

CREATE INDEX IF NOT EXISTS idx_sal_inv_session ON sal_invoice (pos_session_no);

-- ─────────────────────────────────────────────────────────────────────────────────────
-- 5. sal_invoice_dtl — POS line columns
-- ─────────────────────────────────────────────────────────────────────────────────────
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS mrp                 NUMERIC(20,4);
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS line_discount_type  SMALLINT      NOT NULL DEFAULT 1;
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS line_discount_value NUMERIC(20,4) NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS is_tax_inclusive    SMALLINT      NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS promotion_no        BIGINT;
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS promotion_discount  NUMERIC(20,4) NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS is_free_item        SMALLINT      NOT NULL DEFAULT 0;

-- Document numbering needs no seed here: DocSequenceGenerator creates the SAL_SESSION
-- row on first use.

COMMIT;
