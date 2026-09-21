# INV Business

<!-- Consolidated from the previous aidly-business-doc/backend, frontend, memory, and planning files. -->

INV is the stock source of truth: products, variants, warehouses, stock movements, valuation layers, transfer, adjustment, batch/expiry, and reorder controls. Other modules call INV services rather than writing stock tables directly.

## Inventory Database and Backend Blueprint

# INV â€” Inventory Management Module (Production Blueprint)

> **Module prefix:** `inv_` Â· **Backend package:** `com.infoaidtech.aidly.inv` Â· **Frontend feature:** `apps/web-client/src/app/features/inv`
> **Forms covered:** INV_1001 Product Master Â· INV_1002 Barcode Generator Â· INV_1101 Opening Stock Â· INV_1102 Stock Adjustment Â· INV_2001 Warehouse Setup Â· INV_2002 Rack/Shelf Setup Â· INV_2003 Stock Transfer Â· INV_2004 Batch & Expiry Â· INV_2005 Reorder Level
> **Aligns with:** `db-updated.md` (PostgreSQL conventions), backend `CLAUDE.md` (Spring Boot 4 / JPA / MapStruct / form-wise API), frontend `CLAUDE.md` + `FRONTEND_ARCHITECTURE.md` (Angular 21 signals, `app-common-table`, `page-form-layout`).
> This module is the **inventory source of truth**. `sal_*` and `pur_*` post into it through the stock-posting engine defined in Â§3. Do not write stock balances directly anywhere else.

---

## Conventions inherited from the existing codebase (applies to every table below)

These are not re-explained per-table; they are the house style observed in `sys_*`/`hr_*`:

| Concern | Rule |
|---|---|
| Surrogate PK | `{entity}_no BIGSERIAL PRIMARY KEY` (Java `Long`, `@GeneratedValue(IDENTITY)`) |
| Business key | `{entity}_id VARCHAR(n)` â€” human code, unique **per branch among live rows** via partial index `WHERE is_deleted = 0` |
| Tenant columns | `company_no BIGINT NOT NULL`, `branch_no BIGINT NOT NULL` (entity extends `BaseEntity`). Catalog tables shared across branches are `company_no`-scoped with `branch_no` nullable â€” flagged per table. |
| NLS | optional `{x}_name_nls VARCHAR` for Bangla/native rendering |
| Booleans | `SMALLINT NOT NULL DEFAULT 0/1` + `CHECK (col IN (0,1))` â€” never native `BOOLEAN` |
| Enums | `SMALLINT` + `CHECK (col IN (...))` + inline comment listing codes |
| Money | `NUMERIC(20,6)` for unit cost/valuation precision, `NUMERIC(20,4)` for line/transaction amounts, `NUMERIC(18,4)` for quantities, `NUMERIC(5,2)` for percentages |
| Audit block | `is_active`, `is_deleted`, `created_by/created_at`, `updated_by/updated_at`, `deleted_by/deleted_at`, `row_version` (identical to `AuditEntity`) |
| Soft delete | never hard-delete; `performSoftDelete(userNo)` flips `is_deleted=1`, `is_active=0`; override `nullifyBusinessId()` to free a **full** unique slot |
| Optimistic lock | `row_version BIGINT NOT NULL DEFAULT 1` (`@Version`) â†’ 409 on conflict |
| Audit trail | row-level before/after JSON captured into `sys_audit_log` by the audit interceptor |
| FK default | `ON DELETE RESTRICT` for masters; `ON DELETE CASCADE` only headerâ†’detail (`*_dtl`); `ON DELETE SET NULL` for optional self/cross refs |

**The standard audit block** (copied verbatim into every table; shown once here, abbreviated as `-- << AUDIT BLOCK >>` afterwards):

```sql
is_active   SMALLINT     NOT NULL DEFAULT 1,
is_deleted  SMALLINT     NOT NULL DEFAULT 0,
created_by  BIGINT       NOT NULL,
created_at  TIMESTAMPTZ  NOT NULL DEFAULT CURRENT_TIMESTAMP,
updated_by  BIGINT,
updated_at  TIMESTAMPTZ,
deleted_by  BIGINT,
deleted_at  TIMESTAMPTZ,
row_version BIGINT       NOT NULL DEFAULT 1,
CONSTRAINT chk_{t}_is_active  CHECK (is_active  IN (0,1)),
CONSTRAINT chk_{t}_is_deleted CHECK (is_deleted IN (0,1))
```

---

# 1. BUSINESS MODULE OVERVIEW

## 1.1 What this module does
Inventory Management owns **what a product is** (catalog) and **how much of it exists, where, at what cost** (stock). For a multi-branch retailer it must answer, at any instant and per branch/warehouse: *on-hand qty, available qty (on-hand âˆ’ reserved), the moving/FIFO cost, the batch & expiry position, and a fully auditable movement history*. Every quantity change in the whole ERP â€” a purchase receipt, a POS sale, a return, a transfer, a manual adjustment, an opening balance â€” becomes one or more rows in the immutable **stock ledger** (`inv_stock_ledger`) and a synchronized update to the **balance** table (`inv_stock`). Reports never recompute from documents; they read the ledger/balance.

## 1.2 Real retail workflow (lifecycle)
1. **Catalog build-out** â€” Categories (INV_1001 supporting), Brands, UOMs created. Product master created with type (standard / variant-parent / service / bundle), tax class, default prices, tracking flags (batch / expiry / serial). Variants generated from attributes (Size Ã— Color). Barcodes assigned (INV_1002), including pack-level barcodes.
2. **Warehouse topology** â€” Warehouses per branch (INV_2001): main store, sales-floor outlet, transit (virtual), damage/quarantine. Optional rack/shelf bins (INV_2002).
3. **Go-live stock** â€” Opening Stock Entry (INV_1101) seeds quantities and per-unit opening cost â†’ posts `OPENING` ledger rows and creates valuation layers.
4. **Day-to-day** â€” Purchases increase stock (`pur_*` â†’ posting engine); sales/POS decrease it (`sal_*`); transfers move it between warehouses (INV_2003) via the transit warehouse; adjustments correct it (INV_1102) with a reason; physical counts reconcile it.
5. **Replenishment** â€” Reorder levels (INV_2005) drive low-stock alerts and feed purchase suggestions.
6. **Batch/expiry control** â€” Batch-tracked goods are received against batches with mfg/expiry; near-expiry and expired stock surfaces in reports and blocks/limits sale (INV_2004).
7. **Period control** â€” Movements are stamped with the open `sys_fin_year`/period; posting into a closed period is rejected.

## 1.3 Actors
| Actor | Responsibilities |
|---|---|
| Inventory/Store Manager | catalog, warehouses, opening stock, adjustments (approve), transfers (approve), reorder setup |
| Storekeeper / Stock Clerk | receive transfers, raise adjustments/counts (create), bin moves |
| Cashier / Sales | consume available stock at POS (read + reserve via sale) |
| Purchaser | drives inbound stock (via `pur_*`) |
| Auditor / Accountant | reads ledger & valuation; reconciles inventory GL control account |
| Admin | enables tracking flags, costing method, branch policies |

## 1.4 Approval flows
- **Stock Adjustment (INV_1102)**: `DRAFT â†’ SUBMITTED â†’ APPROVED(=POSTED) â†’ (CANCELLED)`. Only APPROVED posts to ledger. `can_approve` required to move SUBMITTEDâ†’APPROVED. A configurable **value threshold** can force two-step approval (clerk submits, manager approves).
- **Stock Transfer (INV_2003)**: `DRAFT â†’ SUBMITTED â†’ DISPATCHED(out posted to transit) â†’ RECEIVED(in posted) â†’ (CANCELLED before dispatch / SHORT_RECEIVED with variance)`.
- **Opening Stock (INV_1101)**: `DRAFT â†’ POSTED`; locked once the financial year has any other movement.
- **Physical Count**: `DRAFT â†’ COUNTING â†’ REVIEW â†’ POSTED` (posts variance adjustments).

## 1.5 Edge cases (must be handled, not ignored)
- Concurrent sale of the last unit from two POS terminals â†’ pessimistic lock on the `inv_stock` row; configurable allow/deny negative stock per branch.
- Selling/transferring a batch whose expiry passed mid-transaction.
- Returns of batch/serial items must restock the **same** batch/serial they left on.
- UOM mismatch: buy in CARTON, sell in PCS â€” every movement normalizes to the product's **base UOM** before touching `inv_stock`.
- Cost of a return: weighted-average vs. original layer cost (return-in re-enters at original issue cost to avoid margin leakage; configurable).
- Negative on-hand from back-dated documents; reorder alert flapping; bundle/kit explosion on sale; rounding drift on UOM conversions (store base-UOM qty authoritatively).
- Deleting a product that already has movements â†’ forbidden (RESTRICT); only `is_active=0` (discontinue).
- Transfer dispatched but never received (stuck in transit) â†’ aging report + force-receive with variance.

## 1.6 Operational constraints
- Costing method (`WEIGHTED_AVG` default | `FIFO` | `LIFO` | `STANDARD`) is set **per company** and is immutable once movements exist (changing it requires a revaluation run).
- Negative stock policy is per branch (`allow_negative_stock`).
- All money in base currency (`sys_currency.is_base_currency=1`); foreign-currency purchases convert at posting time.

## 1.7 SME real-world examples
- A 6-outlet pharmacy chain: batch+expiry mandatory, FEFO (first-expiry-first-out) picking, near-expiry return-to-supplier.
- A 3-branch fashion retailer: variant matrix (sizeÃ—color), barcode per variant, inter-branch transfers to balance sizes, seasonal markdowns.
- A single-shop grocery with a back-store warehouse: weighted-average costing, weight-based UOM (kg), frequent manual adjustments for spoilage.

---

# 2. DATABASE DESIGN

> Read order: catalog (Â§2.1â€“2.8) â†’ topology (Â§2.9â€“2.11) â†’ batch/serial (Â§2.12â€“2.13) â†’ **stock engine** (Â§2.14â€“2.16) â†’ documents (Â§2.17â€“2.22) â†’ shared numbering & events (Â§2.23â€“2.24).

## 2.1 `inv_category` â€” product category (hierarchical)
**Purpose:** classification tree for products; drives reporting roll-ups and category-level promotions/tax defaults.
**Scope:** company-wide catalog (`company_no NOT NULL`, `branch_no` NULL = shared).

```sql
CREATE TABLE inv_category (
    category_no        BIGSERIAL PRIMARY KEY,
    company_no         BIGINT      NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no          BIGINT      REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    category_id        VARCHAR(30) NOT NULL,
    category_name      VARCHAR(150) NOT NULL,
    category_name_nls  VARCHAR(150),
    parent_category_no BIGINT      REFERENCES inv_category(category_no) ON DELETE RESTRICT,
    tree_path          VARCHAR(500),                 -- materialized path e.g. '/1/4/9/' for fast subtree queries
    depth              SMALLINT    NOT NULL DEFAULT 0,
    default_vat_tax_no BIGINT      REFERENCES sys_vat_tax(vat_tax_no) ON DELETE SET NULL,
    image_path         VARCHAR(255),
    order_sl           INTEGER     NOT NULL DEFAULT 0,
    remarks            TEXT,
    -- << AUDIT BLOCK >>
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT   NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT,  updated_at TIMESTAMPTZ,
    deleted_by  BIGINT,  deleted_at TIMESTAMPTZ,
    row_version BIGINT   NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_category_active  CHECK (is_active  IN (0,1)),
    CONSTRAINT chk_inv_category_deleted CHECK (is_deleted IN (0,1)),
    CONSTRAINT chk_inv_category_no_self CHECK (parent_category_no IS NULL OR parent_category_no <> category_no)
);
CREATE UNIQUE INDEX uq_inv_category_id   ON inv_category(company_no, category_id)   WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_inv_category_name ON inv_category(company_no, category_name) WHERE is_deleted = 0;
CREATE INDEX idx_inv_category_parent ON inv_category(parent_category_no);
CREATE INDEX idx_inv_category_path   ON inv_category(tree_path varchar_pattern_ops);
CREATE INDEX idx_inv_category_active ON inv_category(company_no, is_active) WHERE is_deleted = 0;
```
**Business rules:** parent must belong to same company; cannot delete if products reference it (RESTRICT) â€” discontinue instead. `tree_path`/`depth` recomputed on parent change (and cascaded to descendants in the same transaction). Max depth 6 (validated in service).
**Insert/Update/Delete:** insert sets depth/path from parent; update forbids creating a cycle (walk ancestors); delete = soft-delete only when no live child categories and no live products.

## 2.2 `inv_brand` â€” brand master
**Purpose:** manufacturer/brand tagging; reporting + filtering. **Scope:** company-wide.
```sql
CREATE TABLE inv_brand (
    brand_no        BIGSERIAL PRIMARY KEY,
    company_no      BIGINT      NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    brand_id        VARCHAR(30) NOT NULL,
    brand_name      VARCHAR(150) NOT NULL,
    brand_name_nls  VARCHAR(150),
    manufacturer    VARCHAR(200),
    image_path      VARCHAR(255),
    remarks         TEXT,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_brand_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_brand_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_brand_id   ON inv_brand(company_no, brand_id)   WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_inv_brand_name ON inv_brand(company_no, brand_name) WHERE is_deleted = 0;
```

## 2.3 `inv_uom` â€” unit of measure
**Purpose:** the units products are tracked/bought/sold in (PCS, KG, GM, LTR, BOX, CARTON, DOZEN). **Scope:** company-wide.
```sql
CREATE TABLE inv_uom (
    uom_no      BIGSERIAL PRIMARY KEY,
    company_no  BIGINT      NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    uom_id      VARCHAR(20) NOT NULL,        -- 'PCS','KG','CTN'
    uom_name    VARCHAR(50) NOT NULL,        -- 'Pieces'
    uom_name_nls VARCHAR(50),
    uom_type    SMALLINT    NOT NULL DEFAULT 1,  -- 1=Count(integer),2=Weight,3=Volume,4=Length
    decimal_places SMALLINT NOT NULL DEFAULT 0,  -- 0 = whole units only (PCS), 3 = KG
    remarks     TEXT,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_uom_type CHECK (uom_type IN (1,2,3,4)),
    CONSTRAINT chk_inv_uom_dec  CHECK (decimal_places BETWEEN 0 AND 4),
    CONSTRAINT chk_inv_uom_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_uom_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_uom_id ON inv_uom(company_no, uom_id) WHERE is_deleted = 0;
```

