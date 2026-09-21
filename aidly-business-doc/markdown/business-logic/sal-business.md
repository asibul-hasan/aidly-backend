# SAL Business

<!-- Consolidated from the previous aidly-business-doc/backend, frontend, memory, and planning files. -->

SAL manages customers, sales/POS invoices, customer receipts, returns, AR, and sales-side accounting. It calls INV for stock OUT/costing and emits FIN outbox events for revenue, VAT, AR/cash, COGS, and inventory.

## Sales Database and Backend Blueprint

# SAL â€” Sales Management Module (Production Blueprint)

> **Module prefix:** `sal_` Â· **Backend package:** `com.infoaidtech.aidly.sal` Â· **Frontend feature:** `apps/web-client/src/app/features/sal`
> **Forms covered:** SAL_1001 POS Sales Screen Â· SAL_1002 Draft/Hold Sales Â· SAL_1003 POS Closing Â· SAL_1101 Customer Management Â· SAL_1102 Customer Due Collection Â· (+ SAL_1103 Sales Return, SAL_1104 Promotions/Discount Setup â€” added to satisfy mandatory return & promotion workflows)
> **Depends on:** `inv-db.md` (the stock-posting engine `InvStockPostingService`, `inv_stock`, `inv_product`, `inv_batch`, `inv_warehouse`, `sys_doc_sequence`, `sys_event_outbox`). **Aligns with** the same conventions table reproduced in `inv-db.md` (PK `_no`, business `_id` partial-unique per branch, SMALLINT booleans/enums, audit block, soft delete, `row_version`, `ApiResponse`, form-wise API, Angular signals).
> Sales **never** writes `inv_stock` directly â€” it calls the inventory posting engine. Sales **owns** the customer, the AR/due ledger, POS sessions, and the sell-side GL postings.

---

## Conventions
Identical to `inv-db.md` â†’ "Conventions inherited from the existing codebase". The **standard audit block** (`is_active, is_deleted, created_by/at, updated_by/at, deleted_by/at, row_version` + the two CHECKs) is abbreviated below as `-- << AUDIT BLOCK >>`. Money `NUMERIC(20,4)`, qty `NUMERIC(18,4)`, cost `NUMERIC(20,6)`, pct `NUMERIC(5,2)`.

---

# 1. BUSINESS MODULE OVERVIEW

## 1.1 What this module does
Sales converts available inventory into revenue and receivables. It runs two sell motions on **one** document model (`sal_invoice`): **fast POS** (walk-in, immediate multi-tender payment, receipt print, drawer/session control) and **credit/B2B invoicing** (named customer, due date, partial payments). It owns customers, their credit limits and **outstanding dues**, returns/refunds, promotions/discounts, loyalty, and posts the sell-side accounting (revenue, output VAT, COGS/inventory relief, AR or cash).

## 1.2 Real retail workflow (lifecycle)
1. **Cart build** â€” scan/search items â†’ lines with qty, price (resolved from tier/promo), line discount, VAT.
2. **Customer** â€” walk-in (anonymous) or selected customer; credit customers get due-date + credit-limit check.
3. **Discounts/Promotions** â€” line and bill-level discounts; auto-applied promotions (buy-X-get-Y, category %, coupon).
4. **Tender** â€” POS: split payment (cash/card/mobile/bank/points/credit) until paidâ‰¥total; round-off; change due. Credit: paid 0..total, remainder becomes due.
5. **Confirm/Post** â€” stock relieved (reserveâ†’consume) via inventory engine; AR/cash + revenue + VAT posted to GL; receipt/invoice printed.
6. **Hold/Draft (SAL_1002)** â€” park an in-progress cart (reserves stock), resume later, or void.
7. **Return (SAL_1103)** â€” against an invoice (or blind, with permission): restock + refund/credit-note; reverses revenue/VAT/COGS proportionally.
8. **Due collection (SAL_1102)** â€” receipts against customer outstanding, allocated FIFO or to chosen invoices.
9. **POS closing (SAL_1003)** â€” cashier counts drawer; expected vs counted variance recorded; session closed; Z-report.

## 1.3 Actors
Cashier (create/tender/print), Sales Executive (credit invoices, quotations), Customer-Service (returns, due collection), Branch/Sales Manager (approve over-limit sale, blind return, void/cancel, discount over cap, session variance sign-off), Accountant (AR reconciliation), Admin (terminals, promotion setup, price tiers).

## 1.4 Approval flows
- **Invoice:** `Draft/Hold â†’ Confirmed(Posted) â†’ (Returned/PartiallyReturned) / (Cancelledâ†’reversed)`. Over-credit-limit or below-`min_sale_price` or discount above cap â†’ requires `can_approve` (manager override, audited).
- **Return:** `Draft â†’ Approved(Posted) â†’ (Cancelled)`. Blind return (no original invoice) always needs `can_approve`.
- **Receipt (due collection):** `Draft â†’ Posted â†’ (Cancelled/reversed)`.
- **POS session:** `Open â†’ (sales) â†’ Closing(counted) â†’ Closed`. Variance beyond tolerance needs manager sign-off.

## 1.5 Edge cases
Last-unit oversell (engine pessimistic lock + branch negative policy); held cart holding stock that another sale needs (reservation expiry job auto-releases); price/promo changing between hold and resume (re-price on resume, warn); partial payment then customer leaves (credit/due); return of a discounted/promo line (refund the *net* paid, not list); return of batch/serial item (must restock same batch/serial); rounding (store round_off explicitly so GL ties out); refund tender â‰  original tender (cash refund of a card sale needs approval); offline POS double-submit (idempotency via `client_uuid`); negative net invoice (exchange where return value > new sale); tax-inclusive vs exclusive pricing; multi-currency customer (convert at posting).

## 1.6 Operational constraints
- Base currency for GL; FX sales convert at `sys_currency.exchange_rate` on post.
- A POS sale must belong to an **open** `sal_pos_session`; no open session â†’ block POS tender (config) or auto-open.
- Period must be open (`sys_fin_year_dtl`).
- Walk-in customer is a per-branch system customer (`customer_type=1`), never accrues due.

## 1.7 SME examples
Grocery POS with cash drawer + bKash/Nagad mobile tenders and daily Z-closing; electronics shop with serial-tracked credit sales and EMI-style partial dues; pharmacy with batch/expiry FEFO at sale and frequent partial returns; fashion outlet running "Buy 2 Get 1" and category markdown promotions.

---

# 2. DATABASE DESIGN

## 2.1 `sal_customer` â€” customer master (SAL_1101)
**Purpose:** buyer master with credit terms, running due, tier, tax & loyalty. **Scope:** company-wide (shared across branches); `branch_no` = registration branch.
```sql
CREATE TABLE sal_customer (
    customer_no      BIGSERIAL PRIMARY KEY,
    company_no       BIGINT      NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no        BIGINT      NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    customer_id      VARCHAR(30) NOT NULL,
    customer_name    VARCHAR(200) NOT NULL,
    customer_name_nls VARCHAR(200),
    customer_type    SMALLINT    NOT NULL DEFAULT 2,  -- 1=WalkIn,2=Retail,3=Wholesale,4=Corporate
    price_tier       SMALLINT    NOT NULL DEFAULT 1,  -- 1=Retail,2=Wholesale,3=Corporate (â†’ inv_product_price.price_tier)
    customer_group_no BIGINT     REFERENCES sal_customer_group(customer_group_no) ON DELETE SET NULL,
    mobile_no        VARCHAR(20),
    alt_mobile_no    VARCHAR(20),
    email            VARCHAR(150),
    address_line1    VARCHAR(250), address_line2 VARCHAR(250),
    city             VARCHAR(100), post_code VARCHAR(20), country_code VARCHAR(10),
    vat_reg_no       VARCHAR(50), tin_no VARCHAR(50),
    -- credit control
    is_credit_allowed SMALLINT   NOT NULL DEFAULT 0,
    credit_limit     NUMERIC(20,4) NOT NULL DEFAULT 0,
    credit_days      INTEGER     NOT NULL DEFAULT 0,
    opening_balance  NUMERIC(20,4) NOT NULL DEFAULT 0,  -- + = customer owes us
    opening_balance_date DATE,
    current_due      NUMERIC(20,4) NOT NULL DEFAULT 0,  -- maintained by AR ledger postings
    -- loyalty
    loyalty_points   NUMERIC(18,4) NOT NULL DEFAULT 0,
    default_currency_no BIGINT    REFERENCES sys_currency(currency_no) ON DELETE SET NULL,
    default_warehouse_no BIGINT   REFERENCES inv_warehouse(warehouse_no) ON DELETE SET NULL,
    image_path       VARCHAR(255),
    remarks          TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_cust_type   CHECK (customer_type IN (1,2,3,4)),
    CONSTRAINT chk_sal_cust_tier   CHECK (price_tier IN (1,2,3)),
    CONSTRAINT chk_sal_cust_credit CHECK (credit_limit >= 0 AND credit_days >= 0),
    CONSTRAINT chk_sal_cust_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sal_cust_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sal_cust_id     ON sal_customer(company_no, customer_id) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_sal_cust_mobile ON sal_customer(company_no, mobile_no)   WHERE is_deleted = 0 AND mobile_no IS NOT NULL;
CREATE INDEX idx_sal_cust_name_trgm ON sal_customer USING gin (customer_name gin_trgm_ops);
CREATE INDEX idx_sal_cust_branch    ON sal_customer(branch_no) WHERE is_deleted = 0;
CREATE INDEX idx_sal_cust_due       ON sal_customer(company_no) WHERE current_due > 0 AND is_deleted = 0;
```
**Rules:** `current_due` is **derived state** maintained only by AR ledger posts (never edited by the form). Walk-in is a seeded `customer_type=1`, `is_credit_allowed=0`. Deleting requires zero due and no invoices (RESTRICT) â†’ discontinue instead. `nullifyBusinessId()` not needed (partial unique).

