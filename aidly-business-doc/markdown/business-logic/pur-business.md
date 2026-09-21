# PUR Business

<!-- Consolidated from the previous aidly-business-doc/backend, frontend, memory, and planning files. -->

PUR manages suppliers, purchase commitments, goods receipt, supplier invoices, landed costs, purchase returns, and supplier payments. It calls INV for stock movement and emits FIN outbox events for AP and inventory accounting.

## Purchase Database and Backend Blueprint

# PUR â€” Purchase Management Module (Production Blueprint)

> **Module prefix:** `pur_` Â· **Backend package:** `com.infoaidtech.aidly.pur` Â· **Frontend feature:** `apps/web-client/src/app/features/pur`
> **Forms covered:** PUR_1001 Supplier Management Â· PUR_1101 Purchase Order Â· PUR_1102 Purchase Invoice Â· PUR_1103 Purchase Return Â· PUR_1104 Supplier Payment Â· (+ PUR_1105 Goods Receipt Note â€” optional 3-way-match intermediate; PUR_1106 Landed Cost â€” optional, both documented)
> **Depends on:** `inv-db.md` (`InvStockPostingService`, `inv_stock`, `inv_product`, `inv_batch`, `inv_warehouse`, `inv_uom_conversion`, `sys_doc_sequence`, `sys_event_outbox`). **Aligns with** the same conventions reproduced in `inv-db.md`/`sal-db.md`. Purchase is the inbound counterpart of sales: it **receives** stock (movement IN at landed cost) and owns the supplier, **AP/payable** ledger, and buy-side GL postings.
> Purchase **never** writes `inv_stock` directly â€” it calls the inventory posting engine.

---

## Conventions
Identical to `inv-db.md` â†’ "Conventions inherited from the existing codebase". `-- << AUDIT BLOCK >>` = the standard audit columns + two CHECKs. Money `NUMERIC(20,4)`, qty `NUMERIC(18,4)`, cost `NUMERIC(20,6)`, pct `NUMERIC(5,2)`, FX `NUMERIC(15,6)`.

---

# 1. BUSINESS MODULE OVERVIEW

## 1.1 What this module does
Purchase manages procurement end-to-end: supplier master & terms, **Purchase Order** (commitment, approval), **receiving** goods into stock (as part of the invoice, or via an optional GRN for 3-way match), **Purchase Invoice/Bill** (creates the **payable**), **Purchase Return / Debit Note** (send goods back, reduce payable), and **Supplier Payment** (settles AP, allocated to bills). It is the authoritative source of **landed cost** that flows into inventory valuation (so margins are correct downstream in sales), and it posts the buy-side accounting (inventory/GRN-clearing, input VAT, accounts payable).

## 1.2 Real retail workflow (lifecycle)
1. **Supplier onboarding** (PUR_1001) â€” terms, credit days/limit, tax IDs, opening payable.
2. **Reorder/PO** (PUR_1101) â€” from low-stock suggestions or manual; `Draft â†’ Submitted â†’ Approved â†’ (Sent)`. PO is a commitment, **no stock/GL impact**.
3. **Receive** â€” goods arrive:
   - *Two-step (3-way match):* GRN (PUR_1105) increases stock at provisional cost (Dr Inventory, Cr GRN-Clearing); later the Invoice (PUR_1102) matches GRN+PO (Dr GRN-Clearing + Input VAT, Cr AP) and reconciles price/qty variances.
   - *One-step (SME default):* Purchase Invoice (PUR_1102) **both** receives stock and creates the payable in one document.
4. **Landed cost** (PUR_1106, optional) â€” freight/duty/clearing allocated across received lines, raising unit cost in valuation.
5. **Return** (PUR_1103) â€” defective/excess back to supplier; stock OUT; payable reduced (debit note) or refund.
6. **Payment** (PUR_1104) â€” pay supplier; allocate to invoices (explicit or FIFO); advances supported.
7. **Period control** â€” all postings validated against the open `sys_fin_year`/period.

## 1.3 Actors
Purchaser/Procurement (PO, supplier), Store/Receiving Clerk (GRN, invoice receiving), Purchase Manager (approve PO, approve return, price-variance sign-off), Accountant (invoice matching, payments, AP reconciliation), Admin (suppliers, GL mapping, approval thresholds).

## 1.4 Approval flows
- **PO:** `Draft â†’ Submitted â†’ Approved â†’ (PartiallyReceived â†’ Received) / Closed / Cancelled`. Approval (`can_approve`) required above a configurable value threshold before goods can be received against it.
- **GRN:** `Draft â†’ Posted (stock IN) â†’ (Cancelledâ†’reversed)`.
- **Invoice:** `Draft â†’ (Matched) â†’ Posted (AP + stock if one-step) â†’ (PartiallyReturned/Returned) / Cancelled(reversed)`. Price/qty variance beyond tolerance needs `can_approve`.
- **Return:** `Draft â†’ Approved/Posted â†’ Cancelled`.
- **Payment:** `Draft â†’ Posted â†’ Cancelled(reversed)`.

## 1.5 Edge cases
Over/under-receipt vs PO (tolerance %, block/allow/approve); partial receipts across multiple GRNs/invoices against one PO; price on invoice â‰  PO price (purchase-price variance posting); FX PO received when rate changed (cost at receipt-date rate; variance to exchange gain/loss); batch/expiry capture at receipt (create `inv_batch`); receiving into wrong warehouse (transfer to correct); duplicate supplier invoice number (block per supplier); landed cost arriving after stock partly sold (allocate to remaining + COGS adjustment, or expense the sold portion); return of a batch that's partly consumed; payment exceeding payable (advance/on-account); rounding; trade vs cash discount; tax-inclusive supplier pricing; cancelling a posted invoice whose goods are already sold (block â€” must use return).

## 1.6 Operational constraints
- Base currency for GL; FX invoices convert at posting using `sys_currency.exchange_rate`.
- `is_grn_required` (company/branch config) chooses two-step vs one-step receiving.
- Receiving allowed only against an **Approved** PO when `po_required=1`; otherwise direct invoice permitted.
- Period must be open.

## 1.7 SME examples
Grocery buying on 30-day credit from distributors, FIFO costing, frequent partial returns of expired stock as debit notes; electronics importer with FX POs + landed cost (freight/duty) raising unit cost; pharmacy receiving batch+expiry per line with strict GRN; hardware store paying suppliers weekly with FIFO allocation across many small bills.

---

# 2. DATABASE DESIGN