## 2.4 `inv_product` â€” product master (INV_1001)
**Purpose:** the catalog header. A standard product is sellable directly; a `VARIANT_PARENT` is abstract and only its `inv_product_variant` rows are transactable.
**Scope:** company-wide catalog (prices can be overridden per branch in `inv_product_price`).
```sql
CREATE TABLE inv_product (
    product_no        BIGSERIAL PRIMARY KEY,
    company_no        BIGINT       NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    product_id        VARCHAR(40)  NOT NULL,                 -- SKU / item code
    product_name      VARCHAR(200) NOT NULL,
    product_name_nls  VARCHAR(200),
    short_name        VARCHAR(60),                            -- POS button / receipt label
    product_type      SMALLINT     NOT NULL DEFAULT 1,        -- 1=Standard,2=VariantParent,3=Service,4=Bundle/Kit
    category_no       BIGINT       NOT NULL REFERENCES inv_category(category_no) ON DELETE RESTRICT,
    brand_no          BIGINT       REFERENCES inv_brand(brand_no) ON DELETE RESTRICT,
    base_uom_no       BIGINT       NOT NULL REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    purchase_uom_no   BIGINT       REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,  -- default buy unit
    sales_uom_no      BIGINT       REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,  -- default sell unit
    vat_tax_no        BIGINT       REFERENCES sys_vat_tax(vat_tax_no) ON DELETE SET NULL,
    is_tax_inclusive  SMALLINT     NOT NULL DEFAULT 0,        -- 1 = listed prices already include VAT
    hsn_sac_code      VARCHAR(20),                            -- tax classification code
    -- tracking flags (immutable once movements exist)
    is_stock_tracked  SMALLINT     NOT NULL DEFAULT 1,        -- 0 for services
    is_batch_tracked  SMALLINT     NOT NULL DEFAULT 0,
    is_expiry_tracked SMALLINT     NOT NULL DEFAULT 0,
    is_serial_tracked SMALLINT     NOT NULL DEFAULT 0,
    has_variants      SMALLINT     NOT NULL DEFAULT 0,
    shelf_life_days   INTEGER,                                -- for auto expiry suggestion
    -- default pricing (base UOM, base currency)
    cost_price        NUMERIC(20,6) NOT NULL DEFAULT 0,       -- last/standard cost (informational; live cost in inv_stock)
    purchase_price    NUMERIC(20,6) NOT NULL DEFAULT 0,
    sale_price        NUMERIC(20,4) NOT NULL DEFAULT 0,
    mrp               NUMERIC(20,4) NOT NULL DEFAULT 0,        -- max retail price (ceiling)
    min_sale_price    NUMERIC(20,4),                           -- floor (block selling below)
    default_margin_pct NUMERIC(5,2),
    -- replenishment defaults (per-warehouse overrides in inv_reorder)
    reorder_level     NUMERIC(18,4) NOT NULL DEFAULT 0,
    reorder_qty       NUMERIC(18,4) NOT NULL DEFAULT 0,
    min_stock         NUMERIC(18,4) NOT NULL DEFAULT 0,
    max_stock         NUMERIC(18,4),
    -- physical
    weight_gm         NUMERIC(18,4),
    barcode           VARCHAR(64),                             -- primary barcode (also see inv_product_barcode)
    image_path        VARCHAR(255),
    is_sellable       SMALLINT     NOT NULL DEFAULT 1,
    is_purchasable    SMALLINT     NOT NULL DEFAULT 1,
    allow_discount    SMALLINT     NOT NULL DEFAULT 1,
    remarks           TEXT,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_product_type     CHECK (product_type IN (1,2,3,4)),
    CONSTRAINT chk_inv_product_prices   CHECK (sale_price >= 0 AND purchase_price >= 0 AND mrp >= 0),
    CONSTRAINT chk_inv_product_minsale  CHECK (min_sale_price IS NULL OR min_sale_price <= mrp),
    CONSTRAINT chk_inv_product_flags    CHECK (is_batch_tracked IN (0,1) AND is_expiry_tracked IN (0,1)
                                              AND is_serial_tracked IN (0,1) AND has_variants IN (0,1)),
    CONSTRAINT chk_inv_product_active   CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_product_deleted  CHECK (is_deleted IN (0,1)),
    CONSTRAINT chk_inv_product_expiry_needs_batch CHECK (is_expiry_tracked = 0 OR is_batch_tracked = 1)
);
CREATE UNIQUE INDEX uq_inv_product_id      ON inv_product(company_no, product_id) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_inv_product_barcode ON inv_product(company_no, barcode)    WHERE is_deleted = 0 AND barcode IS NOT NULL;
CREATE INDEX idx_inv_product_category ON inv_product(category_no);
CREATE INDEX idx_inv_product_brand    ON inv_product(brand_no);
CREATE INDEX idx_inv_product_active   ON inv_product(company_no, is_active) WHERE is_deleted = 0;
CREATE INDEX idx_inv_product_name_trgm ON inv_product USING gin (product_name gin_trgm_ops);  -- typeahead search (needs pg_trgm)
```
**Business rules:** `is_expiry_tracked` implies `is_batch_tracked` (CHECK). Tracking flags & `base_uom_no` are **frozen** once any `inv_stock_ledger` row exists for the product (service-level guard). `VARIANT_PARENT` carries no stock; its variants do. `cost_price`/`purchase_price` here are reference values; the *authoritative* live cost lives in `inv_stock.avg_cost` / valuation layers. Deleting requires zero live movements.

## 2.5 `inv_product_attribute` / `inv_product_attribute_value` â€” variant axes
**Purpose:** define the variant matrix (Size, Color, â€¦) and allowed values. **Scope:** company-wide.
```sql
CREATE TABLE inv_product_attribute (
    attribute_no   BIGSERIAL PRIMARY KEY,
    company_no     BIGINT      NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    attribute_id   VARCHAR(30) NOT NULL,
    attribute_name VARCHAR(60) NOT NULL,         -- 'Size'
    order_sl       INTEGER NOT NULL DEFAULT 0,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_attr_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_attr_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_attr_id ON inv_product_attribute(company_no, attribute_id) WHERE is_deleted = 0;

CREATE TABLE inv_product_attribute_value (
    attribute_value_no BIGSERIAL PRIMARY KEY,
    attribute_no       BIGINT      NOT NULL REFERENCES inv_product_attribute(attribute_no) ON DELETE CASCADE,
    value_code         VARCHAR(30) NOT NULL,     -- 'M'
    value_name         VARCHAR(60) NOT NULL,     -- 'Medium'
    order_sl           INTEGER NOT NULL DEFAULT 0,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_inv_attr_val UNIQUE (attribute_no, value_code),
    CONSTRAINT chk_inv_attrval_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_attrval_deleted CHECK (is_deleted IN (0,1))
);
```
Note `uq_inv_attr_val` is a **full** unique constraint â†’ `nullifyBusinessId()` sentinels `value_code` on soft-delete.

## 2.6 `inv_product_variant` â€” sellable variant (SKU leaf)
**Purpose:** the actual stock-keeping unit when `inv_product.has_variants=1`. Carries its own barcode/SKU and optional price override.
```sql
CREATE TABLE inv_product_variant (
    variant_no     BIGSERIAL PRIMARY KEY,
    product_no     BIGINT       NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_sku    VARCHAR(48)  NOT NULL,
    variant_name   VARCHAR(200) NOT NULL,           -- 'T-Shirt / Red / M'
    barcode        VARCHAR(64),
    attr1_value_no BIGINT REFERENCES inv_product_attribute_value(attribute_value_no) ON DELETE RESTRICT,
    attr2_value_no BIGINT REFERENCES inv_product_attribute_value(attribute_value_no) ON DELETE RESTRICT,
    attr3_value_no BIGINT REFERENCES inv_product_attribute_value(attribute_value_no) ON DELETE RESTRICT,
    purchase_price NUMERIC(20,6),                    -- NULL â†’ inherit product
    sale_price     NUMERIC(20,4),
    mrp            NUMERIC(20,4),
    image_path     VARCHAR(255),
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_variant_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_variant_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_variant_sku     ON inv_product_variant(product_no, variant_sku) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_inv_variant_barcode ON inv_product_variant(barcode) WHERE is_deleted = 0 AND barcode IS NOT NULL;
CREATE INDEX idx_inv_variant_product ON inv_product_variant(product_no);
CREATE UNIQUE INDEX uq_inv_variant_combo ON inv_product_variant(product_no, attr1_value_no, attr2_value_no, attr3_value_no) WHERE is_deleted = 0;
```
> **Modeling rule used everywhere downstream:** every stock-bearing line references **both** `product_no` and a nullable `variant_no`. For non-variant products `variant_no` is `NULL`. This keeps a single uniform join across stock, ledger and all documents.

## 2.7 `inv_product_barcode` â€” multi-barcode / pack barcode (INV_1002)
**Purpose:** a product/variant may have many barcodes (supplier EAN, internal, pack-of-12 barcode). UOM-aware so a scanned carton barcode resolves to a qty multiplier.
```sql
CREATE TABLE inv_product_barcode (
    barcode_no   BIGSERIAL PRIMARY KEY,
    company_no   BIGINT      NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    product_no   BIGINT      NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no   BIGINT      REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    barcode      VARCHAR(64) NOT NULL,
    uom_no       BIGINT      NOT NULL REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    pack_qty     NUMERIC(18,4) NOT NULL DEFAULT 1,   -- base-UOM units this barcode represents
    barcode_type SMALLINT    NOT NULL DEFAULT 1,      -- 1=EAN13,2=UPC,3=CODE128,4=QR,5=Internal
    is_primary   SMALLINT    NOT NULL DEFAULT 0,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_barcode_packqty CHECK (pack_qty > 0),
    CONSTRAINT chk_inv_barcode_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_barcode_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_barcode_value ON inv_product_barcode(company_no, barcode) WHERE is_deleted = 0;
CREATE INDEX idx_inv_barcode_product ON inv_product_barcode(product_no, variant_no);
```
**Rule:** barcode lookup at POS/receiving returns `(product_no, variant_no, uom_no, pack_qty)`; the scanned qty is multiplied by `pack_qty` to get base-UOM qty.

## 2.8 `inv_uom_conversion` â€” per-product UOM factors
**Purpose:** convert any allowed UOM to base UOM for a product (CARTONâ†’24 PCS). Product-scoped (factors differ per product).
```sql
CREATE TABLE inv_uom_conversion (
    uom_conversion_no BIGSERIAL PRIMARY KEY,
    product_no   BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE CASCADE,
    from_uom_no  BIGINT NOT NULL REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    to_base_factor NUMERIC(18,6) NOT NULL,   -- 1 from_uom = to_base_factor base_uom
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_inv_uomconv UNIQUE (product_no, from_uom_no),
    CONSTRAINT chk_inv_uomconv_factor CHECK (to_base_factor > 0),
    CONSTRAINT chk_inv_uomconv_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_uomconv_deleted CHECK (is_deleted IN (0,1))
);
```

## 2.9 `inv_warehouse` â€” warehouse / store / outlet (INV_2001)
**Purpose:** the physical/virtual stock location. Branch-scoped.
```sql
CREATE TABLE inv_warehouse (
    warehouse_no   BIGSERIAL PRIMARY KEY,
    company_no     BIGINT      NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no      BIGINT      NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    warehouse_id   VARCHAR(30) NOT NULL,
    warehouse_name VARCHAR(150) NOT NULL,
    warehouse_name_nls VARCHAR(150),
    warehouse_type SMALLINT    NOT NULL DEFAULT 1,   -- 1=Main,2=SalesFloor/Outlet,3=Transit(virtual),4=Damage/Quarantine,5=Returns
    address        VARCHAR(250),
    manager_employee_no BIGINT REFERENCES hrm_employee(employee_no) ON DELETE SET NULL,
    is_default     SMALLINT    NOT NULL DEFAULT 0,    -- default sale source for the branch
    allow_negative_stock SMALLINT NOT NULL DEFAULT 0,
    is_sale_point  SMALLINT    NOT NULL DEFAULT 1,
    remarks        TEXT,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_wh_type CHECK (warehouse_type IN (1,2,3,4,5)),
    CONSTRAINT chk_inv_wh_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_wh_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_wh_id      ON inv_warehouse(branch_no, warehouse_id) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_inv_wh_default ON inv_warehouse(branch_no) WHERE is_default = 1 AND is_deleted = 0;  -- one default per branch
CREATE INDEX idx_inv_wh_branch ON inv_warehouse(branch_no) WHERE is_deleted = 0;
```

## 2.10 `inv_rack` â€” bin / rack / shelf location (INV_2002)
```sql
CREATE TABLE inv_rack (
    rack_no      BIGSERIAL PRIMARY KEY,
    warehouse_no BIGINT      NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    rack_id      VARCHAR(30) NOT NULL,        -- 'A-01-03' (aisle-rack-shelf)
    rack_name    VARCHAR(100),
    aisle        VARCHAR(20), rack VARCHAR(20), shelf VARCHAR(20), bin VARCHAR(20),
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_rack_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_rack_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_rack_id ON inv_rack(warehouse_no, rack_id) WHERE is_deleted = 0;
```

## 2.11 `inv_reorder` â€” per-warehouse reorder policy (INV_2005)
**Purpose:** override product-level reorder defaults per warehouse; drives low-stock alerts and purchase suggestions.
```sql
CREATE TABLE inv_reorder (
    reorder_no    BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    warehouse_no  BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    product_no    BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no    BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    reorder_level NUMERIC(18,4) NOT NULL DEFAULT 0,
    reorder_qty   NUMERIC(18,4) NOT NULL DEFAULT 0,
    min_stock     NUMERIC(18,4) NOT NULL DEFAULT 0,
    max_stock     NUMERIC(18,4),
    preferred_supplier_no BIGINT,   -- FK to pur_supplier(supplier_no) â€” see pur-db.md
    lead_time_days INTEGER,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_reorder_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_reorder_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_reorder ON inv_reorder(warehouse_no, product_no, COALESCE(variant_no,0)) WHERE is_deleted = 0;
```