## 2.2 `sal_customer_group` â€” grouping / default terms (optional)
```sql
CREATE TABLE sal_customer_group (
    customer_group_no BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    group_id     VARCHAR(30) NOT NULL,
    group_name   VARCHAR(150) NOT NULL,
    default_price_tier SMALLINT NOT NULL DEFAULT 1,
    default_credit_days INTEGER NOT NULL DEFAULT 0,
    discount_pct NUMERIC(5,2) NOT NULL DEFAULT 0,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_cgrp_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sal_cgrp_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sal_cgrp_id ON sal_customer_group(company_no, group_id) WHERE is_deleted = 0;
```

## 2.3 `sal_pos_terminal` â€” register / POS device
```sql
CREATE TABLE sal_pos_terminal (
    terminal_no   BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no     BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    warehouse_no  BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,  -- stock source
    terminal_id   VARCHAR(30) NOT NULL,
    terminal_name VARCHAR(100) NOT NULL,
    device_uuid   VARCHAR(80),                 -- bound device id for offline sync
    receipt_prefix VARCHAR(20),
    cash_gl_account_no BIGINT,                 -- drawer cash account (fin_account)
    remarks       TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_term_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sal_term_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sal_term_id ON sal_pos_terminal(branch_no, terminal_id) WHERE is_deleted = 0;
```

## 2.4 `sal_pos_session` â€” cashier shift / drawer (SAL_1003)
**Purpose:** one cashier's drawer period on a terminal; bounds sales for closing & cash reconciliation.
```sql
CREATE TABLE sal_pos_session (
    session_no    BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no     BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    terminal_no   BIGINT NOT NULL REFERENCES sal_pos_terminal(terminal_no) ON DELETE RESTRICT,
    session_id    VARCHAR(40) NOT NULL,
    cashier_user_no BIGINT NOT NULL REFERENCES sys_user(user_no) ON DELETE RESTRICT,
    opened_at     TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    opening_float NUMERIC(20,4) NOT NULL DEFAULT 0,
    closed_at     TIMESTAMPTZ,
    -- expected (system) tender totals at close
    expected_cash   NUMERIC(20,4),
    expected_card   NUMERIC(20,4),
    expected_mobile NUMERIC(20,4),
    expected_other  NUMERIC(20,4),
    counted_cash    NUMERIC(20,4),
    cash_variance   NUMERIC(20,4),     -- counted - (opening_float + expected_cash)
    total_sales     NUMERIC(20,4) NOT NULL DEFAULT 0,
    total_returns   NUMERIC(20,4) NOT NULL DEFAULT 0,
    invoice_count   INTEGER NOT NULL DEFAULT 0,
    status        SMALLINT NOT NULL DEFAULT 1,  -- 1=Open,2=Closing,3=Closed
    variance_approved_by BIGINT, variance_remarks TEXT,
    gl_voucher_no BIGINT,             -- cash deposit/short-over voucher on close
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_sess_status CHECK (status IN (1,2,3)),
    CONSTRAINT chk_sal_sess_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sal_sess_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sal_sess_id   ON sal_pos_session(branch_no, session_id) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_sal_sess_open ON sal_pos_session(terminal_no) WHERE status = 1 AND is_deleted = 0;  -- one open session per terminal
CREATE INDEX idx_sal_sess_cashier ON sal_pos_session(cashier_user_no, opened_at);
```

## 2.5 `sal_invoice` â€” sales document header (SAL_1001 / SAL_1002)
**Purpose:** the unified sale: POS or credit, draft/hold or confirmed. Status drives stock reservation vs. consumption.
```sql
CREATE TABLE sal_invoice (
    invoice_no     BIGSERIAL PRIMARY KEY,
    company_no     BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no      BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    invoice_id     VARCHAR(40) NOT NULL,            -- sys_doc_sequence SAL_INV (or receipt_prefix at POS)
    client_uuid    UUID NOT NULL DEFAULT uuid_generate_v4(),  -- offline/idempotency key
    invoice_date   DATE NOT NULL,
    invoice_time   TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    sale_type      SMALLINT NOT NULL DEFAULT 1,     -- 1=POS,2=CreditInvoice,3=Quotation
    customer_no    BIGINT NOT NULL REFERENCES sal_customer(customer_no) ON DELETE RESTRICT,
    warehouse_no   BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    terminal_no    BIGINT REFERENCES sal_pos_terminal(terminal_no) ON DELETE RESTRICT,
    pos_session_no BIGINT REFERENCES sal_pos_session(session_no) ON DELETE RESTRICT,
    salesperson_employee_no BIGINT REFERENCES hrm_employee(employee_no) ON DELETE SET NULL,
    currency_no    BIGINT REFERENCES sys_currency(currency_no) ON DELETE RESTRICT,
    exchange_rate  NUMERIC(15,6) NOT NULL DEFAULT 1,
    status         SMALLINT NOT NULL DEFAULT 1,     -- 1=Draft,2=Hold,3=Confirmed,4=PartiallyReturned,5=Returned,6=Cancelled
    -- money (document currency; *_base = Ã—exchange_rate)
    sub_total      NUMERIC(20,4) NOT NULL DEFAULT 0,   -- Î£ line gross before discount
    line_discount_total NUMERIC(20,4) NOT NULL DEFAULT 0,
    bill_discount_type  SMALLINT NOT NULL DEFAULT 1,   -- 1=Amount,2=Percent
    bill_discount_value NUMERIC(20,4) NOT NULL DEFAULT 0,
    bill_discount_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    promotion_discount  NUMERIC(20,4) NOT NULL DEFAULT 0,
    taxable_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    tax_amount     NUMERIC(20,4) NOT NULL DEFAULT 0,    -- output VAT
    shipping_charge NUMERIC(20,4) NOT NULL DEFAULT 0,
    round_off      NUMERIC(20,4) NOT NULL DEFAULT 0,
    grand_total    NUMERIC(20,4) NOT NULL DEFAULT 0,
    paid_amount    NUMERIC(20,4) NOT NULL DEFAULT 0,
    change_amount  NUMERIC(20,4) NOT NULL DEFAULT 0,    -- cash returned to customer
    due_amount     NUMERIC(20,4) NOT NULL DEFAULT 0,    -- grand_total - paid (credit portion)
    returned_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    total_cost     NUMERIC(20,4) NOT NULL DEFAULT 0,    -- Î£ COGS (for margin)
    payment_status SMALLINT NOT NULL DEFAULT 1,         -- 1=Unpaid,2=Partial,3=Paid
    due_date       DATE,
    fin_year_no    BIGINT NOT NULL REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no  BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    gl_voucher_no  BIGINT,
    posted_at      TIMESTAMPTZ,
    cancelled_by   BIGINT, cancelled_at TIMESTAMPTZ, cancel_reason VARCHAR(250),
    remarks        TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_inv_type    CHECK (sale_type IN (1,2,3)),
    CONSTRAINT chk_sal_inv_status  CHECK (status IN (1,2,3,4,5,6)),
    CONSTRAINT chk_sal_inv_paystat CHECK (payment_status IN (1,2,3)),
    CONSTRAINT chk_sal_inv_money   CHECK (grand_total >= 0 AND paid_amount >= 0),
    CONSTRAINT chk_sal_inv_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sal_inv_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sal_inv_id    ON sal_invoice(branch_no, invoice_id) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_sal_inv_uuid  ON sal_invoice(client_uuid);          -- hard idempotency (incl. soft-deleted)
CREATE INDEX idx_sal_inv_customer    ON sal_invoice(customer_no, invoice_date);
CREATE INDEX idx_sal_inv_status      ON sal_invoice(branch_no, status) WHERE is_deleted = 0;
CREATE INDEX idx_sal_inv_session     ON sal_invoice(pos_session_no);
CREATE INDEX idx_sal_inv_date        ON sal_invoice(branch_no, invoice_date);
CREATE INDEX idx_sal_inv_due         ON sal_invoice(customer_no) WHERE due_amount > 0 AND is_deleted = 0;
```