## 2.1 `pur_supplier` â€” supplier master (PUR_1001)
**Purpose:** vendor master with terms, running payable, tax IDs, banking. **Scope:** company-wide; `branch_no` = registration branch.
```sql
CREATE TABLE pur_supplier (
    supplier_no      BIGSERIAL PRIMARY KEY,
    company_no       BIGINT      NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no        BIGINT      NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    supplier_id      VARCHAR(30) NOT NULL,
    supplier_name    VARCHAR(200) NOT NULL,
    supplier_name_nls VARCHAR(200),
    supplier_type    SMALLINT    NOT NULL DEFAULT 1,   -- 1=Distributor,2=Manufacturer,3=Importer,4=LocalVendor,5=Service
    contact_person   VARCHAR(150),
    mobile_no        VARCHAR(20), alt_mobile_no VARCHAR(20),
    phone_no         VARCHAR(25), email VARCHAR(150), website VARCHAR(150),
    address_line1    VARCHAR(250), address_line2 VARCHAR(250),
    city VARCHAR(100), state_province VARCHAR(100), post_code VARCHAR(20), country_code VARCHAR(10),
    vat_reg_no       VARCHAR(50), tin_no VARCHAR(50), bin_no VARCHAR(50), trade_license_no VARCHAR(100),
    -- terms
    payment_terms    SMALLINT    NOT NULL DEFAULT 1,   -- 1=Cash,2=Credit,3=Advance,4=Consignment
    credit_days      INTEGER     NOT NULL DEFAULT 0,
    credit_limit     NUMERIC(20,4) NOT NULL DEFAULT 0,
    default_currency_no BIGINT   REFERENCES sys_currency(currency_no) ON DELETE SET NULL,
    default_vat_tax_no  BIGINT   REFERENCES sys_vat_tax(vat_tax_no) ON DELETE SET NULL,
    opening_balance  NUMERIC(20,4) NOT NULL DEFAULT 0, -- + = we owe supplier
    opening_balance_date DATE,
    current_payable  NUMERIC(20,4) NOT NULL DEFAULT 0, -- maintained by AP ledger postings
    -- banking
    bank_name VARCHAR(100), bank_account_no VARCHAR(40), bank_branch VARCHAR(100), routing_no VARCHAR(30),
    mfs_provider VARCHAR(30), mfs_number VARCHAR(20),
    lead_time_days INTEGER, rating SMALLINT,
    image_path VARCHAR(255), remarks TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_pur_sup_type   CHECK (supplier_type IN (1,2,3,4,5)),
    CONSTRAINT chk_pur_sup_terms  CHECK (payment_terms IN (1,2,3,4)),
    CONSTRAINT chk_pur_sup_credit CHECK (credit_limit >= 0 AND credit_days >= 0),
    CONSTRAINT chk_pur_sup_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_pur_sup_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_pur_sup_id     ON pur_supplier(company_no, supplier_id) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_pur_sup_mobile ON pur_supplier(company_no, mobile_no)   WHERE is_deleted = 0 AND mobile_no IS NOT NULL;
CREATE INDEX idx_pur_sup_name_trgm ON pur_supplier USING gin (supplier_name gin_trgm_ops);
CREATE INDEX idx_pur_sup_branch ON pur_supplier(branch_no) WHERE is_deleted = 0;
CREATE INDEX idx_pur_sup_payable ON pur_supplier(company_no) WHERE current_payable > 0 AND is_deleted = 0;
```
**Rules:** `current_payable` is derived state (AP ledger only). Delete requires zero payable & no documents (RESTRICT) â†’ discontinue. The `preferred_supplier_no`/`supplier_no` referenced by `inv_reorder`, `inv_batch`, `inv_serial` resolve here.

## 2.2 `pur_supplier_product` â€” supplier price list / preferred mapping (optional)
```sql
CREATE TABLE pur_supplier_product (
    supplier_product_no BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    supplier_no  BIGINT NOT NULL REFERENCES pur_supplier(supplier_no) ON DELETE RESTRICT,
    product_no   BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no   BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    supplier_sku VARCHAR(60),
    purchase_uom_no BIGINT REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    last_price   NUMERIC(20,6), agreed_price NUMERIC(20,6),
    lead_time_days INTEGER, min_order_qty NUMERIC(18,4),
    is_preferred SMALLINT NOT NULL DEFAULT 0,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_pur_supprod_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_pur_supprod_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_pur_supprod ON pur_supplier_product(supplier_no, product_no, COALESCE(variant_no,0)) WHERE is_deleted = 0;
CREATE INDEX idx_pur_supprod_product ON pur_supplier_product(product_no);
```

## 2.3 `pur_order` (+ `_dtl`) â€” Purchase Order (PUR_1101)
**Purpose:** procurement commitment with approval; tracks ordered vs received. No stock/GL impact until receipt.
```sql
CREATE TABLE pur_order (
    order_no      BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no     BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    order_id      VARCHAR(40) NOT NULL,             -- sys_doc_sequence PUR_PO
    order_date    DATE NOT NULL,
    supplier_no   BIGINT NOT NULL REFERENCES pur_supplier(supplier_no) ON DELETE RESTRICT,
    warehouse_no  BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT, -- intended receiving wh
    expected_date DATE,
    currency_no   BIGINT REFERENCES sys_currency(currency_no) ON DELETE RESTRICT,
    exchange_rate NUMERIC(15,6) NOT NULL DEFAULT 1,
    status        SMALLINT NOT NULL DEFAULT 1,      -- 1=Draft,2=Submitted,3=Approved,4=PartiallyReceived,5=Received,6=Closed,7=Cancelled
    sub_total     NUMERIC(20,4) NOT NULL DEFAULT 0,
    discount_total NUMERIC(20,4) NOT NULL DEFAULT 0,
    tax_amount    NUMERIC(20,4) NOT NULL DEFAULT 0,
    shipping_estimate NUMERIC(20,4) NOT NULL DEFAULT 0,
    grand_total   NUMERIC(20,4) NOT NULL DEFAULT 0,
    received_value NUMERIC(20,4) NOT NULL DEFAULT 0,
    fin_year_no   BIGINT NOT NULL REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    submitted_by BIGINT, submitted_at TIMESTAMPTZ,
    approved_by  BIGINT, approved_at TIMESTAMPTZ,
    terms_note TEXT, remarks TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_pur_po_status CHECK (status IN (1,2,3,4,5,6,7)),
    CONSTRAINT chk_pur_po_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_pur_po_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_pur_po_id ON pur_order(branch_no, order_id) WHERE is_deleted = 0;
CREATE INDEX idx_pur_po_supplier ON pur_order(supplier_no, order_date);
CREATE INDEX idx_pur_po_status ON pur_order(branch_no, status) WHERE is_deleted = 0;
CREATE INDEX idx_pur_po_open ON pur_order(warehouse_no) WHERE status IN (3,4) AND is_deleted = 0;  -- for on-order qty

CREATE TABLE pur_order_dtl (
    order_dtl_no BIGSERIAL PRIMARY KEY,
    order_no     BIGINT NOT NULL REFERENCES pur_order(order_no) ON DELETE CASCADE,
    line_no      INTEGER NOT NULL,
    product_no   BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no   BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    uom_no       BIGINT NOT NULL REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    order_qty    NUMERIC(18,4) NOT NULL,
    order_qty_base NUMERIC(18,4) NOT NULL,
    received_qty_base NUMERIC(18,4) NOT NULL DEFAULT 0,  -- running, for partial receipts
    unit_price   NUMERIC(20,6) NOT NULL,
    discount_pct NUMERIC(5,2) NOT NULL DEFAULT 0,
    discount_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    vat_tax_no   BIGINT REFERENCES sys_vat_tax(vat_tax_no) ON DELETE RESTRICT,
    tax_rate_pct NUMERIC(5,2) NOT NULL DEFAULT 0,
    tax_amount   NUMERIC(20,4) NOT NULL DEFAULT 0,
    line_total   NUMERIC(20,4) NOT NULL DEFAULT 0,
    remarks VARCHAR(250),
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_pur_po_line UNIQUE (order_no, line_no),
    CONSTRAINT chk_pur_podtl_qty CHECK (order_qty > 0)
);
CREATE INDEX idx_pur_podtl_hdr ON pur_order_dtl(order_no);
CREATE INDEX idx_pur_podtl_product ON pur_order_dtl(product_no, variant_no);
```