## 2.12 `inv_batch` â€” batch / lot master (INV_2004)
**Purpose:** identifies a received lot with mfg/expiry and its landed cost. Created during receiving; referenced by every batch-tracked movement.
```sql
CREATE TABLE inv_batch (
    batch_no       BIGSERIAL PRIMARY KEY,
    company_no     BIGINT      NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    product_no     BIGINT      NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no     BIGINT      REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    batch_code     VARCHAR(60) NOT NULL,        -- supplier lot no or generated
    mfg_date       DATE,
    expiry_date    DATE,
    supplier_no    BIGINT,                       -- FK pur_supplier(supplier_no) (origin)
    received_cost  NUMERIC(20,6) NOT NULL DEFAULT 0,  -- landed unit cost of this lot
    mrp            NUMERIC(20,4),
    remarks        TEXT,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_batch_dates  CHECK (expiry_date IS NULL OR mfg_date IS NULL OR expiry_date >= mfg_date),
    CONSTRAINT chk_inv_batch_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_batch_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_batch_code ON inv_batch(company_no, product_no, COALESCE(variant_no,0), batch_code) WHERE is_deleted = 0;
CREATE INDEX idx_inv_batch_expiry  ON inv_batch(expiry_date) WHERE is_deleted = 0;
CREATE INDEX idx_inv_batch_product ON inv_batch(product_no, variant_no);
```

## 2.13 `inv_serial` â€” serial number tracking
**Purpose:** unit-level traceability for serialized goods (electronics). One row per physical unit; status transitions IN_STOCKâ†’SOLDâ†’RETURNED.
```sql
CREATE TABLE inv_serial (
    serial_no_pk  BIGSERIAL PRIMARY KEY,
    company_no    BIGINT      NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    product_no    BIGINT      NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no    BIGINT      REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    batch_no      BIGINT      REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,
    serial_code   VARCHAR(80) NOT NULL,
    warehouse_no  BIGINT      REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    serial_status SMALLINT    NOT NULL DEFAULT 1,   -- 1=InStock,2=Reserved,3=Sold,4=Returned,5=Damaged,6=InTransit
    in_doc_type   SMALLINT, in_ref_no VARCHAR(40),
    out_doc_type  SMALLINT, out_ref_no VARCHAR(40),
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_serial_status CHECK (serial_status IN (1,2,3,4,5,6)),
    CONSTRAINT chk_inv_serial_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_serial_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_serial_code ON inv_serial(company_no, product_no, serial_code) WHERE is_deleted = 0;
CREATE INDEX idx_inv_serial_status ON inv_serial(serial_status, warehouse_no);
```

## 2.14 `inv_stock` â€” live balance (the snapshot)
**Purpose:** one row per *stock cell* = (warehouse, product, variant, batch). Holds on-hand, reserved, available and the **weighted-average cost**. This is the table read by POS availability checks and most reports; it is **only** mutated by the posting engine under a row lock.
```sql
CREATE TABLE inv_stock (
    stock_no       BIGSERIAL PRIMARY KEY,
    company_no     BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no      BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    warehouse_no   BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    product_no     BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no     BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    batch_no       BIGINT REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,
    qty_on_hand    NUMERIC(18,4) NOT NULL DEFAULT 0,
    qty_reserved   NUMERIC(18,4) NOT NULL DEFAULT 0,   -- soft-allocated by open sales/holds
    qty_available  NUMERIC(18,4) GENERATED ALWAYS AS (qty_on_hand - qty_reserved) STORED,
    avg_cost       NUMERIC(20,6) NOT NULL DEFAULT 0,    -- weighted-average unit cost (base currency)
    last_cost      NUMERIC(20,6) NOT NULL DEFAULT 0,    -- last receipt cost
    stock_value    NUMERIC(20,4) GENERATED ALWAYS AS (qty_on_hand * avg_cost) STORED,
    last_movement_at TIMESTAMPTZ,
    -- balance carries no business soft-delete; it is derived state. Audit-light:
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_stock_reserved CHECK (qty_reserved >= 0)
);
CREATE UNIQUE INDEX uq_inv_stock_cell ON inv_stock(warehouse_no, product_no, COALESCE(variant_no,0), COALESCE(batch_no,0));
CREATE INDEX idx_inv_stock_product  ON inv_stock(product_no, variant_no);
CREATE INDEX idx_inv_stock_branch   ON inv_stock(branch_no, warehouse_no);
CREATE INDEX idx_inv_stock_low      ON inv_stock(warehouse_no, product_no) WHERE qty_on_hand <= 0;
```
**Concurrency:** all writes go through `SELECT â€¦ FOR UPDATE` on the matching row (or insert-on-conflict to create the cell). `qty_available` and `stock_value` are generated columns â€” never written directly. Negative `qty_on_hand` allowed only when the warehouse's `allow_negative_stock=1`.

## 2.15 `inv_stock_ledger` â€” immutable movement journal (source of truth)
**Purpose:** append-only record of every base-UOM quantity change with its cost. Reconstructs balances, powers valuation, and is the audit backbone. **Never updated or deleted** â€” corrections are new reversing rows.
```sql
CREATE TABLE inv_stock_ledger (
    stock_ledger_no BIGSERIAL PRIMARY KEY,
    company_no     BIGINT NOT NULL,
    branch_no      BIGINT NOT NULL,
    warehouse_no   BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    product_no     BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no     BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    batch_no       BIGINT REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,
    movement_date  DATE   NOT NULL,
    fin_year_no    BIGINT NOT NULL REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no  BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    movement_type  SMALLINT NOT NULL,  -- see enum below
    direction      SMALLINT NOT NULL,  -- +1 = IN, -1 = OUT
    qty_base       NUMERIC(18,4) NOT NULL,        -- always base UOM, always positive
    unit_cost      NUMERIC(20,6) NOT NULL DEFAULT 0,
    total_cost     NUMERIC(20,4) NOT NULL DEFAULT 0,  -- qty_base * unit_cost
    balance_after  NUMERIC(18,4) NOT NULL,        -- running on-hand for the cell after this row
    avg_cost_after NUMERIC(20,6) NOT NULL DEFAULT 0,
    ref_doc_type   SMALLINT NOT NULL,  -- 1=Opening,2=PurInvoice,3=PurReturn,4=SalInvoice,5=SalReturn,6=TransferOut,7=TransferIn,8=AdjIn,9=AdjOut,10=CountVariance,11=BundleAssemble,12=BundleDisassemble
    ref_doc_no     VARCHAR(40) NOT NULL,           -- business document id
    ref_doc_pk     BIGINT,                         -- surrogate of the source document
    ref_line_no    BIGINT,                         -- source line surrogate
    is_reversal    SMALLINT NOT NULL DEFAULT 0,
    reversed_ledger_no BIGINT REFERENCES inv_stock_ledger(stock_ledger_no),
    remarks        VARCHAR(250),
    created_by     BIGINT NOT NULL,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_inv_ledger_dir CHECK (direction IN (-1, 1)),
    CONSTRAINT chk_inv_ledger_qty CHECK (qty_base > 0),
    CONSTRAINT chk_inv_ledger_mt  CHECK (movement_type BETWEEN 1 AND 12)
);
CREATE INDEX idx_inv_ledger_cell   ON inv_stock_ledger(warehouse_no, product_no, variant_no, batch_no, stock_ledger_no);
CREATE INDEX idx_inv_ledger_ref    ON inv_stock_ledger(ref_doc_type, ref_doc_no);
CREATE INDEX idx_inv_ledger_date   ON inv_stock_ledger(branch_no, movement_date);
CREATE INDEX idx_inv_ledger_period ON inv_stock_ledger(fin_period_no);
CREATE INDEX idx_inv_ledger_product_date ON inv_stock_ledger(product_no, movement_date);
```
**`movement_type` ENUM:** `1=Opening, 2=Purchase, 3=PurchaseReturn, 4=Sale, 5=SalesReturn, 6=TransferOut, 7=TransferIn, 8=AdjustmentIn, 9=AdjustmentOut, 10=CountVariance, 11=Assemble, 12=Disassemble`.
**Partitioning:** when ledger volume is high, **range-partition by `movement_date` (monthly)** or by `fin_year_no`. Keep current year hot; archive closed years. Document the partition key as `(branch_no, movement_date)` for partition pruning on branch+date reports.

## 2.16 `inv_valuation_layer` â€” FIFO/LIFO cost layers
**Purpose:** only used when costing method âˆˆ {FIFO, LIFO}. Each IN creates a layer; each OUT consumes `remaining_qty` from layers in date order (FIFO) or reverse (LIFO). For WEIGHTED_AVG this table is unused (cost held in `inv_stock.avg_cost`).
```sql
CREATE TABLE inv_valuation_layer (
    layer_no      BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL,
    warehouse_no  BIGINT NOT NULL,
    product_no    BIGINT NOT NULL,
    variant_no    BIGINT,
    batch_no      BIGINT,
    receipt_ledger_no BIGINT NOT NULL REFERENCES inv_stock_ledger(stock_ledger_no) ON DELETE RESTRICT,
    receipt_date  DATE   NOT NULL,
    original_qty  NUMERIC(18,4) NOT NULL,
    remaining_qty NUMERIC(18,4) NOT NULL,
    unit_cost     NUMERIC(20,6) NOT NULL,
    is_exhausted  SMALLINT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_layer_rem CHECK (remaining_qty >= 0 AND remaining_qty <= original_qty)
);
CREATE INDEX idx_inv_layer_fifo ON inv_valuation_layer(warehouse_no, product_no, variant_no, batch_no, receipt_date, layer_no) WHERE is_exhausted = 0;
```

## 2.17 `inv_stock_adjustment` (+ `_dtl`) â€” manual adjustment (INV_1102) & opening stock (INV_1101)
**Purpose:** one document for any manual change with a reason. `adjustment_type` distinguishes opening vs correction vs damage vs count-variance. Headerâ†’lines `_dtl`.
```sql
CREATE TABLE inv_stock_adjustment (
    adjustment_no   BIGSERIAL PRIMARY KEY,
    company_no      BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no       BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    adjustment_id   VARCHAR(40) NOT NULL,       -- generated via sys_doc_sequence
    adjustment_date DATE  NOT NULL,
    warehouse_no    BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    adjustment_type SMALLINT NOT NULL DEFAULT 1, -- 1=Correction,2=Opening,3=Damage,4=Loss/Theft,5=CountVariance,6=Revaluation
    reason_code     SMALLINT,
    reason_text     VARCHAR(250),
    status          SMALLINT NOT NULL DEFAULT 1, -- 1=Draft,2=Submitted,3=Approved/Posted,4=Cancelled
    fin_year_no     BIGINT NOT NULL REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    fin_period_no   BIGINT REFERENCES sys_fin_year_dtl(fin_period_no) ON DELETE RESTRICT,
    total_in_value  NUMERIC(20,4) NOT NULL DEFAULT 0,
    total_out_value NUMERIC(20,4) NOT NULL DEFAULT 0,
    gl_voucher_no   BIGINT,                       -- set when posted to fin module
    submitted_by BIGINT, submitted_at TIMESTAMPTZ,
    approved_by  BIGINT, approved_at  TIMESTAMPTZ,
    posted_at    TIMESTAMPTZ,
    remarks      TEXT,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_adj_type   CHECK (adjustment_type IN (1,2,3,4,5,6)),
    CONSTRAINT chk_inv_adj_status CHECK (status IN (1,2,3,4)),
    CONSTRAINT chk_inv_adj_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_adj_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_adj_id ON inv_stock_adjustment(branch_no, adjustment_id) WHERE is_deleted = 0;
CREATE INDEX idx_inv_adj_status ON inv_stock_adjustment(branch_no, status) WHERE is_deleted = 0;
CREATE INDEX idx_inv_adj_wh_date ON inv_stock_adjustment(warehouse_no, adjustment_date);

CREATE TABLE inv_stock_adjustment_dtl (
    adjustment_dtl_no BIGSERIAL PRIMARY KEY,
    adjustment_no  BIGINT NOT NULL REFERENCES inv_stock_adjustment(adjustment_no) ON DELETE CASCADE,
    line_no        INTEGER NOT NULL,
    product_no     BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no     BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    batch_no       BIGINT REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,
    rack_no        BIGINT REFERENCES inv_rack(rack_no) ON DELETE RESTRICT,
    uom_no         BIGINT NOT NULL REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    direction      SMALLINT NOT NULL,            -- +1 increase, -1 decrease
    qty            NUMERIC(18,4) NOT NULL,        -- in entered UOM
    qty_base       NUMERIC(18,4) NOT NULL,        -- normalized to base UOM
    unit_cost      NUMERIC(20,6) NOT NULL DEFAULT 0,
    line_value     NUMERIC(20,4) NOT NULL DEFAULT 0,
    system_qty     NUMERIC(18,4),                 -- on-hand snapshot at entry (for count variance)
    remarks        VARCHAR(250),
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_inv_adj_line UNIQUE (adjustment_no, line_no),
    CONSTRAINT chk_inv_adjdtl_dir CHECK (direction IN (-1,1)),
    CONSTRAINT chk_inv_adjdtl_qty CHECK (qty > 0)
);
CREATE INDEX idx_inv_adjdtl_hdr ON inv_stock_adjustment_dtl(adjustment_no);
```
**Lifecycle:** rows mutable only while `status=Draft`. `Approved` triggers posting (Â§3). Cancelling a posted adjustment writes reversal ledger rows (never edits history).