## 2.6 `sal_invoice_dtl` â€” sale lines
```sql
CREATE TABLE sal_invoice_dtl (
    invoice_dtl_no BIGSERIAL PRIMARY KEY,
    invoice_no   BIGINT NOT NULL REFERENCES sal_invoice(invoice_no) ON DELETE CASCADE,
    line_no      INTEGER NOT NULL,
    product_no   BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no   BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    batch_no     BIGINT REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,
    uom_no       BIGINT NOT NULL REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    qty          NUMERIC(18,4) NOT NULL,
    qty_base     NUMERIC(18,4) NOT NULL,            -- normalized for stock relief
    unit_price   NUMERIC(20,4) NOT NULL,            -- before line discount, document currency
    mrp          NUMERIC(20,4),
    line_discount_type SMALLINT NOT NULL DEFAULT 1, -- 1=Amount,2=Percent
    line_discount_value NUMERIC(20,4) NOT NULL DEFAULT 0,
    line_discount_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    promotion_no BIGINT REFERENCES sal_promotion(promotion_no) ON DELETE SET NULL,
    promotion_discount NUMERIC(20,4) NOT NULL DEFAULT 0,
    is_free_item SMALLINT NOT NULL DEFAULT 0,       -- promo giveaway line
    vat_tax_no   BIGINT REFERENCES sys_vat_tax(vat_tax_no) ON DELETE RESTRICT,
    tax_rate_pct NUMERIC(5,2) NOT NULL DEFAULT 0,
    is_tax_inclusive SMALLINT NOT NULL DEFAULT 0,
    taxable_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    tax_amount   NUMERIC(20,4) NOT NULL DEFAULT 0,
    line_total   NUMERIC(20,4) NOT NULL DEFAULT 0,  -- net incl tax
    unit_cost    NUMERIC(20,6) NOT NULL DEFAULT 0,  -- COGS unit (filled at post from engine)
    line_cost    NUMERIC(20,4) NOT NULL DEFAULT 0,
    returned_qty NUMERIC(18,4) NOT NULL DEFAULT 0,
    remarks      VARCHAR(250),
    row_version  BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_sal_inv_line UNIQUE (invoice_no, line_no),
    CONSTRAINT chk_sal_invdtl_qty CHECK (qty > 0),
    CONSTRAINT chk_sal_invdtl_disc CHECK (line_discount_type IN (1,2))
);
CREATE INDEX idx_sal_invdtl_hdr     ON sal_invoice_dtl(invoice_no);
CREATE INDEX idx_sal_invdtl_product ON sal_invoice_dtl(product_no, variant_no);
```

## 2.7 `sal_invoice_payment` â€” split tender / payments on the invoice
```sql
CREATE TABLE sal_invoice_payment (
    invoice_payment_no BIGSERIAL PRIMARY KEY,
    invoice_no   BIGINT NOT NULL REFERENCES sal_invoice(invoice_no) ON DELETE CASCADE,
    line_no      INTEGER NOT NULL,
    payment_method SMALLINT NOT NULL,   -- 1=Cash,2=Card,3=MobileBanking,4=BankTransfer,5=Cheque,6=Credit/Due,7=LoyaltyPoints,8=GiftCard,9=StoreCredit
    amount       NUMERIC(20,4) NOT NULL,
    tendered_amount NUMERIC(20,4),       -- cash given (for change calc)
    card_last4   VARCHAR(4), card_type VARCHAR(20), approval_code VARCHAR(40),
    mobile_provider VARCHAR(30), txn_ref VARCHAR(80),
    bank_no      BIGINT, cheque_no VARCHAR(40), cheque_date DATE,
    points_redeemed NUMERIC(18,4),
    gl_account_no BIGINT,                -- resolved cash/bank/card-clearing account
    remarks      VARCHAR(250),
    row_version  BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_sal_invpay_line UNIQUE (invoice_no, line_no),
    CONSTRAINT chk_sal_invpay_method CHECK (payment_method IN (1,2,3,4,5,6,7,8,9)),
    CONSTRAINT chk_sal_invpay_amt CHECK (amount <> 0)
);
CREATE INDEX idx_sal_invpay_hdr ON sal_invoice_payment(invoice_no);
CREATE INDEX idx_sal_invpay_method ON sal_invoice_payment(payment_method);
```

## 2.8 `sal_return` (+ `_dtl`) â€” sales return / refund (SAL_1103)
```sql
CREATE TABLE sal_return (
    return_no    BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no    BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    return_id    VARCHAR(40) NOT NULL,
    client_uuid  UUID NOT NULL DEFAULT uuid_generate_v4(),
    return_date  DATE NOT NULL,
    original_invoice_no BIGINT REFERENCES sal_invoice(invoice_no) ON DELETE RESTRICT,  -- NULL = blind return (needs approval)
    customer_no  BIGINT NOT NULL REFERENCES sal_customer(customer_no) ON DELETE RESTRICT,
    warehouse_no BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    pos_session_no BIGINT REFERENCES sal_pos_session(session_no) ON DELETE RESTRICT,
    return_reason SMALLINT,             -- 1=Defective,2=WrongItem,3=Expired,4=CustomerChange,5=Other
    status       SMALLINT NOT NULL DEFAULT 1,  -- 1=Draft,2=Approved/Posted,3=Cancelled
    refund_method SMALLINT NOT NULL DEFAULT 1, -- 1=Cash,2=Card,3=Mobile,4=Bank,5=StoreCredit,6=AgainstDue
    sub_total    NUMERIC(20,4) NOT NULL DEFAULT 0,
    discount_reversed NUMERIC(20,4) NOT NULL DEFAULT 0,
    tax_amount   NUMERIC(20,4) NOT NULL DEFAULT 0,
    round_off    NUMERIC(20,4) NOT NULL DEFAULT 0,
    grand_total  NUMERIC(20,4) NOT NULL DEFAULT 0,
    refund_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    total_cost   NUMERIC(20,4) NOT NULL DEFAULT 0,  -- COGS reversed back to inventory
    fin_year_no  BIGINT NOT NULL REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    gl_voucher_no BIGINT, approved_by BIGINT, approved_at TIMESTAMPTZ, posted_at TIMESTAMPTZ,
    remarks TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_ret_status CHECK (status IN (1,2,3)),
    CONSTRAINT chk_sal_ret_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sal_ret_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sal_ret_id   ON sal_return(branch_no, return_id) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_sal_ret_uuid ON sal_return(client_uuid);
CREATE INDEX idx_sal_ret_orig ON sal_return(original_invoice_no);
CREATE INDEX idx_sal_ret_cust ON sal_return(customer_no, return_date);

CREATE TABLE sal_return_dtl (
    return_dtl_no BIGSERIAL PRIMARY KEY,
    return_no    BIGINT NOT NULL REFERENCES sal_return(return_no) ON DELETE CASCADE,
    line_no      INTEGER NOT NULL,
    original_invoice_dtl_no BIGINT REFERENCES sal_invoice_dtl(invoice_dtl_no) ON DELETE RESTRICT,
    product_no   BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no   BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    batch_no     BIGINT REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,    -- restock same batch
    uom_no       BIGINT NOT NULL REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    qty          NUMERIC(18,4) NOT NULL,
    qty_base     NUMERIC(18,4) NOT NULL,
    unit_price   NUMERIC(20,4) NOT NULL,
    net_unit_price NUMERIC(20,4) NOT NULL,        -- price actually paid (post-discount) â†’ refund basis
    tax_rate_pct NUMERIC(5,2) NOT NULL DEFAULT 0,
    tax_amount   NUMERIC(20,4) NOT NULL DEFAULT 0,
    line_total   NUMERIC(20,4) NOT NULL DEFAULT 0,
    unit_cost    NUMERIC(20,6) NOT NULL DEFAULT 0, -- cost restocked
    line_cost    NUMERIC(20,4) NOT NULL DEFAULT 0,
    restock_flag SMALLINT NOT NULL DEFAULT 1,      -- 0 = scrap (damaged, do not add to saleable)
    remarks VARCHAR(250),
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_sal_ret_line UNIQUE (return_no, line_no),
    CONSTRAINT chk_sal_retdtl_qty CHECK (qty > 0)
);
CREATE INDEX idx_sal_retdtl_hdr ON sal_return_dtl(return_no);
```