## 2.4 `pur_receipt` (+ `_dtl`) â€” Goods Receipt Note / GRN (PUR_1105, optional)
**Purpose:** physical receipt that **increases stock** at provisional cost (Dr Inventory, Cr GRN-Clearing) before the invoice arrives. Captures batch/expiry. Skipped in one-step mode.
```sql
CREATE TABLE pur_receipt (
    receipt_no    BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no     BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    receipt_id    VARCHAR(40) NOT NULL,            -- PUR_GRN
    receipt_date  DATE NOT NULL,
    supplier_no   BIGINT NOT NULL REFERENCES pur_supplier(supplier_no) ON DELETE RESTRICT,
    order_no      BIGINT REFERENCES pur_order(order_no) ON DELETE RESTRICT,   -- nullable: receipt w/o PO
    warehouse_no  BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    supplier_challan_no VARCHAR(60), supplier_challan_date DATE,
    status        SMALLINT NOT NULL DEFAULT 1,     -- 1=Draft,2=Posted,3=Invoiced,4=Cancelled
    total_qty_base NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_value   NUMERIC(20,4) NOT NULL DEFAULT 0,
    fin_year_no   BIGINT NOT NULL REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    gl_voucher_no BIGINT, posted_at TIMESTAMPTZ, remarks TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_pur_grn_status CHECK (status IN (1,2,3,4)),
    CONSTRAINT chk_pur_grn_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_pur_grn_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_pur_grn_id ON pur_receipt(branch_no, receipt_id) WHERE is_deleted = 0;
CREATE INDEX idx_pur_grn_order ON pur_receipt(order_no);
CREATE INDEX idx_pur_grn_supplier ON pur_receipt(supplier_no, receipt_date);

CREATE TABLE pur_receipt_dtl (
    receipt_dtl_no BIGSERIAL PRIMARY KEY,
    receipt_no   BIGINT NOT NULL REFERENCES pur_receipt(receipt_no) ON DELETE CASCADE,
    line_no      INTEGER NOT NULL,
    order_dtl_no BIGINT REFERENCES pur_order_dtl(order_dtl_no) ON DELETE RESTRICT,
    product_no   BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no   BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    uom_no       BIGINT NOT NULL REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    received_qty NUMERIC(18,4) NOT NULL,
    received_qty_base NUMERIC(18,4) NOT NULL,
    accepted_qty_base NUMERIC(18,4) NOT NULL,       -- accepted = received âˆ’ rejected
    rejected_qty_base NUMERIC(18,4) NOT NULL DEFAULT 0,
    unit_cost    NUMERIC(20,6) NOT NULL,            -- provisional (PO price)
    line_value   NUMERIC(20,4) NOT NULL DEFAULT 0,
    -- batch/expiry capture
    batch_code   VARCHAR(60), mfg_date DATE, expiry_date DATE,
    batch_no     BIGINT REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,  -- resolved/created on post
    rack_no      BIGINT REFERENCES inv_rack(rack_no) ON DELETE RESTRICT,
    remarks VARCHAR(250),
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_pur_grn_line UNIQUE (receipt_no, line_no),
    CONSTRAINT chk_pur_grndtl_qty CHECK (received_qty > 0)
);
CREATE INDEX idx_pur_grndtl_hdr ON pur_receipt_dtl(receipt_no);
```

## 2.5 `pur_invoice` (+ `_dtl`) â€” Purchase Invoice / Bill (PUR_1102)
**Purpose:** the supplier bill that creates the **payable**. In one-step mode it also **receives stock**; in two-step mode it matches GRN(s) and clears GRN-Clearing.
```sql
CREATE TABLE pur_invoice (
    invoice_no    BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no     BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    invoice_id    VARCHAR(40) NOT NULL,            -- our internal id (PUR_INV)
    supplier_invoice_no VARCHAR(60) NOT NULL,      -- supplier's bill number (dup-checked per supplier)
    invoice_date  DATE NOT NULL,
    supplier_no   BIGINT NOT NULL REFERENCES pur_supplier(supplier_no) ON DELETE RESTRICT,
    order_no      BIGINT REFERENCES pur_order(order_no) ON DELETE RESTRICT,
    warehouse_no  BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    receive_mode  SMALLINT NOT NULL DEFAULT 1,     -- 1=OneStep(receives stock),2=AgainstGRN(stock already in)
    currency_no   BIGINT REFERENCES sys_currency(currency_no) ON DELETE RESTRICT,
    exchange_rate NUMERIC(15,6) NOT NULL DEFAULT 1,
    status        SMALLINT NOT NULL DEFAULT 1,     -- 1=Draft,2=Posted,3=PartiallyReturned,4=Returned,5=Cancelled
    sub_total     NUMERIC(20,4) NOT NULL DEFAULT 0,
    discount_total NUMERIC(20,4) NOT NULL DEFAULT 0,
    taxable_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    tax_amount    NUMERIC(20,4) NOT NULL DEFAULT 0,  -- input VAT
    shipping_charge NUMERIC(20,4) NOT NULL DEFAULT 0,
    other_charges NUMERIC(20,4) NOT NULL DEFAULT 0,
    landed_cost_total NUMERIC(20,4) NOT NULL DEFAULT 0,
    round_off     NUMERIC(20,4) NOT NULL DEFAULT 0,
    grand_total   NUMERIC(20,4) NOT NULL DEFAULT 0,
    paid_amount   NUMERIC(20,4) NOT NULL DEFAULT 0,
    due_amount    NUMERIC(20,4) NOT NULL DEFAULT 0,
    returned_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    payment_status SMALLINT NOT NULL DEFAULT 1,     -- 1=Unpaid,2=Partial,3=Paid
    due_date      DATE,
    fin_year_no   BIGINT NOT NULL REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    gl_voucher_no BIGINT, posted_at TIMESTAMPTZ,
    cancelled_by BIGINT, cancelled_at TIMESTAMPTZ, cancel_reason VARCHAR(250),
    remarks TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_pur_inv_mode   CHECK (receive_mode IN (1,2)),
    CONSTRAINT chk_pur_inv_status CHECK (status IN (1,2,3,4,5)),
    CONSTRAINT chk_pur_inv_paystat CHECK (payment_status IN (1,2,3)),
    CONSTRAINT chk_pur_inv_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_pur_inv_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_pur_inv_id  ON pur_invoice(branch_no, invoice_id) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_pur_inv_sup ON pur_invoice(supplier_no, supplier_invoice_no) WHERE is_deleted = 0;  -- dup bill guard
CREATE INDEX idx_pur_inv_supplier ON pur_invoice(supplier_no, invoice_date);
CREATE INDEX idx_pur_inv_status ON pur_invoice(branch_no, status) WHERE is_deleted = 0;
CREATE INDEX idx_pur_inv_due ON pur_invoice(supplier_no) WHERE due_amount > 0 AND is_deleted = 0;

CREATE TABLE pur_invoice_dtl (
    invoice_dtl_no BIGSERIAL PRIMARY KEY,
    invoice_no   BIGINT NOT NULL REFERENCES pur_invoice(invoice_no) ON DELETE CASCADE,
    line_no      INTEGER NOT NULL,
    order_dtl_no   BIGINT REFERENCES pur_order_dtl(order_dtl_no) ON DELETE RESTRICT,
    receipt_dtl_no BIGINT REFERENCES pur_receipt_dtl(receipt_dtl_no) ON DELETE RESTRICT,  -- two-step link
    product_no   BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no   BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    uom_no       BIGINT NOT NULL REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    qty          NUMERIC(18,4) NOT NULL,
    qty_base     NUMERIC(18,4) NOT NULL,
    unit_price   NUMERIC(20,6) NOT NULL,
    discount_pct NUMERIC(5,2) NOT NULL DEFAULT 0,
    discount_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    vat_tax_no   BIGINT REFERENCES sys_vat_tax(vat_tax_no) ON DELETE RESTRICT,
    tax_rate_pct NUMERIC(5,2) NOT NULL DEFAULT 0,
    is_tax_inclusive SMALLINT NOT NULL DEFAULT 0,
    taxable_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    tax_amount   NUMERIC(20,4) NOT NULL DEFAULT 0,
    line_total   NUMERIC(20,4) NOT NULL DEFAULT 0,
    landed_cost_alloc NUMERIC(20,4) NOT NULL DEFAULT 0,   -- freight/duty allocated to this line
    final_unit_cost NUMERIC(20,6) NOT NULL DEFAULT 0,     -- (line_total ex-tax + landed alloc)/qty_base â†’ stock cost
    -- batch/expiry (one-step receiving)
    batch_code VARCHAR(60), mfg_date DATE, expiry_date DATE,
    batch_no   BIGINT REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,
    rack_no    BIGINT REFERENCES inv_rack(rack_no) ON DELETE RESTRICT,
    returned_qty_base NUMERIC(18,4) NOT NULL DEFAULT 0,
    remarks VARCHAR(250),
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_pur_inv_line UNIQUE (invoice_no, line_no),
    CONSTRAINT chk_pur_invdtl_qty CHECK (qty > 0)
);
CREATE INDEX idx_pur_invdtl_hdr ON pur_invoice_dtl(invoice_no);
CREATE INDEX idx_pur_invdtl_product ON pur_invoice_dtl(product_no, variant_no);
```