## 2.18 `inv_stock_transfer` (+ `_dtl`) â€” inter-warehouse / inter-branch transfer (INV_2003)
**Purpose:** move stock between warehouses with a two-phase (dispatch/receive) flow using the transit warehouse so in-transit stock is always visible and never lost.
```sql
CREATE TABLE inv_stock_transfer (
    transfer_no    BIGSERIAL PRIMARY KEY,
    company_no     BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    transfer_id    VARCHAR(40) NOT NULL,
    transfer_date  DATE NOT NULL,
    from_branch_no   BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    from_warehouse_no BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    to_branch_no     BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    to_warehouse_no  BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    transit_warehouse_no BIGINT REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    status         SMALLINT NOT NULL DEFAULT 1,   -- 1=Draft,2=Submitted,3=Dispatched,4=Received,5=ShortReceived,6=Cancelled
    total_qty      NUMERIC(18,4) NOT NULL DEFAULT 0,
    total_value    NUMERIC(20,4) NOT NULL DEFAULT 0,
    dispatched_by BIGINT, dispatched_at TIMESTAMPTZ,
    received_by   BIGINT, received_at   TIMESTAMPTZ,
    remarks       TEXT,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_xfer_status CHECK (status IN (1,2,3,4,5,6)),
    CONSTRAINT chk_inv_xfer_diff   CHECK (from_warehouse_no <> to_warehouse_no),
    CONSTRAINT chk_inv_xfer_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_xfer_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_xfer_id ON inv_stock_transfer(company_no, transfer_id) WHERE is_deleted = 0;
CREATE INDEX idx_inv_xfer_status ON inv_stock_transfer(status) WHERE is_deleted = 0;
CREATE INDEX idx_inv_xfer_from ON inv_stock_transfer(from_warehouse_no, transfer_date);

CREATE TABLE inv_stock_transfer_dtl (
    transfer_dtl_no BIGSERIAL PRIMARY KEY,
    transfer_no    BIGINT NOT NULL REFERENCES inv_stock_transfer(transfer_no) ON DELETE CASCADE,
    line_no        INTEGER NOT NULL,
    product_no     BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no     BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    batch_no       BIGINT REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,
    uom_no         BIGINT NOT NULL REFERENCES inv_uom(uom_no) ON DELETE RESTRICT,
    qty_sent       NUMERIC(18,4) NOT NULL,
    qty_sent_base  NUMERIC(18,4) NOT NULL,
    qty_received   NUMERIC(18,4),                 -- set on receipt; short = qty_received < qty_sent
    qty_received_base NUMERIC(18,4),
    unit_cost      NUMERIC(20,6) NOT NULL DEFAULT 0,
    line_value     NUMERIC(20,4) NOT NULL DEFAULT 0,
    remarks        VARCHAR(250),
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_inv_xfer_line UNIQUE (transfer_no, line_no),
    CONSTRAINT chk_inv_xferdtl_qty CHECK (qty_sent > 0)
);
CREATE INDEX idx_inv_xferdtl_hdr ON inv_stock_transfer_dtl(transfer_no);
```

## 2.19 `inv_physical_count` (+ `_dtl`) â€” stocktake / cycle count
**Purpose:** capture counted vs. system qty per cell; posting generates variance adjustment(s).
```sql
CREATE TABLE inv_physical_count (
    count_no      BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no     BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    count_id      VARCHAR(40) NOT NULL,
    count_date    DATE NOT NULL,
    warehouse_no  BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    count_type    SMALLINT NOT NULL DEFAULT 1,    -- 1=Full,2=Cycle,3=Spot
    status        SMALLINT NOT NULL DEFAULT 1,    -- 1=Draft,2=Counting,3=Review,4=Posted,5=Cancelled
    freeze_stock  SMALLINT NOT NULL DEFAULT 0,    -- block movements on counted cells while counting
    variance_adjustment_no BIGINT REFERENCES inv_stock_adjustment(adjustment_no) ON DELETE SET NULL,
    remarks TEXT,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_count_status CHECK (status IN (1,2,3,4,5)),
    CONSTRAINT chk_inv_count_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_count_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_inv_count_id ON inv_physical_count(branch_no, count_id) WHERE is_deleted = 0;

CREATE TABLE inv_physical_count_dtl (
    count_dtl_no BIGSERIAL PRIMARY KEY,
    count_no    BIGINT NOT NULL REFERENCES inv_physical_count(count_no) ON DELETE CASCADE,
    line_no     INTEGER NOT NULL,
    product_no  BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no  BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    batch_no    BIGINT REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,
    system_qty  NUMERIC(18,4) NOT NULL,
    counted_qty NUMERIC(18,4) NOT NULL,
    variance_qty NUMERIC(18,4) GENERATED ALWAYS AS (counted_qty - system_qty) STORED,
    unit_cost   NUMERIC(20,6) NOT NULL DEFAULT 0,
    remarks VARCHAR(250),
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_inv_count_line UNIQUE (count_no, line_no)
);
```

## 2.20 `inv_product_price` â€” branch / tier price overrides (optional, supports promotions)
**Purpose:** branch-specific or customer-tier-specific selling prices with validity windows; consumed by sales pricing resolution.
```sql
CREATE TABLE inv_product_price (
    price_no     BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no    BIGINT REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,   -- NULL = all branches
    product_no   BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no   BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    price_tier   SMALLINT NOT NULL DEFAULT 1,    -- 1=Retail,2=Wholesale,3=Corporate (matches sal_customer.customer_type)
    sale_price   NUMERIC(20,4) NOT NULL,
    min_qty      NUMERIC(18,4) NOT NULL DEFAULT 1,  -- qty-break pricing
    effective_from DATE NOT NULL,
    effective_to   DATE,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_price_dates CHECK (effective_to IS NULL OR effective_to >= effective_from),
    CONSTRAINT chk_inv_price_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_price_deleted CHECK (is_deleted IN (0,1))
);
CREATE INDEX idx_inv_price_lookup ON inv_product_price(product_no, variant_no, price_tier, branch_no) WHERE is_deleted = 0;
```

## 2.21 `inv_bundle_dtl` â€” kit/bundle composition (for product_type=4)
```sql
CREATE TABLE inv_bundle_dtl (
    bundle_dtl_no   BIGSERIAL PRIMARY KEY,
    bundle_product_no BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE CASCADE,
    component_product_no BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    component_variant_no BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    qty_base       NUMERIC(18,4) NOT NULL,
    is_active SMALLINT NOT NULL DEFAULT 1, is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ, deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_bundle_qty CHECK (qty_base > 0),
    CONSTRAINT chk_inv_bundle_active CHECK (is_active IN (0,1)),
    CONSTRAINT chk_inv_bundle_deleted CHECK (is_deleted IN (0,1)),
    CONSTRAINT chk_inv_bundle_noself CHECK (bundle_product_no <> component_product_no)
);
CREATE UNIQUE INDEX uq_inv_bundle ON inv_bundle_dtl(bundle_product_no, component_product_no, COALESCE(component_variant_no,0)) WHERE is_deleted = 0;
```

## 2.22 `inv_reservation` â€” soft stock allocation
**Purpose:** track `qty_reserved` provenance so reservations can be released precisely when a sale/hold is cancelled/expired.
```sql
CREATE TABLE inv_reservation (
    reservation_no BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL,
    warehouse_no BIGINT NOT NULL REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT,
    product_no   BIGINT NOT NULL REFERENCES inv_product(product_no) ON DELETE RESTRICT,
    variant_no   BIGINT REFERENCES inv_product_variant(variant_no) ON DELETE RESTRICT,
    batch_no     BIGINT REFERENCES inv_batch(batch_no) ON DELETE RESTRICT,
    qty_base     NUMERIC(18,4) NOT NULL,
    ref_doc_type SMALLINT NOT NULL,   -- 4=SalInvoice(hold/draft)
    ref_doc_no   VARCHAR(40) NOT NULL,
    status       SMALLINT NOT NULL DEFAULT 1,  -- 1=Active,2=Released,3=Consumed,4=Expired
    expires_at   TIMESTAMPTZ,
    created_by BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ, row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_inv_resv_status CHECK (status IN (1,2,3,4)),
    CONSTRAINT chk_inv_resv_qty CHECK (qty_base > 0)
);
CREATE INDEX idx_inv_resv_ref ON inv_reservation(ref_doc_type, ref_doc_no);
CREATE INDEX idx_inv_resv_active ON inv_reservation(warehouse_no, product_no, status) WHERE status = 1;
```

## 2.23 `sys_doc_sequence` â€” shared document numbering (used by inv/sal/pur)
**Purpose:** gap-tolerant, per (company, branch, doc_type, fin_year) running number for all transactional document IDs. Defined once here; referenced by `sal_*` and `pur_*`.
```sql
CREATE TABLE sys_doc_sequence (
    doc_sequence_no BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no    BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    doc_type     VARCHAR(20) NOT NULL,    -- 'INV_ADJ','INV_XFER','PUR_PO','PUR_INV','SAL_INV','SAL_RET',...
    fin_year_no  BIGINT REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    prefix       VARCHAR(20) NOT NULL DEFAULT '',
    suffix       VARCHAR(20) NOT NULL DEFAULT '',
    next_no      BIGINT NOT NULL DEFAULT 1,
    padding      SMALLINT NOT NULL DEFAULT 6,
    reset_policy SMALLINT NOT NULL DEFAULT 2,  -- 1=Never,2=PerFinYear,3=PerMonth
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_sys_doc_seq UNIQUE (company_no, branch_no, doc_type, COALESCE(fin_year_no,0))
);
```
**Allocation:** `UPDATE sys_doc_sequence SET next_no = next_no + 1 WHERE â€¦ RETURNING next_no` inside the document transaction (atomic, row-locked). Format: `{prefix}-{yyShort}-{padded}` â†’ e.g. `ADJ-2526-000123`.

## 2.24 `sys_event_outbox` â€” transactional outbox (shared, event architecture)
**Purpose:** reliably publish domain events (low-stock, posted-invoice, near-expiry) in the **same transaction** as the data change; a relay drains it to the message bus / async handlers.
```sql
CREATE TABLE sys_event_outbox (
    event_no     BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL,
    branch_no    BIGINT,
    aggregate_type VARCHAR(40) NOT NULL,   -- 'INV_STOCK','SAL_INVOICE',...
    aggregate_id   VARCHAR(60) NOT NULL,
    event_type   VARCHAR(60) NOT NULL,     -- 'LowStockDetected','StockMovementPosted',...
    payload      JSONB NOT NULL,
    status       SMALLINT NOT NULL DEFAULT 1,  -- 1=Pending,2=Published,3=Failed
    retry_count  SMALLINT NOT NULL DEFAULT 0,
    available_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    published_at TIMESTAMPTZ,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_sys_outbox_status CHECK (status IN (1,2,3))
);
CREATE INDEX idx_sys_outbox_pending ON sys_event_outbox(status, available_at) WHERE status IN (1,3);
```

## 2.25 Relationship summary (ERD in words)
- `inv_category` self-tree; `inv_product` â†’ category/brand/uom/vat_tax. `inv_product` 1â€”N `inv_product_variant`, 1â€”N `inv_product_barcode`, 1â€”N `inv_uom_conversion`, 1â€”N `inv_bundle_dtl`.
- `inv_warehouse` (branch) 1â€”N `inv_rack`. `inv_batch`/`inv_serial` â†’ product/variant.
- **`inv_stock` (cell)** keyed by (warehouse, product, variant, batch); **`inv_stock_ledger`** N rows per cell (history); `inv_valuation_layer` N receipt layers per cell.
- Documents: `inv_stock_adjustment`/`inv_stock_transfer`/`inv_physical_count` (+`_dtl`) â†’ on post emit `inv_stock_ledger` rows + update `inv_stock` (+ `fin_voucher` in fin module).
- Cross-module: `pur_invoice`/`pur_return` post movement_type 2/3; `sal_invoice`/`sal_return` post 4/5 (see sal-db.md / pur-db.md).

---

# 3. BUSINESS LOGIC

## 3.1 The stock-posting engine (single choke point)
Everything that changes quantity calls **one** internal service: `InvStockPostingService.post(StockPostingCommand)`. No controller, no other module, writes `inv_stock` directly. A command is a list of legs; each leg = (warehouse, product, variant, batch, direction, qtyBase, unitCost?, refDocâ€¦). The engine, inside a single `@Transactional`:

1. **Resolve & validate** product is `is_stock_tracked`; batch required iff `is_batch_tracked`; serials match qty iff `is_serial_tracked`; period open (`sys_fin_year_dtl.period_status=Open`, year `is_closed=0`); warehouse live.
2. **Lock the cell:** `SELECT â€¦ FROM inv_stock WHERE cell FOR UPDATE`. If absent, insert the cell row (handle the unique-violation race via insert-on-conflict, then re-select for update).
3. **Compute cost & new balance** per costing method (Â§3.5).
4. **Guard negative stock:** if OUT and `qty_on_hand - qty < 0` and `warehouse.allow_negative_stock=0` â†’ throw `ValidationException`.
5. **Append ledger row** (`inv_stock_ledger`) with `balance_after`, `avg_cost_after`, period, ref.
6. **Update balance** (`inv_stock`): on-hand Â± qty, recompute `avg_cost`, set `last_movement_at`. (`qty_available`,`stock_value` are generated.)
7. **FIFO/LIFO:** create/consume `inv_valuation_layer` rows; the consumed cost feeds the OUT leg's `unit_cost`.
8. **Serial moves:** flip `inv_serial.serial_status` and stamp in/out refs.
9. **Emit events** to `sys_event_outbox` (StockMovementPosted; LowStockDetected if `on_hand <= reorder_level`).

The whole command is atomic: any leg failing rolls back all legs. Documents call this engine; reversals call it with negated directions and `is_reversal=1` + `reversed_ledger_no`.

## 3.2 State machines
**Adjustment (INV_1102):** `Draft â€”submitâ†’ Submitted â€”approveâ†’ Posted â€”cancelâ†’ Cancelled(reversed)`. Edits allowed only in Draft. `Submittedâ†’Posted` requires `can_approve`. Posting calls the engine; cancel posts reversal legs and a reversing `fin_voucher`.
**Transfer (INV_2003):**
```
Draft â†’ Submitted â†’ Dispatched â†’ Received
                         â”‚            â””â†’ ShortReceived (variance lines)
                         â””â†’ (no receive) stays "in transit" â†’ aging
Draft/Submitted â†’ Cancelled (no ledger impact)
Dispatched â†’ cannot cancel; must Receive or ShortReceive then adjust
```
- **Dispatch:** OUT from `from_warehouse`, IN to `transit_warehouse` (movement 6/7). Stock visible in transit.
- **Receive:** OUT from transit, IN to `to_warehouse`. `qty_received < qty_sent` â†’ ShortReceived; the shortfall remains in transit until written off via adjustment.
**Opening (INV_1101):** `Draft â†’ Posted`; blocked once the year has any non-opening ledger row (prevents rewriting history under live data).
**Physical count:** `Draft â†’ Counting (optional freeze) â†’ Review â†’ Posted(variance adj) â†’ (Cancelled)`.

## 3.3 Validation rules (representative, enforced in service layer)
- Product/variant belongs to the command's company; warehouse belongs to the branch.
- Batch product matches line product; expiry not past for OUT of sale type (configurable warn/block); FEFO suggested batch for expiry-tracked OUT.
- UOM allowed for product (exists in `inv_uom_conversion` or equals base UOM); `qty_base = qty * factor` computed server-side â€” never trust client base qty.
- Adjustment line `direction`/`qty>0`; transfer `fromâ‰ to`; count `counted_qty>=0`.
- Tracking-flag and base-UOM immutability once movements exist.
- Period/year open; document date within the resolved period.