## 2.9 `sal_receipt` (+ `sal_receipt_alloc`) â€” customer due collection (SAL_1102)
**Purpose:** money received against outstanding AR, allocated to specific invoices (or FIFO).
```sql
CREATE TABLE sal_receipt (
    receipt_no   BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no    BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    receipt_id   VARCHAR(40) NOT NULL,
    receipt_date DATE NOT NULL,
    customer_no  BIGINT NOT NULL REFERENCES sal_customer(customer_no) ON DELETE RESTRICT,
    payment_method SMALLINT NOT NULL,           -- same enum as sal_invoice_payment
    amount       NUMERIC(20,4) NOT NULL,
    allocated_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    unallocated_amount NUMERIC(20,4) NOT NULL DEFAULT 0,  -- advance / on-account
    currency_no  BIGINT REFERENCES sys_currency(currency_no) ON DELETE RESTRICT,
    exchange_rate NUMERIC(15,6) NOT NULL DEFAULT 1,
    bank_no BIGINT, txn_ref VARCHAR(80), cheque_no VARCHAR(40), cheque_date DATE,
    gl_account_no BIGINT,                        -- cash/bank debited
    status       SMALLINT NOT NULL DEFAULT 2,    -- 1=Draft,2=Posted,3=Cancelled
    fin_year_no  BIGINT NOT NULL REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    gl_voucher_no BIGINT, posted_at TIMESTAMPTZ, remarks TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_rcpt_status CHECK (status IN (1,2,3)),
    CONSTRAINT chk_sal_rcpt_amt CHECK (amount > 0),
    CONSTRAINT chk_sal_rcpt_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sal_rcpt_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sal_rcpt_id ON sal_receipt(branch_no, receipt_id) WHERE is_deleted = 0;
CREATE INDEX idx_sal_rcpt_cust ON sal_receipt(customer_no, receipt_date);

CREATE TABLE sal_receipt_alloc (
    receipt_alloc_no BIGSERIAL PRIMARY KEY,
    receipt_no   BIGINT NOT NULL REFERENCES sal_receipt(receipt_no) ON DELETE CASCADE,
    invoice_no   BIGINT NOT NULL REFERENCES sal_invoice(invoice_no) ON DELETE RESTRICT,
    allocated_amount NUMERIC(20,4) NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_sal_rcpt_alloc UNIQUE (receipt_no, invoice_no),
    CONSTRAINT chk_sal_rcptalloc_amt CHECK (allocated_amount > 0)
);
CREATE INDEX idx_sal_rcptalloc_inv ON sal_receipt_alloc(invoice_no);
```

## 2.10 `sal_customer_ledger` â€” AR subsidiary ledger (due tracking)
**Purpose:** every event affecting a customer's balance (invoice debit, receipt credit, return credit, opening) with running balance â€” the authoritative "who owes what" and the source for aging.
```sql
CREATE TABLE sal_customer_ledger (
    customer_ledger_no BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL,
    branch_no    BIGINT NOT NULL,
    customer_no  BIGINT NOT NULL REFERENCES sal_customer(customer_no) ON DELETE RESTRICT,
    txn_date     DATE NOT NULL,
    ref_doc_type SMALLINT NOT NULL,    -- 1=Opening,2=Invoice,3=Receipt,4=Return,5=Adjustment
    ref_doc_no   VARCHAR(40) NOT NULL,
    ref_doc_pk   BIGINT,
    debit        NUMERIC(20,4) NOT NULL DEFAULT 0,   -- increases due (invoice)
    credit       NUMERIC(20,4) NOT NULL DEFAULT 0,   -- decreases due (receipt/return)
    balance_after NUMERIC(20,4) NOT NULL,
    fin_year_no  BIGINT REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    remarks VARCHAR(250),
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_sal_custled_dctype CHECK (ref_doc_type IN (1,2,3,4,5)),
    CONSTRAINT chk_sal_custled_amt CHECK (debit >= 0 AND credit >= 0)
);
CREATE INDEX idx_sal_custled_cust ON sal_customer_ledger(customer_no, txn_date, customer_ledger_no);
CREATE INDEX idx_sal_custled_ref  ON sal_customer_ledger(ref_doc_type, ref_doc_no);
```
**Rule:** append-only like `inv_stock_ledger`. `sal_customer.current_due` = last `balance_after`. Reposting forbidden; corrections are reversing rows.

## 2.11 `sal_promotion` (+ `_dtl`) â€” promotions & discount schemes (SAL_1104)
**Purpose:** rule-driven automatic discounts/offers, time- and condition-bound.
```sql
CREATE TABLE sal_promotion (
    promotion_no  BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no     BIGINT REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,  -- NULL = all branches
    promotion_id  VARCHAR(30) NOT NULL,
    promotion_name VARCHAR(150) NOT NULL,
    promo_type    SMALLINT NOT NULL,   -- 1=LinePct,2=LineAmount,3=BillPct,4=BillAmount,5=BuyXGetY,6=QtyBreak,7=Coupon,8=Bundle
    scope_type    SMALLINT NOT NULL DEFAULT 1, -- 1=Product,2=Category,3=Brand,4=All,5=Customer/Group
    coupon_code   VARCHAR(40),
    priority      SMALLINT NOT NULL DEFAULT 0,
    is_stackable  SMALLINT NOT NULL DEFAULT 0,
    -- conditions
    min_qty       NUMERIC(18,4),
    min_amount    NUMERIC(20,4),
    discount_pct  NUMERIC(5,2),
    discount_amount NUMERIC(20,4),
    buy_qty       NUMERIC(18,4), get_qty NUMERIC(18,4),
    max_discount_amount NUMERIC(20,4),
    customer_type SMALLINT, price_tier SMALLINT,
    start_date    DATE NOT NULL, end_date DATE NOT NULL,
    start_time    TIME, end_time TIME,           -- happy-hour
    weekday_mask  SMALLINT,                       -- bitmask Mon..Sun
    usage_limit   INTEGER, used_count INTEGER NOT NULL DEFAULT 0,
    remarks TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_promo_type  CHECK (promo_type IN (1,2,3,4,5,6,7,8)),
    CONSTRAINT chk_sal_promo_dates CHECK (end_date >= start_date),
    CONSTRAINT chk_sal_promo_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sal_promo_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sal_promo_id ON sal_promotion(company_no, promotion_id) WHERE is_deleted = 0;
CREATE INDEX idx_sal_promo_active_window ON sal_promotion(company_no, start_date, end_date) WHERE is_active = 1 AND is_deleted = 0;

CREATE TABLE sal_promotion_dtl (   -- targets (which products/categories/brands the promo applies to or the "get" items)
    promotion_dtl_no BIGSERIAL PRIMARY KEY,
    promotion_no BIGINT NOT NULL REFERENCES sal_promotion(promotion_no) ON DELETE CASCADE,
    target_role  SMALLINT NOT NULL DEFAULT 1,   -- 1=Condition(buy),2=Reward(get)
    product_no   BIGINT REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no   BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    category_no  BIGINT REFERENCES inv_category(category_no) ON DELETE RESTRICT,
    brand_no     BIGINT REFERENCES inv_brand(brand_no) ON DELETE RESTRICT,
    qty          NUMERIC(18,4),
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sal_promodtl_role CHECK (target_role IN (1,2))
);
CREATE INDEX idx_sal_promodtl_hdr ON sal_promotion_dtl(promotion_no);
```

## 2.12 `sal_loyalty_txn` â€” points earn/redeem (optional)
```sql
CREATE TABLE sal_loyalty_txn (
    loyalty_txn_no BIGSERIAL PRIMARY KEY,
    company_no  BIGINT NOT NULL,
    customer_no BIGINT NOT NULL REFERENCES sal_customer(customer_no) ON DELETE RESTRICT,
    txn_date    DATE NOT NULL,
    txn_type    SMALLINT NOT NULL,  -- 1=Earn,2=Redeem,3=Expire,4=Adjust
    points      NUMERIC(18,4) NOT NULL,
    balance_after NUMERIC(18,4) NOT NULL,
    ref_doc_type SMALLINT, ref_doc_no VARCHAR(40),
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_sal_loy_type CHECK (txn_type IN (1,2,3,4))
);
CREATE INDEX idx_sal_loy_cust ON sal_loyalty_txn(customer_no, txn_date);
```

## 2.13 Relationship summary
`sal_customer` (1â€”N) `sal_invoice` (1â€”N `sal_invoice_dtl`, 1â€”N `sal_invoice_payment`). `sal_invoice` (1â€”N) `sal_return` (1â€”N `sal_return_dtl`). `sal_receipt` (1â€”N `sal_receipt_alloc` â†’ `sal_invoice`). `sal_customer_ledger` N rows per customer (AR history). `sal_pos_terminal` (1â€”N) `sal_pos_session` (1â€”N `sal_invoice`). `sal_promotion` (1â€”N `sal_promotion_dtl`) â†’ applied on `sal_invoice_dtl.promotion_no`. Stock/cost via `inv_*`; GL via `fin_*`; numbering via `sys_doc_sequence`; events via `sys_event_outbox`.

---

# 3. BUSINESS LOGIC

## 3.1 Pricing & discount resolution (deterministic order)
For each line, server computes (never trusts client totals):
1. **Base price** = `inv_product_price` match (branch, tier, qty-break, date) â†’ else variant `sale_price` â†’ else product `sale_price`. Enforce `>= min_sale_price` (override needs `can_approve`) and `<= mrp`.
2. **Line discount** (manual, within role cap).
3. **Promotions** (auto): evaluate active `sal_promotion` by priority; apply non-stackable highest-value or stack if `is_stackable`; BuyXGetY adds free lines (`is_free_item=1`, price 0). Respect `usage_limit`, time/weekday window, `max_discount_amount`.
4. **Bill discount** (header level) distributed proportionally back to lines for correct tax & margin.
5. **Tax:** per line `vat_tax_no` rate; inclusiveâ†’ extract tax from gross; exclusiveâ†’ add. `taxable_amount`/`tax_amount` summed to header.
6. **Round-off** to currency `decimal_places`; `grand_total` = taxable + tax + shipping Â± round_off âˆ’ discounts.