## 2.6 `pur_return` (+ `_dtl`) â€” Purchase Return / Debit Note (PUR_1103)
```sql
CREATE TABLE pur_return (
    return_no    BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no    BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    return_id    VARCHAR(40) NOT NULL,             -- PUR_RET / Debit Note
    return_date  DATE NOT NULL,
    supplier_no  BIGINT NOT NULL REFERENCES pur_supplier(supplier_no) ON DELETE RESTRICT,
    original_invoice_no BIGINT REFERENCES pur_invoice(invoice_no) ON DELETE RESTRICT,  -- NULL = blind (approval)
    warehouse_no BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    return_reason SMALLINT,         -- 1=Defective,2=Expired,3=Excess,4=WrongItem,5=PriceDispute,6=Other
    status       SMALLINT NOT NULL DEFAULT 1,      -- 1=Draft,2=Approved/Posted,3=Cancelled
    settlement_mode SMALLINT NOT NULL DEFAULT 1,   -- 1=AdjustPayable(DebitNote),2=CashRefund,3=Replacement
    sub_total    NUMERIC(20,4) NOT NULL DEFAULT 0,
    tax_amount   NUMERIC(20,4) NOT NULL DEFAULT 0,
    round_off    NUMERIC(20,4) NOT NULL DEFAULT 0,
    grand_total  NUMERIC(20,4) NOT NULL DEFAULT 0,
    fin_year_no  BIGINT NOT NULL REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    gl_voucher_no BIGINT, approved_by BIGINT, approved_at TIMESTAMPTZ, posted_at TIMESTAMPTZ, remarks TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_pur_ret_status CHECK (status IN (1,2,3)),
    CONSTRAINT chk_pur_ret_settle CHECK (settlement_mode IN (1,2,3)),
    CONSTRAINT chk_pur_ret_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_pur_ret_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_pur_ret_id ON pur_return(branch_no, return_id) WHERE is_deleted = 0;
CREATE INDEX idx_pur_ret_supplier ON pur_return(supplier_no, return_date);
CREATE INDEX idx_pur_ret_orig ON pur_return(original_invoice_no);

CREATE TABLE pur_return_dtl (
    return_dtl_no BIGSERIAL PRIMARY KEY,
    return_no    BIGINT NOT NULL REFERENCES pur_return(return_no) ON DELETE CASCADE,
    line_no      INTEGER NOT NULL,
    original_invoice_dtl_no BIGINT REFERENCES pur_invoice_dtl(invoice_dtl_no) ON DELETE RESTRICT,
    product_no   BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no   BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    batch_no     BIGINT REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,
    uom_no       BIGINT NOT NULL REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    qty          NUMERIC(18,4) NOT NULL,
    qty_base     NUMERIC(18,4) NOT NULL,
    unit_cost    NUMERIC(20,6) NOT NULL,           -- cost goods leave at (original landed cost)
    tax_rate_pct NUMERIC(5,2) NOT NULL DEFAULT 0,
    tax_amount   NUMERIC(20,4) NOT NULL DEFAULT 0,
    line_total   NUMERIC(20,4) NOT NULL DEFAULT 0,
    remarks VARCHAR(250),
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_pur_ret_line UNIQUE (return_no, line_no),
    CONSTRAINT chk_pur_retdtl_qty CHECK (qty > 0)
);
CREATE INDEX idx_pur_retdtl_hdr ON pur_return_dtl(return_no);
```