## 3.4 Transaction boundaries, race conditions, rollback
- One DB transaction per posted document (header+lines+ledger+balance+layers+voucher+outbox). Spring `@Transactional` on the service method.
- **Race:** two concurrent OUTs on the same cell are serialized by `FOR UPDATE`. New-cell creation race handled by unique `uq_inv_stock_cell` + retry. Document-number race handled by atomic `UPDATE â€¦ RETURNING`.
- **Optimistic lock** (`row_version`) guards document header edits (409 to client â†’ reload).
- **Rollback:** any validation/constraint failure aborts the whole posting; partial stock writes are impossible because ledger+balance share the transaction.
- **Idempotency:** posting keyed by `(ref_doc_type, ref_doc_no)`; re-posting the same posted document is a no-op (checked before legs run). Sales sync uses `client_uuid` (see sal-db.md).

## 3.5 Costing (FIFO / LIFO / Weighted-Average / Standard)
Company setting `costing_method`. Engine computes OUT cost and IN effect:
- **Weighted-Average (default):** on IN: `new_avg = (on_hand*avg + qty*in_cost) / (on_hand+qty)` (guard divide-by-zero / negative on-hand â†’ fall back to last_cost). OUT uses current `avg_cost`. No layers.
- **FIFO:** IN creates a layer (`remaining=qty`, `unit_cost`). OUT consumes oldest non-exhausted layers by `receipt_date, layer_no`; weighted cost of consumed layers = OUT unit_cost. `inv_stock.avg_cost` kept in sync as Î£(layer remaining*cost)/Î£remaining for reporting.
- **LIFO:** same as FIFO but newest layers first.
- **Standard:** OUT uses `inv_product.cost_price`; variance (actualâˆ’standard) on receipt posts to a purchase-price-variance GL account.
- **Batch costing:** when batch-tracked, costing is per-batch (the batch *is* the layer); avg collapses to the batch's `received_cost`.
- **Revaluation:** `adjustment_type=6` sets a new unit cost; engine writes a value-only delta (qty 0 not allowed â†’ uses a paired in/out at old/new cost, or a dedicated revaluation ledger row with qty in the cost field) and a GL revaluation voucher.

## 3.6 Batch & expiry logic
- Receiving batch-tracked goods requires/creates `inv_batch`. Picking for sale/transfer of expiry-tracked goods defaults to **FEFO** (earliest `expiry_date` with available qty). Expired batches excluded from default picking; selling them requires `can_approve` override and is flagged.
- Near-expiry job (scheduled): batches with `expiry_date <= today + alert_days` â†’ outbox `NearExpiryDetected` â†’ notification + report.

## 3.7 Reorder logic
On every OUT post, if `qty_on_hand <= reorder_level` (from `inv_reorder` else product default) â†’ outbox `LowStockDetected`. Replenishment report computes suggested order qty = `max(reorder_qty, max_stock - on_hand - on_order)` where `on_order` = open PO qty (from `pur_order`).

## 3.8 Ledger impact (GL posting â€” contract to FIN module)
Each posted inventory document emits a balanced `fin_voucher` (Dr=Cr). Accounts resolved from `fin_account` mapping (category default / company config). Posting matrix:
| Event | Debit | Credit |
|---|---|---|
| Opening stock | Inventory (asset) | Opening Balance Equity |
| Adjustment IN (gain) | Inventory | Inventory Adjustment (income/expense contra) |
| Adjustment OUT (loss/damage) | Inventory Adjustment / Loss | Inventory |
| Transfer (intra-branch) | *(no GL)* â€” same legal entity, balance moves only | â€” |
| Transfer (inter-branch) | Inventory(to-branch) | Inventory(from-branch) via Inter-Branch Current A/C |
| Count variance | per IN/OUT above | |
| Revaluation up/down | Inventory / Revaluation expense | Revaluation reserve / Inventory |
> COGS/Inventory-relief for sales is posted by the **sales** document (perpetual): Dr COGS, Cr Inventory (see sal-db.md Â§3). Purchase receipt posts Dr Inventory, Cr GRN-Clearing/AP (see pur-db.md Â§3). Inventory module never double-posts those.

## 3.9 Unit conversion logic
`qtyBase = enteredQty Ã— factor(product, enteredUom)`; `factor` = 1 if `enteredUom = base_uom`, else from `inv_uom_conversion`. Display converts back: `displayQty = baseQty / factor`. All storage (`inv_stock`, `inv_stock_ledger`) is base UOM only â€” eliminates rounding drift. Barcode scans resolve UOM+pack_qty from `inv_product_barcode`.

---

# 4. FRONTEND IMPLEMENTATION PLAN

> Stack: Angular 21 standalone + signals, OnPush, reactive forms, `model.service.ts` (view-model) + `data.service.ts` (HTTP) per form, `app-common-table`, `app-page-form-layout`, `.form-section-card`/`.field-group`, permission-aware buttons (`canInsert()/canUpdate()/canDelete()/canApprove()`), snake_case DTO fields.

## 4.1 Screens / routes (`inv.routes.ts`, lazy)
| Form | Route | Pattern |
|---|---|---|
| INV_1001 Product Master | `inv/forms/inv1001` | master-detail (product header + variants/barcodes/UOM tabs) |
| INV_1002 Barcode Generator | `inv/forms/inv1002` | grid + label print |
| INV_1101 Opening Stock | `inv/forms/inv1101` | document (header + lines grid) |
| INV_1102 Stock Adjustment | `inv/forms/inv1102` | document + approval |
| INV_2001 Warehouse | `inv/forms/inv2001` | master-detail (list + form) |
| INV_2002 Rack/Shelf | `inv/forms/inv2002` | master-detail under warehouse |
| INV_2003 Stock Transfer | `inv/forms/inv2003` | document + dispatch/receive |
| INV_2004 Batch & Expiry | `inv/forms/inv2004` | list/filter + expiry dashboard |
| INV_2005 Reorder Level | `inv/forms/inv2005` | editable grid |
| Product List / Stock Report / Category | `inv/pages/*` | already scaffolded (`product-list`, `stock-report`, `category-list`, `stock-adjustment`) |

## 4.2 INV_1001 Product Master (reference screen)
- **Layout:** `app-page-form-layout` with a left product list (`app-common-table`, searchable, paginated, `(rowClick)` loads detail) and a right form area with **page tabs** (`.form-tab-btn`): *General Â· Pricing Â· Inventory Â· Variants Â· Barcodes Â· UOM*.
- **Forms:** `FormGroup` with nested `FormArray`s `variants`, `barcodes`, `uom_conversions`; signals `isLoading`, `isSaving`, `selectedProductNo`. Field names snake_case (`product_id`, `category_no`, `is_batch_tracked`â€¦).
- **Tracking flags** rendered as `.custom--checkbox`; disabled (read-only) when the product already has movements (server returns `has_movements` flag) â€” show a tooltip "locked: stock exists".
- **Variant matrix builder:** pick attributes + values â†’ "Generate combinations" fills the `variants` FormArray (mirrors SYS1003 `autoGeneratePeriods()` pattern). Each row inline-editable (SKU, barcode, prices).
- **Search:** server-side typeahead (`pg_trgm`) via `getList?q=`; debounce 300ms.
- **Validation/Error UX:** inline `ValidationMessage` directive; toast on save (ngx-toastr); 409 â†’ "changed by another user, reloading".
- **Loading states:** skeleton rows in the list; spinner overlay on form during load/save.
- **Permission rendering:** Save hidden unless `canInsert()||canUpdate()`; Delete hidden unless `canDelete()`.

## 4.3 INV_1102 Stock Adjustment (document + approval)
- Header card (warehouse, date, type, reason) + lines grid (`app-common-table` with editable cells: product picker â†’ batch picker if batch-tracked â†’ uom â†’ direction â†’ qty â†’ cost auto-filled from `inv_stock.avg_cost`, `line_value` computed).
- **Product picker modal** (`data-list-modal`): scan barcode field at top (autofocus); Enter adds a line. Footer totals via `bottomTotal: true` columns.
- **Status bar** with workflow buttons: Save Draft, Submit (`statusâ†’2`), Approve & Post (`canApprove()`), Cancel. Buttons enable per current status.
- **Barcode handling:** a hidden focused input captures scanner keystrokes (scanners type fast + Enter); resolve via `/inv/forms/inv1102/barcode/{code}`.

## 4.4 INV_2003 Stock Transfer
- Two-warehouse header; lines grid with `qty_sent`; on Receive screen a `qty_received` column appears with variance highlight. Status chips (Draft/Dispatched/Received) via `cellType: 'badge'`.

## 4.5 INV_2004 Batch & Expiry dashboard
- Filter bar (product, warehouse, expiry window). Color-coded rows: red=expired, amber=â‰¤30d. Export. Drill to batch stock.

## 4.6 Reusable components & cross-cutting
- `inv-product-picker` (shared modal with barcode + search), `inv-batch-picker`, `inv-warehouse-select`, `qty-uom-input` (qty + UOM dropdown that shows base-qty hint).
- **Mobile responsiveness:** Tailwind breakpoints; document line grids switch to stacked cards below `lg`; POS-like adjustment usable on tablet.
- **Keyboard shortcuts (document forms):** `F2` new line, `F4` product picker, `Ctrl+S` save, `Ctrl+Enter` submit/post, `Esc` close modal, `â†‘/â†“` row nav.
- **Printing:** barcode labels (INV_1002) â†’ print stylesheet / label template (configurable label size, batch+price+barcode); adjustment/transfer note printable A4. Use a `print-layout` component + `window.print()` scoped CSS.

---

# 5. BACKEND & MIDDLEWARE PLAN

> Spring Boot 4, package `com.infoaidtech.aidly.inv`, layered controllerâ†’serviceâ†’repositoryâ†’entity with DTO+MapStruct. Form-wise controllers at `/api/v1/inv/forms/{formId}`; generic CRUD at `/api/v1/inv/{resource}` for API consumers. All responses `ApiResponse<T>` (`status_code` snake_case). DTOs snake_case.

## 5.1 Representative endpoints
**INV_1001 Product (`/api/v1/inv/forms/inv1001`)**
| Method | Path | Body/Result |
|---|---|---|
| GET | `/products` | `List<Inv1001ProductDto>` (asc by product_no) |
| GET | `/products/page?page&size&sort&q` | `Page<Inv1001ProductDto>` |
| GET | `/products/{productNo}` | detail incl. variants/barcodes/uom |
| POST | `/products` | upsert (insert if `product_no` null) â†’ `Inv1001ProductDto` |
| DELETE | `/products/{productNo}` | soft-delete (409 if movements) |
| GET | `/barcode/{code}` | resolve `{product_no,variant_no,uom_no,pack_qty}` |
| GET | `/lookups` | categories/brands/uoms/tax for selects |

**INV_1102 Adjustment (`/forms/inv1102`)**: `GET /adjustments`, `/adjustments/page`, `/adjustments/{no}`, `POST /adjustments` (draft upsert), `POST /adjustments/{no}/submit`, `POST /adjustments/{no}/approve`, `POST /adjustments/{no}/cancel`, `DELETE /adjustments/{no}`.
**INV_2003 Transfer**: `â€¦ /transfers`, `POST /transfers/{no}/dispatch`, `POST /transfers/{no}/receive`.
**Stock queries (generic)**: `GET /api/v1/inv/stock?warehouseNo&productNo`, `GET /inv/stock/availability?warehouseNo&productNo&variantNo`, `GET /inv/ledger?productNo&from&to`.

## 5.2 DTO structure (snake_case, Jakarta validation)
```java
@Data public class Inv1102AdjustmentDto {
  private Long adjustment_no;
  private String adjustment_id;            // server-generated; client sends null
  @NotNull private LocalDate adjustment_date;
  @NotNull private Long warehouse_no;
  @NotNull private Short adjustment_type;  // 1..6
  private Short reason_code; private String reason_text;
  private Short status;                     // server-controlled via action endpoints
  private String remarks; private Short is_active; private Long row_version;
  @NotEmpty @Valid private List<Inv1102AdjustmentLineDto> lines;
}
@Data public class Inv1102AdjustmentLineDto {
  private Long adjustment_dtl_no; private Integer line_no;
  @NotNull private Long product_no; private Long variant_no; private Long batch_no; private Long rack_no;
  @NotNull private Long uom_no; @NotNull private Short direction;
  @NotNull @DecimalMin("0.0001") private BigDecimal qty;
  private BigDecimal unit_cost; private String remarks;
}
```
MapStruct mapper ignores audit fields (`createdBy/At`, `updatedBy/At`, `isDeleted`, `deletedBy/At`, `rowVersion`) per house rule; on update also ignores PK + business id.

## 5.3 Middleware / cross-cutting
- **Auth:** `JwtAuthenticationFilter` sets `CompanyBranchContext` (companyNo, branchNo, userNo, sessionNo) from JWT; module reads tenant from context, never from client.
- **RBAC:** `RbacAuthorizationInterceptor` maps routeâ†’form_id (`INV_1102`) and checks `can_view/insert/update/delete/approve`. Action endpoints (`/approve`) require `can_approve`.
- **Validation middleware:** `@Valid` on bodies â†’ `MethodArgumentNotValidException` â†’ 400 field-error map (GlobalExceptionHandler). Deep business validation in service throws `ValidationException`/`NotFoundException`.
- **Inventory locking:** posting service uses `@Lock(PESSIMISTIC_WRITE)` repo method / `SELECT â€¦ FOR UPDATE` on `inv_stock`. Document headers use optimistic `row_version`.
- **Transactions:** `@Transactional` on write services; `@Transactional(readOnly=true)` on reads.
- **Audit middleware:** existing audit interceptor writes `sys_audit_log` (old/new JSON) for every entity insert/update/soft-delete.
- **Idempotency:** posting keyed by `(ref_doc_type, ref_doc_no)`; an `Idempotency-Key` header accepted on POST mutations and stored to dedupe retries.
- **Rate limiting:** reuse `LoginAttemptLimiter` pattern; add a bucket filter on heavy endpoints (barcode resolve, stock query) per user â€” e.g. 50 rps.
- **Logging:** Lombok `@Slf4j`; `sys_log` request log (status, duration, uri) by existing filter; structured logs include `refDoc` on posting.
- **Error handling:** `GlobalExceptionHandler` â†’ 400/404/409/500 envelopes; negative-stock & period-closed are `ValidationException` (400) with actionable messages.
- **Retry:** posting wrapped with retry on `CannotAcquireLockException`/serialization failure (max 3, backoff). Outbox relay retries failed publishes (`retry_count`).