## 3.2 Sale state machine & stock interaction
```
Draft â”€editâ†’ Draft â”€â”€confirmâ”€â”€â–¶ Confirmed(Posted) â”€â”€â”¬â”€ return â”€â–¶ PartiallyReturned â”€â–¶ Returned
  â”‚                                                  â””â”€ cancel â”€â–¶ Cancelled (reversed)
Hold (reserves stock) â”€â”€resumeâ”€â”€â–¶ Draft â”€â”€confirmâ”€â”€â–¶ Confirmed
Draft/Hold â”€â”€voidâ”€â”€â–¶ Cancelled (release reservations, no GL/stock)
```
- **Hold (SAL_1002):** create `inv_reservation` rows (qty_reservedâ†‘ on `inv_stock`) with `expires_at`; no ledger, no GL. A scheduled job releases expired holds.
- **Confirm/Post (SAL_1001):** inside one transaction â†’
  1. resolve batches (FEFO for expiry-tracked), validate availability under `FOR UPDATE`;
  2. call `InvStockPostingService` movement_type=4 (OUT) â†’ fills `unit_cost` (COGS) per costing method; consume any reservation (reservedâ†’consumed);
  3. compute payments; `due = grand_total âˆ’ paid`; set `payment_status`;
  4. write `sal_customer_ledger` debit = grand_total (credit portion) â€” walk-in cash sale with paid=total writes no due;
  5. emit `fin_voucher` (Â§3.5);
  6. update `sal_pos_session` running totals;
  7. outbox `SaleConfirmed`, `LowStockDetected` (from engine).
- **Credit-limit check** before post: `current_due + new_due > credit_limit` â†’ block unless `can_approve`.

## 3.3 Return logic (SAL_1103)
- Against original invoice: validate `qty <= original_qty âˆ’ already_returned`; refund basis = `net_unit_price` actually paid (post-discount/promo); restock movement_type=5 (IN) to the **same batch/serial** (or to Damage warehouse if `restock_flag=0`); reverse COGS (inventory value restored at original issue cost).
- GL: reverse revenue & output VAT (proportionally), Dr Sales Return/Revenue & VAT, Cr Cash/Customer-AR (refund or due reduction); Dr Inventory, Cr COGS.
- Update `sal_invoice.returned_amount`, line `returned_qty`, status â†’ PartiallyReturned/Returned; AR ledger credit if refund applied to due.
- Blind return (no invoice): always `can_approve`; price/cost from current master; flagged in fraud report.

## 3.4 Due collection logic (SAL_1102)
- Receipt amount allocated to invoices: explicit allocations (`sal_receipt_alloc`) or auto-FIFO oldest-due-first; leftover â†’ `unallocated_amount` (advance).
- Each allocation: AR ledger credit on the invoice's customer; reduce `sal_invoice.due_amount`, bump `payment_status`. GL: Dr Cash/Bank, Cr Customer AR.
- Cancelling a posted receipt writes reversing AR ledger + reversing voucher; re-opens invoice dues.

## 3.5 Ledger impact (GL posting matrix â†’ FIN module)
| Event | Debit | Credit |
|---|---|---|
| POS/credit sale | Cash/Bank (paid) + Accounts Receivable (due) | Sales Revenue; Output VAT Payable; (Round-off to gain/loss) |
| â€” perpetual COGS | Cost of Goods Sold | Inventory |
| Sales return | Sales Return (contra-revenue); Output VAT (reversal) | Cash/Bank (refund) or Accounts Receivable (due â†“) |
| â€” return restock | Inventory | COGS |
| Due collection receipt | Cash/Bank | Accounts Receivable |
| POS session close (deposit) | Cash-in-Transit/Bank | Drawer Cash; Cash short/over â†’ expense/income |
| Loyalty redeem (if expensed) | Loyalty Expense / Sales | Cash equivalent (contra) |
All vouchers balance (Î£dr=Î£cr); accounts resolved from `fin_account` mapping (product category â†’ revenue/COGS/inventory accounts; tax â†’ `sys_vat_tax.gl_account_no`; tender â†’ terminal/bank account). Posting can be synchronous in-tx or via outboxâ†’GL-poster (idempotent on `ref_doc_no`).

## 3.6 POS session close (SAL_1003)
On close: system computes expected tender totals from `sal_invoice_payment` grouped by method for the session (minus returns). Cashier enters counted cash; `cash_variance = counted âˆ’ (opening_float + expected_cash)`; variance beyond tolerance â†’ manager approval. Post deposit voucher; set `status=Closed`; generate Z-report. No further sales accepted on a closed session.

## 3.7 Transaction safety, concurrency, idempotency, rollback
- One tx per posted document; stock relief + AR + GL + session update + outbox atomic.
- Concurrency: stock via engine `FOR UPDATE`; invoice header optimistic `row_version`; doc number atomic `UPDATE â€¦ RETURNING`; session totals updated under row lock.
- Idempotency: `client_uuid` unique on `sal_invoice`/`sal_return` â€” offline replays and double-clicks dedupe to the first post; the API returns the existing document.
- Rollback: any failure (insufficient stock, closed period, over-limit without approval, unbalanced voucher) aborts the whole post; nothing partial persists.

## 3.8 Unit conversion / batch / FX
Lines normalize `qty_base` server-side (via `inv_uom_conversion`); batch chosen FEFO for expiry-tracked; FX sale stores document-currency amounts + `exchange_rate`, posts base-currency to GL/AR.

---

# 4. FRONTEND IMPLEMENTATION PLAN

## 4.1 Screens / routes (`sal.routes.ts`, lazy)
| Form | Route | Pattern |
|---|---|---|
| SAL_1001 POS | `sal/pages/pos` (already scaffolded) | full-screen POS |
| SAL_1002 Draft/Hold | `sal/forms/sal1002` or POS hold tray | list + resume |
| SAL_1003 POS Closing | `sal/forms/sal1003` | session count form |
| SAL_1101 Customer | `sal/forms/sal1101` (+ `pages/customer-list`) | master-detail |
| SAL_1102 Due Collection | `sal/forms/sal1102` | customerâ†’open invoicesâ†’allocate |
| SAL_1103 Sales Return | `sal/forms/sal1103` | invoice lookup â†’ return doc |
| SAL_1104 Promotions | `sal/forms/sal1104` | rule builder |
| Invoice List / Sales Report | `sal/pages/invoice-list`, `sal/pages/sales-report` (scaffolded) | grids |

## 4.2 SAL_1001 POS (reference screen â€” performance-critical)
- **Layout:** two panes. Left = scrollable cart grid (`app-common-table` dense), right = totals + tender. Top = barcode/search input (always autofocused), customer selector (defaults walk-in), warehouse/terminal context badge.
- **Signals:** `cart = signal<CartLine[]>`, `computed` subtotals/discount/tax/total/change; `activePayments = signal<Payment[]>`. No full reactive FormGroup per line for speed â€” use signal store; validate on confirm.
- **Add item:** barcode resolve â†’ `inv_product_barcode` (product/variant/uom/pack_qty); if not found, search modal. Qty editable inline; price auto from tier/promo (server-priced on a debounced `/price` call or cached price list for offline).
- **Tender modal:** numeric keypad, method buttons (Cash/Card/bKash/Nagad/Bank/Credit), split rows; computes change; Enter to add tender; Confirm when paidâ‰¥total (or due allowed for credit customer).
- **Hold/Resume:** "Hold" parks cart (POST draft, status=Hold) â†’ appears in hold tray (SAL_1002) with customer/amount/time chips.
- **Receipt print:** 80mm thermal template (logo, items, tax breakdown, tenders, change, barcode/QR of invoice_id, footer); auto-print on confirm; reprint from invoice list.
- **Keyboard shortcuts:** `F1` search, `F2` qty edit, `F3` customer, `F4` discount, `F8` hold, `F9` tender/pay, `F10` confirm, `Del` remove line, `+/-` qty, `Esc` cancel. Documented on-screen help (`?`).
- **Offline-safe:** see Â§5.5 â€” cart & catalog cached; queued submit; show "offline" banner.
- **Loading/Error UX:** optimistic line add; toast on tender errors; block double-confirm (disable button + idempotency key); 409 â†’ reload invoice.
- **Permission rendering:** discount-over-cap, below-min-price, over-credit-limit prompt for manager PIN (re-auth) â†’ `can_approve`.

## 4.3 SAL_1101 Customer (master-detail)
Left list (`app-common-table`, search, due badge); right tabs *General Â· Credit Â· Address Â· Ledger*. Ledger tab shows `sal_customer_ledger` running balance + aging summary. Reactive form, snake_case fields, `current_due` read-only.