## 2.7 `pur_payment` (+ `pur_payment_alloc`) â€” Supplier Payment (PUR_1104)
```sql
CREATE TABLE pur_payment (
    payment_no   BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no    BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    payment_id   VARCHAR(40) NOT NULL,
    payment_date DATE NOT NULL,
    supplier_no  BIGINT NOT NULL REFERENCES pur_supplier(supplier_no) ON DELETE RESTRICT,
    payment_method SMALLINT NOT NULL,   -- 1=Cash,2=BankTransfer,3=Cheque,4=MobileBanking,5=Card,6=Adjustment(DebitNote)
    amount       NUMERIC(20,4) NOT NULL,
    allocated_amount NUMERIC(20,4) NOT NULL DEFAULT 0,
    unallocated_amount NUMERIC(20,4) NOT NULL DEFAULT 0,  -- advance to supplier
    currency_no  BIGINT REFERENCES sys_currency(currency_no) ON DELETE RESTRICT,
    exchange_rate NUMERIC(15,6) NOT NULL DEFAULT 1,
    bank_no BIGINT, cheque_no VARCHAR(40), cheque_date DATE, txn_ref VARCHAR(80),
    gl_account_no BIGINT,                 -- cash/bank credited
    status       SMALLINT NOT NULL DEFAULT 2,    -- 1=Draft,2=Posted,3=Cancelled
    fin_year_no  BIGINT NOT NULL REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    gl_voucher_no BIGINT, posted_at TIMESTAMPTZ, remarks TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_pur_pay_method CHECK (payment_method IN (1,2,3,4,5,6)),
    CONSTRAINT chk_pur_pay_status CHECK (status IN (1,2,3)),
    CONSTRAINT chk_pur_pay_amt CHECK (amount > 0),
    CONSTRAINT chk_pur_pay_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_pur_pay_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_pur_pay_id ON pur_payment(branch_no, payment_id) WHERE is_deleted = 0;
CREATE INDEX idx_pur_pay_supplier ON pur_payment(supplier_no, payment_date);

CREATE TABLE pur_payment_alloc (
    payment_alloc_no BIGSERIAL PRIMARY KEY,
    payment_no   BIGINT NOT NULL REFERENCES pur_payment(payment_no) ON DELETE CASCADE,
    invoice_no   BIGINT REFERENCES pur_invoice(invoice_no) ON DELETE RESTRICT,
    return_no    BIGINT REFERENCES pur_return(return_no) ON DELETE RESTRICT,  -- apply a debit note
    allocated_amount NUMERIC(20,4) NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_pur_payalloc_amt CHECK (allocated_amount <> 0),
    CONSTRAINT chk_pur_payalloc_target CHECK (invoice_no IS NOT NULL OR return_no IS NOT NULL)
);
CREATE INDEX idx_pur_payalloc_inv ON pur_payment_alloc(invoice_no);
CREATE INDEX idx_pur_payalloc_pay ON pur_payment_alloc(payment_no);
```

## 2.8 `pur_supplier_ledger` â€” AP subsidiary ledger (payable tracking)
```sql
CREATE TABLE pur_supplier_ledger (
    supplier_ledger_no BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL,
    branch_no    BIGINT NOT NULL,
    supplier_no  BIGINT NOT NULL REFERENCES pur_supplier(supplier_no) ON DELETE RESTRICT,
    txn_date     DATE NOT NULL,
    ref_doc_type SMALLINT NOT NULL,   -- 1=Opening,2=Invoice,3=Payment,4=Return/DebitNote,5=Adjustment
    ref_doc_no   VARCHAR(40) NOT NULL,
    ref_doc_pk   BIGINT,
    credit       NUMERIC(20,4) NOT NULL DEFAULT 0,   -- increases payable (invoice)
    debit        NUMERIC(20,4) NOT NULL DEFAULT 0,   -- decreases payable (payment/return)
    balance_after NUMERIC(20,4) NOT NULL,
    fin_year_no  BIGINT REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    remarks VARCHAR(250),
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_pur_supled_type CHECK (ref_doc_type IN (1,2,3,4,5)),
    CONSTRAINT chk_pur_supled_amt CHECK (debit >= 0 AND credit >= 0)
);
CREATE INDEX idx_pur_supled_sup ON pur_supplier_ledger(supplier_no, txn_date, supplier_ledger_no);
CREATE INDEX idx_pur_supled_ref ON pur_supplier_ledger(ref_doc_type, ref_doc_no);
```
Append-only; `pur_supplier.current_payable` = last `balance_after`.

## 2.9 `pur_landed_cost` (+ `_alloc`) â€” landed cost allocation (PUR_1106, optional)
**Purpose:** add freight/duty/clearing to received goods cost, allocated across invoice lines by value/qty/weight.
```sql
CREATE TABLE pur_landed_cost (
    landed_cost_no BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no    BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    landed_cost_id VARCHAR(40) NOT NULL,
    cost_date    DATE NOT NULL,
    invoice_no   BIGINT REFERENCES pur_invoice(invoice_no) ON DELETE RESTRICT,
    receipt_no   BIGINT REFERENCES pur_receipt(receipt_no) ON DELETE RESTRICT,
    cost_type    SMALLINT NOT NULL,   -- 1=Freight,2=CustomsDuty,3=Clearing,4=Insurance,5=Handling,6=Other
    vendor_no    BIGINT REFERENCES pur_supplier(supplier_no) ON DELETE RESTRICT,
    amount       NUMERIC(20,4) NOT NULL,
    alloc_basis  SMALLINT NOT NULL DEFAULT 1,  -- 1=ByValue,2=ByQty,3=ByWeight
    status       SMALLINT NOT NULL DEFAULT 1,  -- 1=Draft,2=Applied,3=Cancelled
    gl_voucher_no BIGINT, remarks TEXT,
    -- << AUDIT BLOCK >>
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_pur_lc_type CHECK (cost_type IN (1,2,3,4,5,6)),
    CONSTRAINT chk_pur_lc_basis CHECK (alloc_basis IN (1,2,3)),
    CONSTRAINT chk_pur_lc_status CHECK (status IN (1,2,3)),
    CONSTRAINT chk_pur_lc_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_pur_lc_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_pur_lc_id ON pur_landed_cost(branch_no, landed_cost_id) WHERE is_deleted = 0;

CREATE TABLE pur_landed_cost_alloc (
    landed_cost_alloc_no BIGSERIAL PRIMARY KEY,
    landed_cost_no BIGINT NOT NULL REFERENCES pur_landed_cost(landed_cost_no) ON DELETE CASCADE,
    invoice_dtl_no BIGINT NOT NULL REFERENCES pur_invoice_dtl(invoice_dtl_no) ON DELETE RESTRICT,
    allocated_amount NUMERIC(20,4) NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_pur_lc_alloc UNIQUE (landed_cost_no, invoice_dtl_no)
);
```

## 2.10 Relationship summary
`pur_supplier` (1â€”N) `pur_order`/`pur_receipt`/`pur_invoice`/`pur_return`/`pur_payment`. PO (1â€”N `pur_order_dtl`); receipt links PO line; invoice links PO line and/or GRN line; return links invoice line. `pur_payment` (1â€”N `pur_payment_alloc` â†’ invoice or return). `pur_supplier_ledger` N rows per supplier (AP history). `pur_landed_cost` (1â€”N alloc â†’ invoice line). Stock/cost via `inv_*` (creates `inv_batch`, posts movement 2/3); GL via `fin_*`; numbering via `sys_doc_sequence`; events via `sys_event_outbox`.

---

# 3. BUSINESS LOGIC

## 3.1 PO logic
PO totals computed server-side from lines (price âˆ’ discount + tax). Approval gate by value threshold (`can_approve`). `received_qty_base` per line incremented by GRN/invoice receipts; header status auto-advances Approvedâ†’PartiallyReceivedâ†’Received when all lines met (within over/under tolerance). Over-receipt beyond tolerance blocked unless `can_approve`. `on_order` qty for replenishment = Î£ `(order_qty_base âˆ’ received_qty_base)` over open POs.