## 5.4 Queue / event architecture
- **Transactional outbox** (`sys_event_outbox`) written in the posting tx. A `@Scheduled` relay (or Debezium later) publishes to **Redis Streams / RabbitMQ**. Events: `StockMovementPosted`, `LowStockDetected`, `NearExpiryDetected`, `TransferDispatched/Received`, `AdjustmentPosted`.
- **Consumers:** notification service (in-app/email/SMS), analytics/materialized-view refresher, GL poster (if async posting chosen), reorder suggestion builder.
- For current SME scale, Spring `ApplicationEventPublisher` + `@TransactionalEventListener(AFTER_COMMIT)` is acceptable as phase-1; outbox makes the move to a broker non-breaking.

---

# 6. REPORTING & ANALYTICS

## 6.1 Essential reports
Stock on hand (by warehouse/branch/category/brand), Stock valuation (avg/FIFO), Stock ledger / movement history, Batch-wise stock & **expiry/near-expiry**, Reorder/low-stock, Stock-transfer register & in-transit aging, Adjustment register, Stock-aging (slow/dead stock), ABC analysis, Variant-matrix stock, Negative-stock exceptions, Inventory turnover, Physical-count variance.

## 6.2 KPIs / dashboard metrics
Total stock value, # SKUs below reorder, # near-expiry/expired lines & value, in-transit value, inventory turnover ratio, dead-stock value (no movement > N days), GMROI (needs sales margin), stock accuracy (count variance %).

## 6.3 Aggregation logic & materialized views
- `mv_inv_stock_summary` (product/variant Ã— warehouse: on_hand, available, value, last_movement) â€” refreshed on `StockMovementPosted` (debounced) or `REFRESH MATERIALIZED VIEW CONCURRENTLY` on schedule.
- `mv_inv_valuation_by_category` (branch Ã— category: qty, value).
- `mv_inv_aging` (buckets 0-30/31-60/61-90/90+ by last_movement_at).
- Period stock = ledger replay: opening (last `balance_after` â‰¤ from-date) + Î£ in âˆ’ Î£ out within range; precompute monthly snapshots (`inv_stock_period_snapshot`) for closed periods to avoid full replay.

## 6.4 Optimization
Reports read MVs / snapshots, not live documents. Heavy date-range ledger queries hit the `idx_inv_ledger_date`/`idx_inv_ledger_product_date` indexes and partition pruning. Export via streaming (Spring `StreamingResponseBody`) for large CSV.

---

# 7. SECURITY & COMPLIANCE

## 7.1 Permission matrix (per form, `sys_role_permission`)
| Form | view | insert | update | delete | approve |
|---|---|---|---|---|---|
| INV_1001 Product | Sales/Store | Store Mgr | Store Mgr | Admin | â€” |
| INV_1101 Opening | Store Mgr | Store Mgr | Store Mgr | Admin | Store Mgr |
| INV_1102 Adjustment | Clerk | Clerk | Clerk(draft) | Admin | **Store Mgr** |
| INV_2003 Transfer | Clerk | Clerk | Clerk(draft) | Admin | Store Mgr (dispatch/receive) |
| INV_2001/2/5 Setup | Store Mgr | Store Mgr | Store Mgr | Admin | â€” |

## 7.2 Access control & sensitive actions
- Branch isolation enforced from `CompanyBranchContext`; users only see their branches (`sys_user_branch`); `has_global_access=1` bypasses for HQ roles.
- Sensitive: approve/post, cancel-posted, cost edit, negative-stock override, costing-method change â†’ require `can_approve` and are written to `sys_audit_log` with reason.
- Cost/valuation columns hidden from non-finance roles (DTO projection per permission).

## 7.3 Audit trail & fraud prevention
- Immutable `inv_stock_ledger` + `sys_audit_log` give full forensic trail (who/when/before/after).
- Fraud signals: repeated downward adjustments by same user, frequent negative-stock overrides, count variances clustering on high-value SKUs, after-hours postings â†’ flagged report.
- Reason mandatory for loss/damage/theft adjustment types; large adjustments above threshold require dual approval.

## 7.4 Data validation strategy
Three layers: client (reactive validators), API (Jakarta `@Valid`), DB (CHECK/UNIQUE/FK). Server recomputes all money/qty/base-UOM â€” client values are inputs only.

---

# 8. PERFORMANCE & SCALABILITY

- **Indexes:** every FK indexed; partial `WHERE is_deleted=0` on lookups; `uq_inv_stock_cell` for O(1) balance lookup; `idx_inv_ledger_cell` for fast running-balance; `pg_trgm` GIN for product search; `idx_inv_batch_expiry` for expiry jobs.
- **Query optimization:** availability check is a single indexed PK-ish lookup on `inv_stock`; never `SUM(ledger)` at runtime for current balance.
- **Caching (Redis):** product catalog & barcodeâ†’product map (read-mostly), UOM/tax/category lookups, warehouse list. Invalidate on product/barcode save via outbox event. Stock balances **not** cached for correctness (or cached with very short TTL + read-through, never write-back).
- **Read/write split:** reports/MVs on a read replica; postings on primary.
- **Partitioning:** `inv_stock_ledger` range-partitioned by month/fin-year; archive closed years to cold storage.
- **Event-driven:** MV refresh, notifications, reorder suggestions off the outbox â€” keep the posting tx short.
- **Horizontal scaling:** stateless API (JWT) scales out; DB is the contention point â€” pessimistic locks are per-cell (fine-grained), so throughput scales with SKU/warehouse spread. Hot SKU? consider per-cell sharded counters only if proven necessary.
- **Multi-tenant readiness:** company_no/branch_no on every row enables shared-schema multitenancy; can graduate to schema-per-tenant or RLS (`CREATE POLICY` on company_no) without model change.

---

# 9. TESTING STRATEGY

- **Unit:** UOM conversion (factors, rounding), weighted-avg & FIFO/LIFO cost math, FEFO batch selection, reorder trigger, doc-number formatting, state-machine transitions (illegal transitions rejected).
- **Integration (Spring Boot, H2/Testcontainers-PG):** post adjustment â†’ ledger+balance+layers+voucher+outbox all written atomically; rollback on a failing leg leaves nothing; soft-delete frees unique slot; period-closed rejection; negative-stock policy both ways.
- **E2E:** product createâ†’opening stockâ†’sale (cross-module)â†’returnâ†’transferâ†’adjustment; barcode scan flow; approval workflow with RBAC.
- **Inventory consistency:** invariant test â€” for every cell, `inv_stock.qty_on_hand == last inv_stock_ledger.balance_after` and `== Î£(direction*qty_base)`; `Î£ layer.remaining == on_hand` (FIFO); `stock_value == on_hand*avg_cost`.
- **Concurrency:** N threads selling the same last unit â†’ exactly one succeeds (or all succeed only if negative allowed); no lost updates; doc-number uniqueness under parallel create.
- **Financial integrity:** every inventory voucher balances (Î£dr=Î£cr); inventory GL control == Î£ `inv_stock.stock_value` per branch at period close.

---

# 10. PRODUCTION DEPLOYMENT NOTES

- **Migration:** Flyway/Liquibase scripts `V{n}__inv_*.sql` in dependency order (lookups â†’ product â†’ warehouse â†’ batch/serial â†’ stock/ledger/layer â†’ documents â†’ sequence/outbox). Add `pg_trgm` extension migration. Backfill: create `inv_stock` cells + `OPENING` ledger from opening-stock entry, not by direct insert.
- **Rollback:** migrations paired with down scripts; never drop ledger/stock in a rollback once live â€” prefer forward fixes. Feature-flag new forms via menu enrollment (`sys_enroll_menu`).
- **Seed data:** base UOMs (PCS/KG/GM/LTR/BOX/CTN/DOZEN), default categories, a default+transit+damage warehouse per branch, costing-method company config, doc-sequence rows per branch/fin-year, sample tax link.
- **Environment:** `DB_URL/USERNAME/PASSWORD`, `JWT_SECRET`, Redis URL, broker URL; profiles local/dev/prod/test; port 7860 (HF).
- **Monitoring:** Actuator health/metrics; alerts on outbox backlog, lock-wait/deadlock rate, posting latency p95, negative-stock count, MV refresh lag.
- **Backup:** PITR (WAL archiving) on PostgreSQL; nightly logical dump; test restore monthly; ledger is append-only so backups are consistent.
- **Disaster recovery:** documented RPO/RTO; ledger replay can rebuild `inv_stock`/layers from `inv_stock_ledger` if the balance table is ever corrupted (recovery procedure: truncate balances â†’ replay ledger ordered by `stock_ledger_no`). Cross-region replica for failover.

---

## Appendix A â€” Enum reference (inventory)
`product_type`:1 StandardÂ·2 VariantParentÂ·3 ServiceÂ·4 Bundle | `warehouse_type`:1 MainÂ·2 OutletÂ·3 TransitÂ·4 DamageÂ·5 Returns | `movement_type`:1 OpeningÂ·2 PurchaseÂ·3 PurReturnÂ·4 SaleÂ·5 SalReturnÂ·6 TransferOutÂ·7 TransferInÂ·8 AdjInÂ·9 AdjOutÂ·10 CountVarianceÂ·11 AssembleÂ·12 Disassemble | `adjustment_type`:1 CorrectionÂ·2 OpeningÂ·3 DamageÂ·4 LossÂ·5 CountVarianceÂ·6 Revaluation | `*_status` adjustment:1 DraftÂ·2 SubmittedÂ·3 PostedÂ·4 Cancelled | transfer:1 DraftÂ·2 SubmittedÂ·3 DispatchedÂ·4 ReceivedÂ·5 ShortReceivedÂ·6 Cancelled | `serial_status`:1 InStockÂ·2 ReservedÂ·3 SoldÂ·4 ReturnedÂ·5 DamagedÂ·6 InTransit | `costing_method`(company): WEIGHTED_AVGÂ·FIFOÂ·LIFOÂ·STANDARD.

## Appendix B â€” Cross-module contracts
- **Purchase â†’ Inventory:** `pur_invoice`/`pur_receipt` call posting engine with movement 2 (IN) at landed cost; `pur_return` movement 3 (OUT). Creates `inv_batch` for batch-tracked receipts. (See pur-db.md Â§3.)
- **Sales â†’ Inventory:** `sal_invoice` reserves then posts movement 4 (OUT) at costing cost (feeds COGS); `sal_return` movement 5 (IN). (See sal-db.md Â§3.)
- **Inventory â†’ Finance:** every posted doc emits `fin_voucher` per Â§3.8 matrix; `vat_tax_no`â†’`sys_vat_tax`; period from `sys_fin_year`/`sys_fin_year_dtl`.


## Shared Frontend and Form Design Overview

# INV Module â€” Perâ€‘Form Design (Business Â· Middleware Â· Frontend)

> Companion to **`inv-db.md`** (DB blueprint). This file defines, **for every INV form**, the
> *business* it serves, the *middleware* (Spring Boot) design, and the *frontend* (Angular 21)
> design â€” at the level we used to build the SYS module (one form = one dedicated
> controller + service + DTOs on the backend, and one `data.service` + `model.service` +
> `component` on the frontend).
>
> **Backend pkg:** `com.infoaidtech.aidly.inv` Â· **Frontend feature:** `apps/web-client/src/app/features/inv`
> **API base:** `/api/v1/inv/forms/{formId}` (formâ€‘wise; generic CRUD at `/api/v1/inv/{resource}` for API consumers only)
> **Conventions:** entities extend `BaseEntity` (company_no + branch_no) for branchâ€‘scoped tables or
> `AuditEntity` for companyâ€‘wide catalog; DTOs snake_case; `ApiResponse<T>`; soft delete via
> `performSoftDelete()`; optimistic lock `row_version`; RBAC by `form_id` (e.g. `INV_1001`).
> Form services map entityâ†”DTO **manually** (same as `Sys1007Service`), not MapStruct, so the
> formâ€‘specific projections stay explicit.

---

## Build order (dependency tiers)

| Tier | Build | Why first |
|---|---|---|
| 0 â€” Lookups | Category, Brand, UOM, Product Attribute (+values) | Product master needs these as selects |
| 1 â€” Catalog | **INV_1001 Product Master**, **INV_1002 Barcode Generator** | everything transacts on products/variants |
| 2 â€” Topology | **INV_2001 Warehouse**, **INV_2002 Rack**, **INV_2005 Reorder** | stock lives in a warehouse |
| 3 â€” Engine | `InvStockPostingService` + `inv_stock` / `inv_stock_ledger` / `inv_valuation_layer` (internal, no UI) | the single write path for all quantity changes |
| 4 â€” Documents | **INV_1101 Opening Stock**, **INV_1102 Stock Adjustment**, **INV_2003 Stock Transfer** | they post through the engine |
| 5 â€” Monitoring | **INV_2004 Batch & Expiry** (read/dashboard), Physical Count | read the ledger/balance the engine wrote |

A form in tier *n* must not be wired until its tier *< n* dependencies exist. The engine (tier 3)
is **not a form** â€” it is an internal `@Service` every document form calls.

---

## A. Shared backend infrastructure (build once, used by many forms)

These are not forms but every document form depends on them.

### A.1 `InvStockPostingService` â€” the single write path (inv-db.md Â§3.1)
- One public method: `post(StockPostingCommand)`; **only** code allowed to write `inv_stock` /
  `inv_stock_ledger` / `inv_valuation_layer`. Controllers and other modules never touch those tables.
- `StockPostingCommand` = `{ refDocType, refDocNo, refDocPk, movementDate, finYearNo, finPeriodNo, legs[] }`;
  each leg = `{ warehouseNo, productNo, variantNo?, batchNo?, direction(+1/-1), qtyBase, unitCost?, refLineNo? }`.
- Per call, inside one `@Transactional`: validate (tracked/period/warehouse) â†’ `SELECT â€¦ FOR UPDATE`
  the cell (insertâ€‘onâ€‘conflict if absent) â†’ cost & new balance per costing method â†’ negativeâ€‘stock
  guard â†’ append ledger row â†’ update balance â†’ FIFO/LIFO layers â†’ serial flips â†’ write
  `sys_event_outbox` (`StockMovementPosted`, `LowStockDetected`).