## 4.4 SAL_1102 Due Collection
Select customer â†’ grid of open invoices (due, age) with allocate inputs + "Auto-allocate (FIFO)"; receipt header (method/amount/ref). On post â†’ toast + refresh dues. Print money-receipt.

## 4.5 SAL_1103 Return
Scan/enter original invoice â†’ load lines with returnable qty; pick qty + reason + restock/scrap; refund method; totals computed; approval gate for blind/over.

## 4.6 SAL_1003 Closing
Session summary (expected by tender, invoice count); cash-count entry (denomination breakdown optional); variance highlight; manager sign-off; Z-report print.

## 4.7 Reusable / responsive / printing
Shared: `sal-customer-picker`, `tender-modal`, `cart-line`, `receipt-print`, `aging-badge`. POS optimized for touch tablets (large buttons, Tailwind `lg` breakpoints); admin forms desktop-grid. Printing: 80mm receipt + A4 tax invoice (with company VAT/BIN from `sys_company`) + money receipt; print stylesheet, configurable templates.

---

# 5. BACKEND & MIDDLEWARE PLAN

## 5.1 Endpoints (form-wise, `/api/v1/sal/forms/{formId}`)
**POS SAL_1001:** `POST /sales` (confirm/post; body has `client_uuid`), `POST /sales/hold`, `GET /sales/held`, `POST /sales/{no}/resume`, `POST /sales/{no}/void`, `POST /sales/quote-price` (server pricing of a cart), `GET /sales/{no}` (reprint), `GET /barcode/{code}`.
**Returns SAL_1103:** `GET /returns`, `GET /invoices/{invoiceNo}/returnable`, `POST /returns`, `POST /returns/{no}/approve`, `POST /returns/{no}/cancel`.
**Due SAL_1102:** `GET /customers/{customerNo}/open-invoices`, `POST /receipts`, `POST /receipts/auto-allocate`, `POST /receipts/{no}/cancel`.
**Customer SAL_1101:** `GET/POST /customers`, `/customers/page`, `/customers/{no}`, `/customers/{no}/ledger`, `DELETE /customers/{no}`.
**Session SAL_1003:** `POST /sessions/open`, `GET /sessions/current`, `GET /sessions/{no}/summary`, `POST /sessions/{no}/close`.
**Promotions SAL_1104:** `GET/POST /promotions`, `/promotions/{no}`, `POST /promotions/evaluate` (cartâ†’applicable promos).
All return `ApiResponse<T>`; lists asc by PK with `/page` variants.

## 5.2 DTOs (snake_case, validated)
```java
@Data public class Sal1001SaleDto {
  private Long invoice_no; private String invoice_id; private UUID client_uuid;
  @NotNull private LocalDate invoice_date; @NotNull private Short sale_type;
  @NotNull private Long customer_no; @NotNull private Long warehouse_no;
  private Long terminal_no; private Long pos_session_no;
  private Short bill_discount_type; private BigDecimal bill_discount_value;
  private BigDecimal shipping_charge; private BigDecimal round_off;
  private LocalDate due_date; private String remarks; private Long row_version;
  @NotEmpty @Valid private List<Sal1001SaleLineDto> lines;
  @Valid private List<Sal1001PaymentDto> payments;
}
```
Server recomputes every monetary field from lines+master config; client-sent totals are ignored (or 400 on mismatch in strict mode). MapStruct ignores audit fields per house rule.

## 5.3 Middleware / cross-cutting
- Auth/tenant from `CompanyBranchContext`; RBAC by form_id (`SAL_1001`â€¦), `can_approve` for overrides/voids/blind returns.
- Inventory locking delegated to `InvStockPostingService` (pessimistic per cell). Header optimistic lock.
- Validation: `@Valid` â†’ 400 field map; business rules throw `ValidationException` (credit limit, min price, closed period, insufficient stock) â†’ actionable 400.
- Audit: `sys_audit_log` for all entities; sensitive overrides logged with approver + reason.
- Idempotency: `client_uuid` (unique) + optional `Idempotency-Key` header; replays return the original posted doc.
- Logging: `@Slf4j`; `sys_log` request log.
- Error handling: `GlobalExceptionHandler` envelopes; 409 on optimistic conflict.
- Retry: posting retried on lock/serialization failure; GL/notification off outbox with retry.

## 5.4 Queue / events
Outbox events: `SaleConfirmed`, `SaleHeld`, `SaleVoided`, `ReturnPosted`, `ReceiptPosted`, `CreditLimitBreached`, `SessionClosed`, `LoyaltyPointsChanged`. Consumers: GL poster, AR/aging MV refresh, notifications (SMS receipt/loyalty), analytics, low-stock (via inv engine). Phase-1 Spring `@TransactionalEventListener(AFTER_COMMIT)`; broker-ready via outbox.

## 5.5 Offline-safe POS strategy
- **Client cache (IndexedDB):** product/barcode/price-list snapshot + customer list pulled on session open; refreshed on reconnect.
- **Local pricing:** prices computed client-side from cached price list/promotions so sales continue offline; server re-prices & validates on sync (discrepancy â†’ flagged, manager-resolved).
- **Queued submit:** each sale gets a client `client_uuid`; stored locally; background sync POSTs queued sales when online; server idempotency dedupes; conflicts (stock now negative) handled per branch negative policy or queued for review.
- **Sequence:** POS uses terminal `receipt_prefix` + local counter for the printed receipt; server assigns the canonical `invoice_id` on sync (printed receipt stores `client_uuid` + local no; reconciliation maps them).
- **Constraints:** offline sales are cash/within-cache only; credit-limit checks deferred to sync (advisory offline). Drawer reconciliation still per session.

---

# 6. REPORTING & ANALYTICS
- **Reports:** daily sales summary (by branch/terminal/cashier/payment method), sales by product/category/brand/customer, **invoice register**, sales **return register**, **customer due/aging (0-30/31-60/61-90/90+)**, receipt/collection register, gross-margin (revenueâˆ’COGS) by product/category, discount & promotion effectiveness, POS Z-report & session variance, top customers/products, hourly sales heatmap, tax/VAT output summary (for filing), salesperson performance.
- **KPIs:** today's sales/returns/net, avg basket value, items per basket, gross margin %, total AR outstanding & overdue, collection ratio, cash variance trend, repeat-customer %, conversion of holds.
- **Aggregation/MVs:** `mv_sal_daily_summary` (branchÃ—dateÃ—method), `mv_sal_customer_aging` (refresh on ReceiptPosted/SaleConfirmed), `mv_sal_product_margin`. AR balance from `sal_customer_ledger` last `balance_after`; never re-sum invoices at runtime.
- **Optimization:** indexes `idx_sal_inv_date`, `idx_sal_inv_due`, `idx_sal_custled_cust`; report off replica/MVs; stream large exports.

---

# 7. SECURITY & COMPLIANCE
## 7.1 Permission matrix
| Form | view | insert | update | delete | approve |
|---|---|---|---|---|---|
| SAL_1001 POS | Cashier | Cashier | (draft only) | â€” | Manager (override/void) |
| SAL_1101 Customer | Sales | Sales | Sales | Admin | Manager (credit limit) |
| SAL_1102 Due Collection | Cashier/CS | Cashier/CS | â€” | Admin | Manager (cancel receipt) |
| SAL_1103 Return | CS | CS | (draft) | Admin | **Manager (post/blind)** |
| SAL_1003 Closing | Cashier | Cashier | â€” | â€” | Manager (variance) |
| SAL_1104 Promotions | Manager | Manager | Manager | Admin | â€” |

## 7.2 Access control & sensitive actions
Branch isolation via `sys_user_branch`/context; manager re-auth (PIN) for: discount above cap, sell below `min_sale_price`, over-credit-limit, void posted/cancel, blind return, session variance, price/promo override. All such actions audited (actor, approver, reason).
## 7.3 Audit & fraud prevention
Immutable `sal_customer_ledger` + `inv_stock_ledger` + `sys_audit_log`. Fraud signals: high void/return rate per cashier, frequent no-customer refunds to cash, recurring cash-short variance, after-hours sales, discount outliers, returns without original invoice â†’ exception dashboard. Sequential, gap-audited invoice numbering deters deletion.
## 7.4 Data validation
Client + API (`@Valid`) + DB (CHECK/UNIQUE/FK). Server is sole authority for prices, tax, totals, COGS, due.

---

# 8. PERFORMANCE & SCALABILITY
- POS confirm latency budget < 300ms p95: single-tx, indexed cell locks, server pricing cached (Redis price list per branch/tier with event invalidation), receipt rendered client-side.
- Indexes per FK + partial active; AR aging via MV; `client_uuid` unique for idempotent retries.
- Caching: catalog/barcode/price-list/promotions in Redis (read-mostly), customer typeahead. Invoice writes never cached.
- Read/write split: reports on replica; POS on primary.
- Partition `sal_invoice`/`sal_invoice_dtl`/`sal_customer_ledger` by `invoice_date`/`txn_date` (monthly) at high volume; keep current period hot.
- Horizontal scale: stateless JWT API; per-terminal session avoids cross-terminal contention; promotions evaluated in-memory from cached rules.
- Multi-tenant: company/branch on every row; RLS-ready.