## 3.2 Receiving & costing (the cost that flows to inventory)
**One-step (receive_mode=1, default):** on Posting the invoice, for each line compute `final_unit_cost = (line_total_ex_tax + landed_cost_alloc) / qty_base`; resolve/create `inv_batch` (batch_code+mfg+expiry) for batch-tracked products; call `InvStockPostingService` movement_type=2 (IN) at `final_unit_cost`. The engine updates `inv_stock.avg_cost` (weighted) or creates FIFO/LIFO layers. Update PO `received_qty_base`.
**Two-step (receive_mode=2):** GRN already posted stock at provisional PO cost (Dr Inventory, Cr GRN-Clearing). The invoice does **not** re-receive stock; it clears GRN-Clearing and posts AP. If invoice price â‰  GRN cost â†’ **purchase price variance**: adjust inventory value for on-hand portion (revaluation ledger) and post variance to PPV account for the consumed portion.
**Landed cost (PUR_1106):** allocate `amount` across lines by basis â†’ bump `final_unit_cost` â†’ engine writes value-only revaluation to `inv_stock`/layers for on-hand qty; portion attributable to already-sold qty posts to COGS/landed-cost-variance.

## 3.3 State machines
**Invoice:** `Draft â†’ Posted (stock IN if one-step + AP + input VAT + GL) â†’ PartiallyReturned/Returned (via returns) / Cancelled(reversed)`. Cannot cancel if goods already sold (block â†’ use return). Cancel writes reversal stock legs + reversing voucher + reversing AP ledger.
**GRN:** `Draft â†’ Posted(stock IN, GRN-Clearing) â†’ Invoiced (linked) â†’ Cancelled(reversed)`.
**Return:** `Draft â†’ Approved/Posted` â†’ stock OUT (movement 3) at original landed cost; AP debit (debit note) or cash refund; GL reversal. Validate `qty <= received âˆ’ already_returned` against the original invoice line.
**Payment:** `Draft â†’ Posted` â†’ AP debit + cash/bank credit; allocations reduce `pur_invoice.due_amount`; advances â†’ `unallocated_amount`. Cancel reverses.

## 3.4 Ledger impact (GL posting matrix â†’ FIN module)
| Event | Debit | Credit |
|---|---|---|
| GRN (two-step) | Inventory | GRN-Clearing (accrued) |
| Purchase Invoice (one-step) | Inventory; Input VAT Receivable | Accounts Payable |
| Purchase Invoice (two-step) | GRN-Clearing; Input VAT Receivable; PPV (if variance) | Accounts Payable |
| Landed cost | Inventory (on-hand share); COGS/Variance (sold share); Input VAT (if any) | AP / Cash (to logistics vendor) |
| Purchase Return / Debit Note | Accounts Payable (or Cash if refund) | Inventory; Input VAT (reversal) |
| Supplier Payment | Accounts Payable | Cash / Bank |
| Advance to supplier | Supplier Advance (asset) | Cash / Bank |
All vouchers balance (Î£dr=Î£cr); accounts resolved from `fin_account` mapping (product category â†’ inventory; tax â†’ `sys_vat_tax.gl_account_no`; supplier control â†’ AP; tender â†’ bank/cash). Posting synchronous or via outboxâ†’GL-poster (idempotent on `ref_doc_no`).

## 3.5 Transaction safety, concurrency, idempotency, rollback
- One tx per posted document (stock + AP ledger + GL + PO update + outbox).
- Concurrency: stock via engine `FOR UPDATE`; header optimistic `row_version`; doc number atomic; PO `received_qty_base` updated under row lock to serialize concurrent receipts.
- Idempotency: duplicate supplier bill blocked by `uq_pur_inv_sup`; posting keyed by `(ref_doc_type, ref_doc_no)`; `Idempotency-Key` header for retries.
- Rollback: insufficient validation (closed period, over-receipt without approval, unbalanced voucher, dup bill) aborts whole post.

## 3.6 FX, batch/expiry, unit conversion
FX invoices store doc-currency amounts + `exchange_rate`; base-currency cost posted to stock/GL; rate change between PO and receipt â†’ exchange variance to gain/loss. Batch/expiry captured at receipt â†’ `inv_batch`. `qty_base` normalized server-side via `inv_uom_conversion` (buy in CARTON, stock in PCS).

---

# 4. FRONTEND IMPLEMENTATION PLAN

## 4.1 Screens / routes (`pur.routes.ts`, lazy)
| Form | Route | Pattern |
|---|---|---|
| PUR_1001 Supplier | `pur/forms/pur1001` | master-detail (list + tabs General/Terms/Banking/Ledger) |
| PUR_1101 Purchase Order | `pur/forms/pur1101` | document + approval |
| PUR_1102 Purchase Invoice | `pur/forms/pur1102` | document (receive + bill), PO/GRN pull |
| PUR_1103 Purchase Return | `pur/forms/pur1103` | invoice lookup â†’ return |
| PUR_1104 Supplier Payment | `pur/forms/pur1104` | supplierâ†’billsâ†’allocate |
| PUR_1105 GRN (optional) | `pur/forms/pur1105` | receive against PO |
| PUR_1106 Landed Cost (optional) | `pur/forms/pur1106` | allocate charges |
| Supplier/PO/Invoice lists & purchase report | `pur/pages/*` | grids |

## 4.2 PUR_1102 Purchase Invoice (reference screen)
- **Layout:** `app-page-form-layout`; header card (supplier, supplier-invoice-no+date, warehouse, currency, due date, receive_mode) + lines grid (`app-common-table`, editable) + totals/landed-cost/payment panel.
- **Pull from PO/GRN:** "Load PO" / "Load GRN" buttons open a `data-list-modal` of open POs/GRNs for the supplier; selecting copies remaining lines (qty defaults to outstanding). Lines show ordered vs receiving qty with variance highlight.
- **Per-line batch/expiry:** when product `is_batch_tracked`, inline batch_code + mfg/expiry inputs (required); expiry-tracked enforces expiry. `final_unit_cost` shown read-only (computed).
- **Signals/forms:** `FormGroup` + `FormArray lines`; signals `isLoading/isSaving/poList`; snake_case fields. Server re-computes totals on `quote`/save.
- **Payment-on-receipt:** optional immediate payment block (method/amount) â†’ creates linked `pur_payment` in same flow; remainder becomes due.
- **Validation/Error UX:** duplicate supplier-invoice-no â†’ inline error (server 400); over-receipt beyond tolerance â†’ approval prompt; closed period â†’ blocked toast. 409 â†’ reload.
- **Permission rendering:** Post hidden unless `can_insert`; variance/over-receipt approval gated by `canApprove()`.
- **Keyboard shortcuts:** `F2` add line, `F3` supplier, `F4` load PO, `Ctrl+S` save draft, `Ctrl+Enter` post.
- **Printing:** A4 purchase invoice / GRN note / debit note / payment voucher; print stylesheet.