- **Idempotency:** keyed by `(ref_doc_type, ref_doc_no)`; reâ€‘posting a posted doc is a noâ€‘op.
- **Reversal:** same command with negated directions + `is_reversal=1`, `reversed_ledger_no` set.
- Repo needs `@Lock(PESSIMISTIC_WRITE)` finder on `inv_stock` by cell, and an insertâ€‘onâ€‘conflict path.

### A.2 `InvDocSequenceService` â€” document numbering (inv-db.md Â§2.23)
- `next(companyNo, branchNo, docType, finYearNo)` â†’ atomic `UPDATE sys_doc_sequence SET next_no = next_no+1 â€¦ RETURNING next_no`, formatted `{prefix}-{yy}-{padded}` (e.g. `ADJ-2526-000123`).
- Called when a document leaves Draft (or on first save) â€” never on the client.

### A.3 `InvPeriodGuard` + UOM helper
- `assertOpenPeriod(finYearNo, finPeriodNo, date)` â†’ throws `ValidationException("PERIOD_CLOSED")`.
- `toBaseQty(productNo, uomNo, qty)` â†’ `qty Ã— factor` (factor=1 if base UOM, else `inv_uom_conversion`); **serverâ€‘computed**, client base qty never trusted (inv-db.md Â§3.9).

### A.4 Tenant + RBAC (already exists, reused)
- `JwtAuthenticationFilter` â†’ `CompanyBranchContext` (companyNo/branchNo/userNo/sessionNo). Services read tenant from context.
- `RbacAuthorizationInterceptor` maps route â†’ `form_id`, checks `can_view/insert/update/delete/approve`. Action endpoints (`/approve`, `/dispatch`, `/receive`) require `can_approve`.
- Frontend gates buttons with `permissionService.useLocalPermissions('INV_xxxx')` + `canInsert()/canUpdate()/canDelete()/canApprove()`.

### A.5 Shared frontend pieces (inv-db.md Â§4.6)
`inv-product-picker` (modal: barcode field autofocus + trgm search), `inv-batch-picker`
(FEFOâ€‘sorted), `inv-warehouse-select`, `qty-uom-input` (qty + UOM with baseâ€‘qty hint),
`print-layout` (labels / document notes). Document forms support `F2` new line, `F4` picker,
`Ctrl+S` save, `Ctrl+Enter` submit/post, `Esc` close.

---

## B. Supporting masters (Tier 0 â€” simple CRUD, build before INV_1001)

These follow the **exact SYS1007 pattern** (masterâ€‘detail `page-form-layout`, list + form,
manual toDto). Each = `InvCatXxxController/Service/Dto` + entity + repository + frontend trio.

| Master | Table | Form id (suggested) | Notes |
|---|---|---|---|
| Category | `inv_category` | INV_1003 | hierarchical: parent select, `tree_path`/`depth` recomputed in service; max depth 6; delete blocked if children/products exist |
| Brand | `inv_brand` | INV_1004 | flat CRUD |
| UOM | `inv_uom` | INV_1005 | `uom_type`, `decimal_places`; flat CRUD |
| Attribute + Values | `inv_product_attribute(_value)` | INV_1006 | masterâ€‘detail: attribute header + values grid (FormArray) |

**Middleware (each):** `findByCompanyNoAndIsDeletedOrderByâ€¦Asc`, uniqueâ€‘code guard, `performSoftDelete()`; companyâ€‘scoped (`AuditEntity`, `company_no` from context, `branch_no` null = shared).
**Frontend (each):** `page-form-layout` sidebar list + right form card; search; status select. Category adds a parent `ng-select` (exclude self + descendants) and a tree preview.

---

## 1. INV_1001 â€” Product Master  *(catalog header; the reference screen)*

### Business
The catalog header. `product_type` âˆˆ {Standard, VariantParent, Service, Bundle}. Carries tax class,
default prices, tracking flags (stock/batch/expiry/serial), replenishment defaults. A
`VARIANT_PARENT` is abstract â€” only its `inv_product_variant` rows transact. **Tracking flags and
`base_uom_no` freeze once any ledger row exists.** Deleting is forbidden once movements exist
(discontinue via `is_active=0`). RBAC: view Sales/Store Â· insert/update Store Mgr Â· delete Admin.

### Middleware â€” `/api/v1/inv/forms/inv1001`
Entities/tables: `inv_product` (+ `inv_product_variant`, `inv_product_barcode`, `inv_uom_conversion`, `inv_bundle_dtl`).
DTOs: `Inv1001ProductDto` (header + `has_movements` flag) with nested `List<Inv1001VariantDto>`, `List<Inv1001BarcodeDto>`, `List<Inv1001UomConvDto>`, `List<Inv1001BundleLineDto>`; `Inv1001LookupDto` bundle (categories/brands/uoms/tax/attributes).

| Method | Path | Purpose |
|---|---|---|
| GET | `/products` | list asc by product_no |
| GET | `/products/page?page&size&sort&q` | serverâ€‘side trgm search (debounced) |
| GET | `/products/{productNo}` | detail incl. variants/barcodes/uom/bundle + `has_movements` |
| POST | `/products` | upsert (insert when `product_no` null); reconciles child collections |
| DELETE | `/products/{productNo}` | soft delete; **409 if movements** |
| GET | `/barcode/{code}` | resolve `{product_no,variant_no,uom_no,pack_qty}` |
| GET | `/lookups` | selects for the form |
| POST | `/products/{productNo}/variants/generate` | expand attribute matrix â†’ variant rows |

Service rules: enforce `is_expiry_tracked â‡’ is_batch_tracked`; freeze flags/baseâ€‘UOM when `has_movements`; unique `product_id` & `barcode` per company (live); variant combo unique; bundle components â‰  self. `has_movements` = `inv_stock_ledger` exists for product. Save is one transaction reconciling header + child FormArrays (softâ€‘delete removed children).

### Frontend â€” `inv/forms/inv1001`  (masterâ€‘detail + page tabs)
- `page-form-layout`: left product list (`app-common-table`, searchable/paginated, `(rowClick)`); right form with `.form-tab-btn` tabs: **General Â· Pricing Â· Inventory Â· Variants Â· Barcodes Â· UOM** (+ **Bundle** when type=4).
- `FormGroup` + nested `FormArray`s `variants/barcodes/uom_conversions/bundle`; signals `isLoading/isSaving/selectedProductNo/hasMovements`.
- Tracking flags = `.custom--checkbox`, **disabled with tooltip** when `has_movements`.
- Variant matrix builder: pick attributes+values â†’ "Generate combinations" fills `variants` (mirrors SYS1003 `autoGeneratePeriods()`); rows inlineâ€‘editable.
- trgm typeahead via `/products/page?q=` (300ms debounce); 409 â†’ "changed by another user, reloading"; Save/Delete gated by permissions.

---

## 2. INV_1002 â€” Barcode Generator

### Business
A product/variant may carry many barcodes (supplier EAN, internal, packâ€‘ofâ€‘N). Each barcode is
**UOMâ€‘aware**: scanning a carton barcode resolves to `pack_qty` base units. This form assigns,
generates and **prints** barcode labels. One barcode value is unique per company among live rows.

### Middleware â€” `/api/v1/inv/forms/inv1002`
Tables: `inv_product_barcode`. DTOs: `Inv1002BarcodeDto` (barcode_no, product_no, variant_no?, barcode, uom_no, pack_qty, barcode_type, is_primary), `Inv1002LabelRequest` (list of {barcode_no, copies} + label template).

| Method | Path | Purpose |
|---|---|---|
| GET | `/barcodes?productNo&variantNo` | list for a product |
| POST | `/barcodes` | create/update; one `is_primary` per (product,variant) â€” demote others |
| POST | `/barcodes/generate` | autoâ€‘generate EANâ€‘13/internal codes for selected products/variants |
| DELETE | `/barcodes/{barcodeNo}` | soft delete |
| GET | `/products/lookup?q=` | product/variant picker source |

Service rules: validate `uom_no` allowed for product (base or in `inv_uom_conversion`); `pack_qty>0`; generated EANâ€‘13 checkâ€‘digit; uniqueness guard; demote previous primary.

### Frontend â€” `inv/forms/inv1002`  (grid + label print)
- Top: product picker + filter. Center: `app-common-table` of barcodes (inline edit pack_qty, type, primary radio). Right/secondary: live **barcode preview** (render via a JS barcode lib) + labelâ€‘size config.
- Actions: Generate (bulk), Print â†’ `print-layout` with scoped `@media print` CSS (barcode + name + price + batch); configurable label dimensions.
- A focused hidden input demonstrates scanâ€‘toâ€‘resolve (calls `/barcode/{code}` on the INV_1001 endpoint).

---

## 3. INV_2001 â€” Warehouse Setup

### Business
The physical/virtual stock location, **branchâ€‘scoped**. Types: Main Â· Outlet/SalesFloor Â· Transit
(virtual, for transfers) Â· Damage/Quarantine Â· Returns. Exactly one `is_default` per branch.
`allow_negative_stock` is the perâ€‘warehouse negative policy the posting engine honours.
RBAC: Store Mgr (CRUD) Â· Admin (delete).

### Middleware â€” `/api/v1/inv/forms/inv2001`
Tables: `inv_warehouse` (BaseEntity, branch_no NOT NULL). DTO: `Inv2001WarehouseDto`.

| Method | Path | Purpose |
|---|---|---|
| GET | `/warehouses` | list for active branch asc |
| POST | `/warehouses` | upsert |
| DELETE | `/warehouses/{warehouseNo}` | soft delete (409 if stock/ledger exists) |
| GET | `/manager-options` | hrm_employee options for manager select |

Service rules: branch from context; unique `warehouse_id` per branch; enforce **single default**
(clear other defaults on set, mirror of `Sys1104` default reconcile); block delete when
`inv_stock`/`inv_stock_ledger` rows reference it; recommend autoâ€‘creating Transit + Damage
warehouses when the first Main is created (seed helper).

### Frontend â€” `inv/forms/inv2001`  (masterâ€‘detail, SYS1007 clone)
`page-form-layout` list + form: id, name, type `ng-select`, manager `ng-select`, `is_default` /
`allow_negative_stock` / `is_sale_point` as `.custom--checkbox`, address, status. Default badge in list.

---

## 4. INV_2002 â€” Rack / Shelf Setup

### Business
Bin locations **under a warehouse** (`aisle-rack-shelf-bin`, e.g. `A-01-03`). Optional; used by
adjustment/count lines and putaway. Unique `rack_id` per warehouse.

### Middleware â€” `/api/v1/inv/forms/inv2002`
Tables: `inv_rack`. DTO: `Inv2002RackDto`.

| Method | Path | Purpose |
|---|---|---|
| GET | `/warehouse-options` | warehouses of the branch |
| GET | `/racks?warehouseNo` | racks for a warehouse |
| POST | `/racks` | upsert |
| DELETE | `/racks/{rackNo}` | soft delete (block if referenced by stock) |

### Frontend â€” `inv/forms/inv2002`  (masterâ€‘detail under a warehouse)
Left: warehouse selector + rack list filtered by warehouse. Right: form (aisle/rack/shelf/bin
parts autoâ€‘compose `rack_id`). Same SYS1007 layout.

---

## 5. INV_2005 â€” Reorder Level

### Business
Perâ€‘warehouse override of product reorder defaults; drives **lowâ€‘stock alerts** and **purchase
suggestions**. Key = (warehouse, product, variant). Fields: reorder_level, reorder_qty, min/max
stock, preferred supplier, leadâ€‘time. The engine raises `LowStockDetected` when onâ€‘hand â‰¤
reorder_level (inv-db.md Â§3.7).

### Middleware â€” `/api/v1/inv/forms/inv2005`
Tables: `inv_reorder`. DTO: `Inv2005ReorderDto` (+ product/warehouse labels).

| Method | Path | Purpose |
|---|---|---|
| GET | `/reorders?warehouseNo` | grid for a warehouse (LEFT JOIN product for current onâ€‘hand) |
| POST | `/reorders/bulk` | save the edited grid (reconcile upsert/softâ€‘delete) |
| GET | `/warehouse-options` / `/product-lookup?q=` | selects |

Service rules: unique per (warehouse, product, variant) live; numeric guards (max â‰¥ min, reorder_qty â‰¥ 0).

### Frontend â€” `inv/forms/inv2005`  (editable grid)
Warehouse selector â†’ editable `app-common-table` (add product rows via `inv-product-picker`, inline
edit thresholds). Optional column showing live onâ€‘hand + a red flag when below level. Bulk Save.

---

## 6. INV_1101 â€” Opening Stock