---

# 9. TESTING STRATEGY
- **Unit:** pricing/discount/promotion resolution (each promo_type), tax inclusive/exclusive, round-off, bill-discount proportional spread, change calc, FEFO selection, credit-limit gate, allocation FIFO.
- **Integration (PG/Testcontainers):** confirm sale â†’ stock OUT + COGS + AR ledger + GL voucher + session totals + outbox atomic; rollback on insufficient stock/closed period; idempotent re-post by `client_uuid`; return restocks same batch & reverses GL; receipt allocation reduces dues correctly.
- **E2E:** POS sale (cash split tender)â†’receipt; holdâ†’resumeâ†’confirm; credit saleâ†’partial receiptâ†’aging; returnâ†’refund; session openâ†’salesâ†’close with variance approval; offline queueâ†’sync.
- **Inventory consistency:** post-sale `inv_stock` matches ledger; return reverses exactly.
- **Concurrency:** two terminals selling last unit (one succeeds / negative policy honored); parallel receipts on same invoice don't over-allocate (`due >= 0` invariant); doc-number uniqueness.
- **Financial integrity:** every sale/return/receipt voucher balances; `sal_customer.current_due == last sal_customer_ledger.balance_after == grand_total âˆ’ paid âˆ’ returns` per customer; AR control GL == Î£ customer dues; session expected cash == Î£ cash tenders.

---

# 10. PRODUCTION DEPLOYMENT NOTES
- **Migration:** Flyway `V{n}__sal_*.sql` after `inv_*` (FK deps). Seed walk-in customer + default terminal/session config per branch + `sys_doc_sequence` rows (SAL_INV/SAL_RET/SAL_RCPT) + `fin_account` mappings (revenue/COGS/AR/VAT/cash). `pg_trgm` for customer search.
- **Rollback:** down scripts; never drop posted ledgers/invoices once live â€” forward-fix. New forms gated via `sys_enroll_menu`.
- **Seed data:** payment-methodâ†’GL map, price tiers aligned to `inv_product_price`, sample promotion, tax links to `sys_vat_tax`.
- **Environment:** DB/JWT/Redis/broker vars; profiles; port 7860; CORS per backend `CLAUDE.md`.
- **Monitoring:** POS confirm latency, outbox backlog, AR posting failures, session-close variance alerts, offline-sync conflict count, lock waits.
- **Backup/DR:** PITR + nightly dump; append-only ledgers ease consistency; AR can be rebuilt by replaying `sal_customer_ledger`; cross-region replica for failover; documented RPO/RTO. Receipts retained per tax-law retention period.

---

## Appendix â€” Enums
`customer_type`:1 WalkInÂ·2 RetailÂ·3 WholesaleÂ·4 Corporate | `sale_type`:1 POSÂ·2 CreditÂ·3 Quotation | invoice `status`:1 DraftÂ·2 HoldÂ·3 ConfirmedÂ·4 PartiallyReturnedÂ·5 ReturnedÂ·6 Cancelled | `payment_status`:1 UnpaidÂ·2 PartialÂ·3 Paid | `payment_method`:1 CashÂ·2 CardÂ·3 MobileÂ·4 BankÂ·5 ChequeÂ·6 CreditÂ·7 PointsÂ·8 GiftCardÂ·9 StoreCredit | return `status`:1 DraftÂ·2 PostedÂ·3 Cancelled | session `status`:1 OpenÂ·2 ClosingÂ·3 Closed | `promo_type`:1 LinePctÂ·2 LineAmtÂ·3 BillPctÂ·4 BillAmtÂ·5 BuyXGetYÂ·6 QtyBreakÂ·7 CouponÂ·8 Bundle | customer-ledger `ref_doc_type`:1 OpeningÂ·2 InvoiceÂ·3 ReceiptÂ·4 ReturnÂ·5 Adjustment.


## PUR/SAL Integration Plan

# PUR + SAL â€” Build-Readiness & Integration Plan (bridge to the built platform)

**Status: planning only â€” nothing built yet.** The full module designs already exist:
`pur-db.md` / `pur-forms-design.md` and `sal-db.md` / `sal-forms-design.md` (schema, UX, state
machines, GL matrices, build waves). Those are the *what*. **This doc is the missing *how-it-plugs-in*** â€”
it pins every integration seam those blueprints reference abstractly ("outboxâ†’GL-poster",
"`InvStockPostingService` movement 2", "`fin_account` mapping") to the **concrete contracts that are now
actually built** (FIN GL engine + outbox + `fin_gl_map`, INV stock-posting engine, SYS approval engine).
Read the blueprints for detail; read this before writing code so PUR/SAL wire in exactly like INV/HRM did.

> Reference implementations to copy, not re-derive:
> `Inv1102Service` (stock post + outbox emit + reversal), `Hrm1202Service.emitPayrollPosted` (outbox emit),
> `Hrm1301Service` + `HrmApprovalListener` (approval wiring), `Fin1101Service.postSystemVoucher` (the GL sink),
> `FinPostingService` (the outbox drain), `FinApprovalListener` (FIN_VOUCHER apply).

---

## 0. The four integration seams (pinned to built code)

Every posted PUR/SAL document touches the same four shared services. **Modules never write `fin_*` tables**
and never write `inv_stock*` directly â€” they call the engines.

### Seam 1 â€” Inventory movement â†’ `InvStockPostingService`
- Call `postingService.post(new StockPostingCommand(companyNo, branchNo, refDocType, refDocId, refDocNo /*Long*/, date, finYearNo, costCenterNo /*nullable*/, isReversal, legs))`.
- Build legs with `StockPostingLeg.in(warehouseNo, productNo, variantNo, batchNo, qtyBase, unitCost, movementType, refLineNo)` for receipts, `StockPostingLeg.out(warehouseNo, productNo, variantNo, batchNo, qtyBase, movementType, refLineNo)` for sales/issues.
- Movement types: **PUR receipt = IN**, **SAL sale = OUT** (use the INV movement-type codes already defined; adjustment used 8/9/10 â€” pick the receipt/issue codes from the INV enum). Cancel = same legs with `isReversal=true`.
- **COGS contract (SAL):** the OUT posting is what *computes* the costed value (FIFO/avg layer). The SAL posting service must read that costed amount back (engine return value or the just-written `inv_stock_ledger` rows for this `refDocNo`) and use it for the **COGS / Inventory** GL legs. âš ï¸ Confirm the engine exposes the costed total per post â€” if not, add a small return/lookup. This is the one non-obvious coupling.

### Seam 2 â€” GL posting â†’ `sys_event_outbox` (NOT direct `fin_*`)
- Decoupled, raw-JSON, idempotent. Copy the emitter shape from `Inv1102Service.emitGlEvent` / `Hrm1202Service.emitPayrollPosted`:
  - inject `EventOutboxRepository`; build `private static final ObjectMapper OUTBOX_MAPPER = JsonMapper.builder().addModule(new JavaTimeModule()).build();` (the app has **no** ObjectMapper bean).
  - payload = `GlPostingPayload` JSON: `{ voucherDate, narration, branchNo, legs:[{legKey, subKey?, amount, drCr /*dr or cr*/, costCenterNo?, partyType?, partyNo?}] }`.
  - `EventOutbox`: `companyNo`, `branchNo`, `aggregateType` (e.g. `PUR_INVOICE`), `aggregateId` = **the document PK as a String of a Long** (FinPostingService `parseLong`s it â†’ must be numeric), `eventType` (e.g. `PurchaseInvoicePosted`), `status=1`.
  - **Reversal/cancel** must use a *distinct* `eventType` (e.g. `PurchaseInvoiceReversed`) so the engine's idempotency key `(eventType, aggregateId)` doesn't dedupe it.
- `FinPostingService` (scheduled, every 30s) drains â†’ resolves each leg via `fin_gl_map(company, eventType, legKey, subKey)` â†’ `Fin1101Service.postSystemVoucher` â†’ immutable `fin_ledger`. Unmapped leg â‡’ event **parks as Failed** for the FIN_1201 monitor to re-drive; never blocks the queue.
- **Set `partyType`/`partyNo` on the AR/AP legs** (party_type 1=Customer, 2=Supplier). This is what lights up the already-built **FIN_1307 Aging** straight from `fin_ledger` â€” no extra work needed there.

### Seam 3 â€” Approval gate â†’ `ApprovalService` (configured, never hard-coded)
- Submit â†’ `approvalService.raise(documentType, documentPk, documentNo, amount)` â†’ `ApprovalOutcome` (auto when no `sys_approval_workflow` row covers the amount, else pending).
- Register a `PurApprovalListener` / `SalApprovalListener` `@EventListener` on `ApprovalCompletedEvent` dispatching by `documentType` â†’ `service.applyApprovalOutcome(pk, approved)` (the post/stock/GL happens there, synchronously in the engine tx). Copy `HrmApprovalListener` + `FinApprovalListener`.
- **`document_type` registry to add:** `PUR_PO`, `PUR_INVOICE`, `PUR_RETURN`, `PUR_PAYMENT`, `SAL_RETURN`, `SAL_POS_SESSION` (and `SAL_INVOICE` only if credit sales need a gate; POS cash sales auto-post). `PUR_PO` and `SAL_RETURN` are already named in the SYS registry.