## 4.3 PUR_1101 PO
Header + lines (product picker, qty, price from `pur_supplier_product.last_price`, discount, tax); workflow buttons Save/Submit/Approve/Cancel by status; print PO; "Convert to Invoice/GRN" action.

## 4.4 PUR_1104 Supplier Payment
Select supplier â†’ grid of open bills (due, age) + allocate inputs + "Auto-allocate (FIFO)"; apply debit notes (`pur_return`) as negative allocations; method/amount/bank/cheque; print payment voucher. Advance handling shows `unallocated_amount`.

## 4.5 PUR_1001 Supplier (master-detail)
Left list (search, payable badge); right tabs incl. **Ledger** tab rendering `pur_supplier_ledger` running balance + aging. `current_payable` read-only.

## 4.6 Reusable / responsive
Shared: `pur-supplier-picker`, `inv-product-picker` (reused), `po-pull-modal`, `batch-input`, `aging-badge`, `payment-allocation-grid`. Desktop-grid forms; tablet-friendly receiving screen for warehouse. Permission-aware buttons throughout.

---

# 5. BACKEND & MIDDLEWARE PLAN

## 5.1 Endpoints (form-wise, `/api/v1/pur/forms/{formId}`)
**Supplier PUR_1001:** `GET/POST /suppliers`, `/suppliers/page`, `/suppliers/{no}`, `/suppliers/{no}/ledger`, `DELETE /suppliers/{no}`.
**PO PUR_1101:** `GET /orders`, `/orders/page`, `/orders/{no}`, `POST /orders` (draft upsert), `/orders/{no}/submit`, `/orders/{no}/approve`, `/orders/{no}/cancel`, `GET /orders/open?supplierNo`.
**Invoice PUR_1102:** `GET /invoices`, `/invoices/{no}`, `POST /invoices` (draft), `POST /invoices/{no}/post`, `POST /invoices/{no}/cancel`, `GET /po/{orderNo}/pull`, `GET /grn/{receiptNo}/pull`, `POST /invoices/quote` (server totals).
**Return PUR_1103:** `GET /returns`, `GET /invoices/{invoiceNo}/returnable`, `POST /returns`, `/returns/{no}/approve`, `/returns/{no}/cancel`.
**Payment PUR_1104:** `GET /suppliers/{no}/open-invoices`, `POST /payments`, `POST /payments/auto-allocate`, `/payments/{no}/cancel`.
**GRN PUR_1105:** `GET/POST /receipts`, `/receipts/{no}/post`, `/receipts/{no}/cancel`. **Landed PUR_1106:** `POST /landed-costs`, `/landed-costs/{no}/apply`.
All `ApiResponse<T>`; lists asc by PK; `/page` variants.

## 5.2 DTOs (snake_case, validated)
```java
@Data public class Pur1102InvoiceDto {
  private Long invoice_no; private String invoice_id;
  @NotBlank private String supplier_invoice_no;
  @NotNull private LocalDate invoice_date;
  @NotNull private Long supplier_no; private Long order_no;
  @NotNull private Long warehouse_no; @NotNull private Short receive_mode;
  private Long currency_no; private BigDecimal exchange_rate;
  private BigDecimal shipping_charge; private BigDecimal other_charges; private BigDecimal round_off;
  private LocalDate due_date; private String remarks; private Long row_version;
  @NotEmpty @Valid private List<Pur1102InvoiceLineDto> lines;
  @Valid private Pur1102PaymentOnReceiptDto payment;  // optional
}
@Data public class Pur1102InvoiceLineDto {
  private Long invoice_dtl_no; private Integer line_no; private Long order_dtl_no; private Long receipt_dtl_no;
  @NotNull private Long product_no; private Long variant_no;
  @NotNull private Long uom_no; @NotNull @DecimalMin("0.0001") private BigDecimal qty;
  @NotNull private BigDecimal unit_price; private BigDecimal discount_pct;
  private Long vat_tax_no; private Short is_tax_inclusive;
  private String batch_code; private LocalDate mfg_date; private LocalDate expiry_date; private Long rack_no;
}
```
Server recomputes taxable/tax/line_total/final_unit_cost; MapStruct ignores audit fields per house rule.

## 5.3 Middleware / cross-cutting
Auth/tenant via `CompanyBranchContext`; RBAC by form_id (`PUR_1102`â€¦), `can_approve` for PO approval, over-receipt, variance, blind return, cancel posted. Inventory locking delegated to engine; header optimistic lock. Validation `@Valid`â†’400 + business `ValidationException` (dup bill, over-receipt, closed period). Audit via `sys_audit_log`. Idempotency via `uq_pur_inv_sup` + `Idempotency-Key`. Logging `@Slf4j`+`sys_log`. Errors via `GlobalExceptionHandler` (404/400/409/500). Retry on lock/serialization; outbox retry for GL/notify.

## 5.4 Queue / events
Outbox events: `PoApproved`, `GoodsReceived`, `PurchaseInvoicePosted`, `PurchaseReturnPosted`, `SupplierPaymentPosted`, `PriceVarianceDetected`, `LandedCostApplied`. Consumers: GL poster, AP/aging MV refresh, reorder/on-order recalculation, supplier-performance analytics, notifications (payment due reminders). Phase-1 Spring `@TransactionalEventListener(AFTER_COMMIT)`; broker-ready via outbox.

---

# 6. REPORTING & ANALYTICS
- **Reports:** purchase register (by supplier/product/category/date), PO status & pending-receipt, GRN register, **purchase return/debit-note register**, **supplier payable & aging (0-30/31-60/61-90/90+)**, payment register, price-variance (PO vs invoice), landed-cost analysis, supplier performance (lead-time, fill-rate, defect/return rate, price trend), input-VAT summary (for filing), top suppliers/products by spend, on-order vs reorder gap.
- **KPIs:** total payable & overdue, PO fill rate, avg lead time, purchase price variance %, return-to-supplier rate, on-time payment %, spend by category, advances outstanding.
- **Aggregation/MVs:** `mv_pur_supplier_aging` (refresh on InvoicePosted/PaymentPosted), `mv_pur_spend_by_category`, `mv_pur_supplier_performance`. AP balance from `pur_supplier_ledger` last `balance_after`; on-order from open POs.
- **Optimization:** indexes `idx_pur_inv_due`, `idx_pur_supled_sup`, `idx_pur_po_open`; reports off replica/MVs; stream exports.

---

# 7. SECURITY & COMPLIANCE
## 7.1 Permission matrix
| Form | view | insert | update | delete | approve |
|---|---|---|---|---|---|
| PUR_1001 Supplier | Purchaser | Purchaser | Purchaser | Admin | Manager (credit terms) |
| PUR_1101 PO | Purchaser | Purchaser | Purchaser(draft) | Admin | **Manager (approve)** |
| PUR_1102 Invoice | Clerk/Acct | Clerk/Acct | (draft) | Admin | Manager (variance/over-receipt) |
| PUR_1103 Return | Clerk | Clerk | (draft) | Admin | **Manager (post/blind)** |
| PUR_1104 Payment | Accountant | Accountant | (draft) | Admin | Manager (cancel) |
| PUR_1105 GRN | Receiving | Receiving | (draft) | Admin | Manager |