### Business
Seeds goâ€‘live quantities and **perâ€‘unit opening cost** â†’ posts `OPENING` (movement 1) ledger rows
and creates valuation layers. State: `Draft â†’ Posted`. **Locked once the finâ€‘year has any
nonâ€‘opening movement** (can't rewrite history under live data). RBAC: Store Mgr (incl. approve).

### Middleware â€” `/api/v1/inv/forms/inv1101`
Tables: `inv_stock_adjustment` with `adjustment_type=2 (Opening)` (+ `_dtl`). Reuses the adjustment
header but a dedicated controller/DTO/screen. DTOs: `Inv1101OpeningDto` + `Inv1101OpeningLineDto`
(product, variant?, batch fields {code, mfg, expiry} when batchâ€‘tracked, warehouse, uom, qty, unit_cost).

| Method | Path | Purpose |
|---|---|---|
| GET | `/openings`, `/openings/{no}` | list / detail |
| POST | `/openings` | draft upsert |
| POST | `/openings/{no}/post` | validate + create batches + call posting engine (movement 1) |
| DELETE | `/openings/{no}` | soft delete (draft only) |

Service rules on post: assert no nonâ€‘opening ledger in the year; create `inv_batch` for batchâ€‘tracked
lines; build one `StockPostingCommand` (all legs IN, unit_cost = opening cost); set `status=Posted`,
`posted_at`; emit `fin_voucher` (Dr Inventory / Cr Opening Balance Equity).

### Frontend â€” `inv/forms/inv1101`  (document: header + lines grid)
Header card (warehouse, date, finâ€‘year). Lines grid (`app-common-table` editable): product picker â†’
batch subâ€‘fields if batchâ€‘tracked â†’ uom â†’ qty â†’ unit_cost â†’ line_value (computed); footer totals
(`bottomTotal`). Buttons: Save Draft, **Post** (`canApprove`), disabled after Posted. Banner when the
year is locked.

---

## 7. INV_1102 â€” Stock Adjustment  *(document + approval)*

### Business
Any manual quantity change with a reason: Correction Â· Opening Â· Damage Â· Loss/Theft Â· CountVariance Â·
Revaluation. State machine `Draft â†’ Submitted â†’ Approved(=Posted) â†’ Cancelled(reversed)`. Only
Approved posts to the ledger; `Submittedâ†’Approved` requires `can_approve`; a value threshold can force
twoâ€‘step approval. Cancel of a posted doc writes **reversal** legs (never edits history). Reason
mandatory for loss/damage/theft; large adjustments â†’ dual approval. RBAC: Clerk create/submit Â·
Store Mgr approve Â· Admin delete.

### Middleware â€” `/api/v1/inv/forms/inv1102`
Tables: `inv_stock_adjustment(_dtl)`. DTOs as in inv-db.md Â§5.2 (`Inv1102AdjustmentDto` + line DTO,
`status` serverâ€‘controlled).

| Method | Path | Guard |
|---|---|---|
| GET | `/adjustments`, `/adjustments/page`, `/adjustments/{no}` | can_view |
| POST | `/adjustments` | draft upsert (Draft only editable) â€” can_insert/update |
| POST | `/adjustments/{no}/submit` | Draftâ†’Submitted â€” can_update |
| POST | `/adjustments/{no}/approve` | Submittedâ†’Posted â†’ **posting engine** â€” **can_approve** |
| POST | `/adjustments/{no}/cancel` | Postedâ†’Cancelled â†’ **reversal** â€” can_approve |
| DELETE | `/adjustments/{no}` | Draft only â€” can_delete |
| GET | `/barcode/{code}` | line entry by scan |

Service rules: lines mutable only in Draft; on approve build `StockPostingCommand` (legs from lines,
`qty_base` serverâ€‘computed, OUT cost from `inv_stock.avg_cost`), assert open period, generate
`adjustment_id` via docâ€‘sequence at submit; idempotent post; cancel posts negated legs + reversing
voucher; capture system_qty snapshot for countâ€‘variance type; audit reason.

### Frontend â€” `inv/forms/inv1102`  (document + workflow bar)
Header card (warehouse, date, type, reason). Lines grid (`app-common-table` editable: product picker â†’
batch picker if batchâ€‘tracked â†’ uom â†’ direction â†’ qty â†’ cost auto from avg_cost â†’ line_value).
**Productâ€‘picker modal** with autofocus barcode field (Enter adds line); hidden focused input captures
scanner. **Status bar** buttons enable per status: Save Draft Â· Submit Â· **Approve & Post**
(`canApprove`) Â· Cancel. Status chip; totals via `bottomTotal`. Printable A4 note.

---

## 8. INV_2003 â€” Stock Transfer  *(twoâ€‘phase dispatch/receive)*

### Business
Move stock between warehouses (intraâ€‘ or interâ€‘branch) through the **transit** warehouse so inâ€‘transit
stock is never lost/invisible. State: `Draft â†’ Submitted â†’ Dispatched â†’ Received` (or `ShortReceived`
on variance; `Cancelled` only before dispatch). **Dispatch:** OUT from source, IN to transit
(movement 6/7). **Receive:** OUT transit, IN destination; `qty_received < qty_sent` â†’ ShortReceived,
shortfall stays in transit until written off by adjustment. Interâ€‘branch posts an Interâ€‘Branch Current
A/C voucher; intraâ€‘branch has no GL. RBAC: Clerk create Â· Store Mgr dispatch/receive.

### Middleware â€” `/api/v1/inv/forms/inv2003`
Tables: `inv_stock_transfer(_dtl)`. DTOs: `Inv2003TransferDto` + line (`qty_sent`, `qty_received`).

| Method | Path | Guard |
|---|---|---|
| GET | `/transfers`, `/transfers/page`, `/transfers/{no}` | can_view |
| POST | `/transfers` | draft upsert (fromâ‰ to) â€” can_insert/update |
| POST | `/transfers/{no}/submit` | Draftâ†’Submitted |
| POST | `/transfers/{no}/dispatch` | engine: sourceâ†’transit (6/7) â€” **can_approve** |
| POST | `/transfers/{no}/receive` | engine: transitâ†’dest; set qty_received, Short logic â€” **can_approve** |
| POST | `/transfers/{no}/cancel` | only before dispatch |

Service rules: resolve transit warehouse (branch default type=3); cost carried from source cell;
two posting commands (dispatch, receive); shortâ€‘receipt leaves remainder in transit; docâ€‘sequence id.

### Frontend â€” `inv/forms/inv2003`  (document with phase screens)
Header: from/to warehouse selects (+ branch when interâ€‘branch), date. Lines grid: `qty_sent`; on the
**Receive** view a `qty_received` column appears with **variance highlight**. Status chips
(Draft/Submitted/Dispatched/Received/Short) via `cellType:'badge'`. Buttons switch by phase: Save/Submit
(creator) â†’ Dispatch â†’ Receive (`canApprove`). Inâ€‘transit aging surfaces in the report.

---

## 9. INV_2004 â€” Batch & Expiry  *(monitoring dashboard, mostly read)*

### Business
Visibility + control over batchâ€‘tracked stock and **expiry**. Lists batches with onâ€‘hand, mfg/expiry,
landed cost; flags nearâ€‘expiry (â‰¤ alert window) and expired; supports FEFO picking insight and
nearâ€‘expiry returnâ€‘toâ€‘supplier. Selling/transferring an expired batch needs `can_approve` override and
is flagged. A scheduled job emits `NearExpiryDetected`. RBAC: Store/Sales view; overrides need approve.

### Middleware â€” `/api/v1/inv/forms/inv2004`
Tables (read): `inv_batch` JOIN `inv_stock` (onâ€‘hand per batch). DTO: `Inv2004BatchDto` (batch + on_hand
+ days_to_expiry + status_band). Light writes: edit batch mrp/remarks; deactivate a batch.

| Method | Path | Purpose |
|---|---|---|
| GET | `/batches?productNo&warehouseNo&expiryWindow&status` | filtered list with onâ€‘hand + bands |
| GET | `/expiry-summary` | KPI counts/values: expired / â‰¤30d / â‰¤60d |
| POST | `/batches/{batchNo}` | edit mrp/remarks |
| POST | `/batches/{batchNo}/deactivate` | block batch from picking |

Service: compute `days_to_expiry`, band (expired / amber â‰¤30d / ok); aggregate value = Î£ on_handÃ—cost.

### Frontend â€” `inv/forms/inv2004`  (filter + dashboard)
KPI tiles (expired count/value, nearâ€‘expiry). Filter bar (product, warehouse, expiry window). Colorâ€‘coded
`app-common-table` rows: red=expired, amber=â‰¤30d (`badgeConfig`). Export (CSV stream). Drill to batch
stock by cell. Readâ€‘only for most; edit/deactivate gated by `canUpdate`.

---

## 10. Crossâ€‘cutting: how a document form is wired (checklist)

For each **document** form (INV_1101/1102/2003) the build is:
1. **Entities + repositories** for header/detail (extend `BaseEntity`; `_dtl` cascade).
2. **DTOs** (header + `@Valid @NotEmpty` line list), serverâ€‘controlled `status`.
3. **`InvXXXXService`**: draft upsert (reconcile lines), action methods (`submit/approve/cancel` or
   `dispatch/receive`) that build a `StockPostingCommand` and call `InvStockPostingService`; docâ€‘sequence;
   period guard; idempotency; manual toDto.
4. **`InvXXXXController`** at `/api/v1/inv/forms/invXXXX` with the action endpoints; `@Valid` bodies; `ApiResponse<T>`.
5. **Frontend** `data.service.ts` (CRUD + actions), `model.service.ts` (signals, line FormArray, totals,
   workflow state), `component.ts/html` (header card + editable lines grid + status bar), route in `inv.routes.ts`.
6. **DB seed**: `sys_menu` row (`form_id=INV_xxxx`, `route_path=inv/forms/invXXXX`) + `sys_doc_sequence`
   row per branch/finâ€‘year + `sys_enroll_menu` so it's licensed/visible.
7. **Tests**: stateâ€‘machine transitions, posting atomicity/rollback, negativeâ€‘stock both ways, periodâ€‘closed reject (inv-db.md Â§9).

Master forms (B, INV_2001/2/5) skip steps 3â€‘action/6â€‘sequence â€” they're plain CRUD like SYS1007.

---

## 11. Suggested implementation waves (mirrors the SYS build cadence)

- **Wave I (lookups + topology):** Category, Brand, UOM, Attribute, INV_2001 Warehouse, INV_2002 Rack â€” all SYS1007â€‘style CRUD. Verify compile + tsc.
- **Wave II (catalog):** INV_1001 Product Master (+ variant generator), INV_1002 Barcode, INV_2005 Reorder.
- **Wave III (engine):** `InvStockPostingService` + `inv_stock/ledger/layer` entities + `InvDocSequenceService` + period guard (no UI; unitâ€‘tested hard).
- **Wave IV (documents):** INV_1101 Opening, INV_1102 Adjustment, INV_2003 Transfer.
- **Wave V (monitoring):** INV_2004 Batch & Expiry, Physical Count, stock/ledger read endpoints + reports.

Each wave verified with `./mvnw -o -q compile` and `tsc --noEmit -p apps/web-client/tsconfig.app.json`,
exactly as the SYS waves were.


## Common Structure Notes

---
name: common-lookups-and-structure
description: "Common lookups module, FIN dto structure fix, and aidly-business-doc reorg"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9cca2c5d-341a-4a5a-8fbf-926297681c99
---

## COMMON LOOKUPS MODULE (built â€” compile + AidlyApplicationTests + nx build green)
New package `com.infoaidtech.aidly.common` â€” the ONE place for cross-form, read-only reference lists (NO transactional endpoints ever).
- **`GET /api/v1/common/lookups/{resource}`** â†’ `ApiResponse<List<...>>`. Two DTO shapes (had to MATCH the existing frontend `core/services/common-lookup.service.ts` which is ALREADY used by sys1002/1007/1008/1104):
  - **Rich DTOs** (frontend contract): `employees`â†’EmployeeLookupDto{employee_no,employee_id,full_name,full_name_with_id,department_name,designation_name,user_no}; `branches`â†’BranchLookupDto{branch_no,branch_id,branch_name,branch_type,is_main_branch}; `departments`â†’DepartmentLookupDto; `designations`â†’DesignationLookupDto (optional `?departmentNo=` filter).
  - **Uniform `LookupDto {no,code,name}`**: currencies, warehouses, products, uoms, accounts(postable), suppliers, customers.
  - **CRITICAL**: sys1104 relies on `user_no` from `employees` (employeeâ†’login map built from UserRepository.findByCompanyNoAndIsDeleted). Don't break these shapes.
- Frontend `CommonLookupService` (already existed with getEmployees/Branches/Departments/Designations) â€” I ADDED generic helpers getCurrencies/Warehouses/Products/Uoms/Accounts/Suppliers/Customers (return `LookupOption{no,code,name}`) + `LookupOption` interface. New forms should inject this instead of cross-form reads. (PUR_1101 uses it â€” reference impl.)
- `CommonLookupController` + `CommonLookupService` (injects the 11 source repos) + `common/dto/LookupDto`.
- **Tenant safety**: HRM masters (employee/department/designation) + currencies are **branch-scoped** (extend AuditEntity, declare branch_no, NO auto @Filter) â†’ scoped via `findByBranchNoAndIsDeleted(branch,0)`. branches by company. INV/FIN/PUR/SAL masters company-scoped. Never use `findAllByIsDeleted` on AuditEntity masters (cross-tenant leak).
- **RBAC**: `/common/lookups/**` is NOT a menu route â†’ `RbacAuthorizationInterceptor` resolves form id from the `X-Form-Id` header (the calling form) and authorizes by that form's VIEW. Works for any authenticated user viewing a form; no whitelist needed. The FE HTTP interceptor already sends X-Form-Id.
- **Going forward**: new forms should use `/common/lookups/*` for shared dropdowns instead of cross-form reads or per-form list endpoints. (Existing HRM generic controllers HrmEmployee/Department/DesignationController still exist â€” migrate forms to common lookups when touched; didn't remove them to avoid breaking current callers.)
- TODO (perf): add `@Cacheable` once a cache manager is enabled; lookups are ideal reference-data cache candidates.

## FIN DTO STRUCTURE FIX (the non-form-id files the user flagged)
- `GlPostingPayload` moved `fin/dto/` â†’ **`fin/contract/`** (it's the cross-module GL event contract, not a form DTO). Updated import in `FinPostingService`.
- `FinStatementRowDto` moved `fin/dto/` â†’ **`fin/dto/shared/`** (shared by FIN_1304 P&L + FIN_1305 Balance Sheet). Updated imports in Fin1304PnlDto, Fin1305BalanceSheetDto, FinReportService (its `fin.dto.*` wildcard doesn't cover the subpackage). Rule: every `*/dto/*.java` is form-id-prefixed; genuinely shared/contract types go in `module/contract/` or `module/dto/shared/`.

## aidly-business-doc REORG (docs consolidated, code untouched)
- `aidly-business-doc/backend/` â† `*-db.md` (fin/hrm/inv/pur/sal/sys), `db-script.sql`, `sys-db.sql`, `db-updated.md`, `fix.md`, **and `db-migration/`** (moved from `sme-software-backend/db-migration` â€” **all SQL migration + menu-seed scripts now live at `aidly-business-doc/backend/db-migration/`**; generate future SQL there).
- `aidly-business-doc/frontend/` â† `*-forms-design.md` (inv/pur/sal), `FRONTEND_ARCHITECTURE.md`, `form.md`, `fix.md` (moved out of `sme-software-frontend`).
- `aidly-business-doc/memory/` â† copy of all progress-memory `.md` (the `.claude/.../memory/` store remains the live memory; this is the in-repo mirror).
- Root keeps `business.md`, `pur-sal-build-plan.md`.
- Frontend CLAUDE.md updated: architecture/business docs now under `../aidly-business-doc/{frontend,backend}/`.