### Seam 4 â€” Period guard Â· doc numbering Â· fin-year (reused as-is)
- `InvPeriodResolver.resolveOpenFinYear(branchNo, date)` â†’ the open `FinYear` (branch-scoped; period must be Open to post â€” same guard FIN/INV use; honors FIN_1401 close).
- `InvDocSequenceService.next(companyNo, branchNo, docType, finYearNo)` for gap-free document ids â€” register `PUR_PO/PUR_GRN/PUR_INV/PUR_RET/PUR_PAY`, `SAL_INV/SAL_RET/SAL_RCT`.
- UOM `toBaseQty` via `inv_uom_conversion`; FX via `sys_currency.exchange_rate`; tenant via `CompanyBranchContext`.

---

## 1. `fin_gl_map` seed contract (the concrete config FIN_1006 needs)

Each posted event must have every `(eventType, legKey)` mapped to a GL account for the company, or it parks.
Derived from `pur-db.md Â§3.4` and `sal-db.md Â§3.5`. `subKey` differentiates where one legKey resolves to
different accounts (e.g. per product-category inventory/revenue) â€” leave `subKey=""` for the simple SME case.

| eventType | legKey (drCr) | resolves to (control_type / role) |
|---|---|---|
| **PurchaseInvoicePosted** | INVENTORY (Dr) Â· INPUT_VAT (Dr) Â· AP (Cr, +party) | inventory Â· input-VAT receivable Â· AP control (2) |
| â†³ two-step | GRN_CLEARING (Dr) Â· INPUT_VAT (Dr) Â· PPV (Dr/Cr) Â· AP (Cr, +party) | GRN clearing Â· â€¦ Â· purchase-price-variance |
| **GoodsReceiptPosted** (two-step) | INVENTORY (Dr) Â· GRN_CLEARING (Cr) | inventory Â· GRN clearing accrual |
| **PurchaseReturnPosted** | AP (Dr, +party) Â· INVENTORY (Cr) Â· INPUT_VAT (Cr) | reverse of invoice |
| **SupplierPaymentPosted** | AP (Dr, +party) Â· CASH_BANK (Cr) | AP control (2) Â· bank/cash (3/4) |
| **SalesInvoicePosted** | CASH_BANK (Dr) Â· AR (Dr, +party) Â· REVENUE (Cr) Â· OUTPUT_VAT (Cr) Â· ROUNDING (Dr/Cr) | tender Â· AR control (1) Â· sales income Â· output-VAT payable |
| â†³ perpetual COGS | COGS (Dr) Â· INVENTORY (Cr) | COGS expense Â· inventory â€” **amounts from Seam-1 costed OUT** |
| **SalesReturnPosted** | SALES_RETURN (Dr) Â· OUTPUT_VAT (Dr) Â· CASH_BANK/AR (Cr, +party) Â· INVENTORY (Dr) Â· COGS (Cr) | contra-revenue Â· â€¦ |
| **CustomerReceiptPosted** | CASH_BANK (Dr) Â· AR (Cr, +party) | bank/cash Â· AR control (1) |
| **PosSessionClosePosted** | CASH_IN_TRANSIT (Dr) Â· DRAWER_CASH (Cr) Â· CASH_OVER_SHORT (Dr/Cr) | deposit Â· drawer Â· variance to expense/income |

> Build task per wave: add the wave's gl_map rows to the FIN_1006 seed (or a `*_gl_map_seed.sql`) so the loop
> works the moment a document posts. VAT accounts may alternatively resolve from `sys_vat_tax.gl_account_no`
> â€” **decision D3 below**.

---

## 2. AR / AP source-of-truth (the one real design decision)

The blueprints define module sub-ledgers (`pur_supplier_ledger`, `sal_customer_ledger`) **and** the GL carries
party tags on the AR/AP control accounts. Resolve the overlap explicitly:

- **Sub-ledger tables = operational truth** for a party's running balance, statements, and fast aging; written
  append-only by `PurApLedgerService` / `SalArLedgerService` inside the same post tx. They feed **FIN_1202 AP /
  FIN_1203 AR** (currently deferred) and the supplier/customer statement screens.
- **GL party tags (`fin_ledger.party_type/party_no`) = accounting truth**, written by Seam-2 setting party on
  the AR/AP legs. They feed the **already-built FIN_1307 Aging** with zero extra code.
- **Rule:** every AR/AP-moving post writes *both*, from the same transaction, with the same numbers â€” they
  reconcile by construction. FIN_1202/1203 then become thin readers over the sub-ledger tables (build alongside
  PUR/SAL, or as a fast-follow). Do **not** let them drift.

---

## 3. Unified cross-module build order (waves)

Mirrors the per-module waves in the forms-design docs, interleaved so shared services land before documents.
Verify every wave: `./mvnw -o -q compile` + `./mvnw -o test -Dtest=AidlyApplicationTests` + `npx nx build web-client`,
then seed that wave's `fin_gl_map` rows + approval `document_type`s + menu seed + enroll (company 2), exactly
like FIN.

- **Wave 0 â€” this plan + decisions D1â€“D5 confirmed; gl_map seed skeleton; doc-sequence + approval registry entries.**
- **Wave 1 â€” Masters:** PUR_1001 Supplier (+ PUR_1002 price list), SAL_1101 Customer (+ group). Plain CRUD (SYS1007 pattern).
- **Wave 2 â€” Shared posting services (no UI, unit-tested hard):** `PurReceivingPostingService` + `PurApLedgerService` (+ `PurLandedCostAllocator`); `SalPostingService` + `SalArLedgerService` (+ POS session math). Each = Seam-1 stock + Seam-2 outbox emit + sub-ledger write, one `@Transactional`, idempotent on `(ref_doc_type, ref_doc_no)`.
- **Wave 3 â€” Commitment / front-line:** PUR_1101 Purchase Order; SAL_1001 POS Sales (performance-critical) + SAL_1002 hold/draft.
- **Wave 4 â€” Receive & settle:** PUR_1105 GRN (if `is_grn_required`), PUR_1102 Invoice, PUR_1106 Landed Cost, PUR_1103 Return, PUR_1104 Payment; SAL_1003 POS session close, SAL_1102 due collection, SAL_1103 return.
- **Wave 5 â€” Sub-ledger forms + reports:** FIN_1202 AP / FIN_1203 AR (read sub-ledgers); supplier/customer statements; PUR/SAL aging + spend/sales MVs. Verify FIN_1307 aging + FIN_1306 cash-flow already reflect the new postings (they will, via Seams 2 + party tags).

---

## 4. Decisions to confirm before Wave 2 (cheap now, expensive later)

- **D1 â€” Receiving model default:** one-step (invoice receives + posts AP, SME default) vs two-step (GRN then invoice). `is_grn_required` is a company/branch flag; pick the default and whether two-step is in v1.
- **D2 â€” COGS costing:** confirm `InvStockPostingService` returns the costed value of an OUT post (for the SAL COGS legs). If it doesn't yet, that's a small engine addition in Wave 2.
- **D3 â€” VAT account routing:** resolve VAT GL accounts via `fin_gl_map` (INPUT_VAT/OUTPUT_VAT legKeys) **or** via `sys_vat_tax.gl_account_no` per tax code. Pick one source so the emitter is unambiguous.
- **D4 â€” Credit sales & approval:** do POS credit sales (AR) need an approval gate (credit-limit), or only returns/discount overrides? Sets whether `SAL_INVOICE` joins the approval registry.
- **D5 â€” FIN_1202/1203 timing:** build the AP/AR reader forms inside SAL/PUR (Wave 5) or as a separate FIN fast-follow. Either works; the sub-ledger tables are the contract.

---

## 5. Definition of done (per module, mirrors FIN)

Masters â†’ shared posting services â†’ documents â†’ settle â†’ reports, with: stock posted via the engine, GL posted
**only** via the outboxâ†’`FinPostingService` loop (party-tagged AR/AP legs), approvals via `ApprovalService` +
SYS_1108 config, period guard honored (FIN_1401), gap-free numbering, idempotent posts, soft-delete, tenant
isolation, `<app-common-table>` grids, constants centralized in `{module}.constants.ts`, DB migration + menu
seed + company-2 enrollment, and `mvnw compile` + `AidlyApplicationTests` + `nx build` green at every wave.
When done, the auto-posting loop is real end-to-end: **buy â†’ receive â†’ pay** and **sell â†’ return â†’ collect**
all flow to the immutable `fin_ledger` and surface in TB / P&L / Balance Sheet / Cash Flow / Aging with no FIN code changes.

