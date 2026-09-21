# INV Form Business

<!-- Consolidated from the previous aidly-business-doc/backend, frontend, memory, and planning files. -->
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