## 7.2 Access control & sensitive actions
Branch isolation via `sys_user_branch`/context. Manager approval (audited, with reason) for: PO over threshold, over-receipt beyond tolerance, price-variance beyond tolerance, blind return, cancel posted invoice/payment, supplier credit-term change. Banking fields and payment posting restricted to finance roles.
## 7.3 Audit & fraud prevention
Immutable `pur_supplier_ledger` + `inv_stock_ledger` + `sys_audit_log`. Fraud signals: duplicate/near-duplicate supplier bills, round-number invoices, price creep vs history, payments to suppliers with no receipts, split POs to dodge approval threshold, returns without goods movement â†’ exception dashboard. `uq_pur_inv_sup` prevents double-billing; three-way match (POâ†”GRNâ†”Invoice) flags mismatches.
## 7.4 Data validation
Client + API (`@Valid`) + DB (CHECK/UNIQUE/FK). Server is sole authority for costs, tax, totals, landed allocation, payable.

---

# 8. PERFORMANCE & SCALABILITY
- Indexes per FK + partial active; AP aging via MV; dup-bill unique index; `idx_pur_po_open` for on-order.
- Caching: supplier typeahead + `pur_supplier_product` price list in Redis (read-mostly), invalidated on save. Document writes never cached.
- Read/write split: reports on replica; postings on primary.
- Partition `pur_invoice`/`pur_invoice_dtl`/`pur_supplier_ledger` by date (monthly) at high volume.
- Receiving locks are per stock-cell (engine) â€” fine-grained; concurrent receipts to different SKUs don't contend.
- Horizontal scale: stateless JWT API. Multi-tenant: company/branch on every row; RLS-ready.

---

# 9. TESTING STRATEGY
- **Unit:** PO total/tax/discount math, over/under-receipt tolerance, price-variance calc, landed-cost allocation (value/qty/weight, sums to total), FX conversion, payable allocation FIFO, final_unit_cost computation, state-machine guards.
- **Integration (PG/Testcontainers):** post invoice (one-step) â†’ stock IN + batch created + AP ledger + GL voucher + PO received_qty + outbox atomic; two-step GRNâ†’invoice clears GRN-clearing with variance; rollback on dup bill/closed period/over-receipt; return reverses stock & AP; payment reduces dues; landed cost revalues on-hand.
- **E2E:** supplierâ†’POâ†’approveâ†’GRNâ†’invoiceâ†’landed costâ†’partial returnâ†’payment (FIFO); FX PO with rate change; direct invoice without PO.
- **Inventory consistency:** received qty/cost match `inv_stock_ledger`; weighted-avg/FIFO cost correct after multi-receipt; return restocks correct cost.
- **Concurrency:** parallel receipts against same PO line don't exceed ordered+tolerance; parallel payments don't over-allocate (`due >= 0`); doc-number uniqueness; dup-bill race blocked by unique index.
- **Financial integrity:** every invoice/return/payment voucher balances; `pur_supplier.current_payable == last pur_supplier_ledger.balance_after == Î£ invoice âˆ’ Î£ payment âˆ’ Î£ return` per supplier; AP control GL == Î£ supplier payables; inventory value increase == Î£ received line value.

---

# 10. PRODUCTION DEPLOYMENT NOTES
- **Migration:** Flyway `V{n}__pur_*.sql` after `inv_*`. Seed `sys_doc_sequence` rows (PUR_PO/PUR_GRN/PUR_INV/PUR_RET/PUR_PAY), `fin_account` mappings (inventory/GRN-clearing/input-VAT/AP/PPV/cash/supplier-advance), company config (`is_grn_required`, `po_required`, over/under-receipt tolerance, PO approval threshold). `pg_trgm` for supplier search.
- **Rollback:** down scripts; never drop posted ledgers/invoices once live â€” forward-fix. New forms gated via `sys_enroll_menu`.
- **Seed data:** payment-methodâ†’GL map, tax links to `sys_vat_tax`, sample supplier + supplier price list, default receiving warehouse.
- **Environment:** DB/JWT/Redis/broker vars; profiles; port 7860; CORS per backend `CLAUDE.md`.
- **Monitoring:** posting latency, outbox backlog, GL/AP posting failures, three-way-match exceptions, dup-bill rejections, lock waits, payable-aging alerts.
- **Backup/DR:** PITR + nightly dump; append-only ledgers ease consistency; AP rebuildable by replaying `pur_supplier_ledger`; inventory rebuildable from `inv_stock_ledger`; cross-region replica; documented RPO/RTO. Supplier bills/vouchers retained per tax-law retention.

---

## Appendix â€” Enums
`supplier_type`:1 DistributorÂ·2 ManufacturerÂ·3 ImporterÂ·4 LocalVendorÂ·5 Service | `payment_terms`:1 CashÂ·2 CreditÂ·3 AdvanceÂ·4 Consignment | PO `status`:1 DraftÂ·2 SubmittedÂ·3 ApprovedÂ·4 PartiallyReceivedÂ·5 ReceivedÂ·6 ClosedÂ·7 Cancelled | GRN `status`:1 DraftÂ·2 PostedÂ·3 InvoicedÂ·4 Cancelled | invoice `status`:1 DraftÂ·2 PostedÂ·3 PartiallyReturnedÂ·4 ReturnedÂ·5 Cancelled | `receive_mode`:1 OneStepÂ·2 AgainstGRN | `payment_status`:1 UnpaidÂ·2 PartialÂ·3 Paid | return `status`:1 DraftÂ·2 PostedÂ·3 Cancelled | `settlement_mode`:1 AdjustPayableÂ·2 CashRefundÂ·3 Replacement | `payment_method`:1 CashÂ·2 BankÂ·3 ChequeÂ·4 MobileÂ·5 CardÂ·6 Adjustment | landed `cost_type`:1 FreightÂ·2 DutyÂ·3 ClearingÂ·4 InsuranceÂ·5 HandlingÂ·6 Other | supplier-ledger `ref_doc_type`:1 OpeningÂ·2 InvoiceÂ·3 PaymentÂ·4 ReturnÂ·5 Adjustment.

---

## Cross-module integration recap (inv â†” sal â†” pur)
- **Stock IN** (this module): `pur_invoice`/`pur_receipt` â†’ `InvStockPostingService` movement 2 at landed cost â†’ updates `inv_stock`/layers; **Stock OUT** to supplier: `pur_return` movement 3.
- **Cost flows forward:** landed cost set here is the `avg_cost`/layer cost that `sal_invoice` reads as COGS â€” accurate margins depend on correct purchase costing.
- **AP vs AR symmetry:** `pur_supplier_ledger` (payable) mirrors `sal_customer_ledger` (receivable); both feed the FIN module's control accounts.
- **Shared infra:** `sys_doc_sequence` (numbering), `sys_event_outbox` (events), `sys_fin_year`/`sys_fin_year_dtl` (period control), `sys_vat_tax` (tax), `sys_currency` (FX), `fin_account`/`fin_voucher` (GL). See `inv-db.md` Â§3.8 and `sal-db.md` Â§3.5 for the full posting picture.


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

