# POS Build Plan — INV · PUR · SAL (retail front-line, on top of the closed FIN loop)

> **Status:** plan only — nothing in this document is built yet unless §1 says it is.
> **Written:** 2026-08-13. **Audited against:** live code in `sme-software-backend`, `sme-dotnet-backend`,
> `sme-software-frontend`, and `db_schema_dump.tsv` (dumped 2026-08-10).
> **Mirrors:** the FIN build plan (`business-logic/fin-business.md` §"Finance Build Memory") — same shape:
> verified current state → delta → dependency-safe waves → definition of done.
>
> **Source blueprints (unchanged, still authoritative for column-level detail):**
> `business-logic/inv-business.md` · `business-logic/pur-business.md` · `business-logic/sal-business.md`
> · the cross-module seam/wave plan appended to `pur-business.md` §0–§5.
>
> **The one-line thesis:** POS is **not a new module**. It is the missing 30% of SAL (`SAL_1001/1002/1003/1104`)
> plus one missing INV capability (reservations) plus the tender/session data model. INV and PUR are ~90% built
> and feed it. This document is the plan to finish all three properly, with POS as the destination.

---

# 0. Scope and how to read this

| Section | What it gives you |
|---|---|
| §1 | **Verified inventory** of what exists today in INV / PUR / SAL / the FIN loop — built, partially built, absent |
| §2 | The gap list, split into *POS-blocking* vs *module-completeness* vs *deferrable* |
| §3 | **Database delta** — the exact new tables and column additions, nothing more |
| §4 | Shared services delta (reservation, pricing, session) — built once, used by every POS form |
| §5 | **Build waves POS-0 … POS-7** — the actual work order, dependency-safe |
| §6 | `fin_gl_map` seed rows the new events need (else events park as Failed) |
| §7 | Menu seed · RBAC · doc sequences · approval registry |
| §8 | The POS screen itself (frontend spec, keyboard, print) |
| §9 | Offline strategy — phased, not all-or-nothing |
| §10 | Decisions to confirm before Wave POS-2 (cheap now, expensive later) |
| §11 | Definition of done + the exact verification commands |
| App. A–C | Enums · form registry · **dual-backend (Java/.NET) parity ledger** |

---

# 1. VERIFIED CURRENT STATE (as of 2026-08-13)

This section is an audit, not a plan. Everything below was read out of the repo.

## 1.1 The good news: the money loop is already closed

The purchase → stock → sale → AR → GL chain **exists and runs**. FIN is feature-complete (17 forms) and
`FinPostingService` drains `sys_event_outbox` every 30s into balanced vouchers. The following emitters are
**already wired in the Java backend**:

| Emitter | Events emitted |
|---|---|
| `Inv1101Service` (opening stock) | `OpeningStockPosted` |
| `Inv1102Service` (adjustment) | `StockAdjustmentPosted` · `StockAdjustmentReversed` |
| `Pur1102Service` (purchase invoice) | `PurchaseInvoicePosted` · `PurchaseInvoiceReversed` |
| `Pur1103Service` (purchase return) | `PurchaseReturnPosted` · `PurchaseReturnReversed` |
| `Pur1104Service` (supplier payment) | `SupplierPaymentPosted` · `SupplierPaymentReversed` |
| `Pur1105Service` (GRN) | `GoodsReceiptPosted` · `GoodsReceiptReversed` |
| `Pur1106Service` (landed cost) | *(allocation, no direct GL event)* |
| `Sal1001Service` (sales invoice) | `SalesInvoicePosted` · `SalesInvoiceReversed` |
| `Sal1102Service` (customer receipt) | `CustomerReceiptPosted` · `CustomerReceiptReversed` |
| `Sal1103Service` (sales return) | `SalesReturnPosted` · `SalesReturnReversed` |
| `Hrm1202Service` / `Hrm1207Service` | `PayrollPosted` · `FinalSettlementPosted` |

**Implication for POS:** we are *not* building a posting pipeline. POS sales already post to the GL via
`SalesInvoicePosted`. What POS adds on top is the **session/tender/drawer layer** and the **cashier screen**.

## 1.2 INV — Inventory (13 of 13 planned forms built)

**Built (Java backend + Angular frontend, both):**

| Form | Route (`/inv/...`) | Status |
|---|---|---|
| INV_1001 Product Master | `form/product-master-setup` | built |
| INV_1002 Barcode Generator | `form/barcode-generator` | built |
| INV_1003 Category | `form/category-setup` | built |
| INV_1004 Brand | `form/brand-setup` | built |
| INV_1005 UOM | `form/unit-of-measure-setup` | built |
| INV_1006 Product Attribute | `form/product-attribute-setup` | built |
| INV_1101 Opening Stock | `form/opening-stock-entry` | built + GL emitter |
| INV_1102 Stock Adjustment | `form/stock-adjustment` | built + GL emitter + approval |
| INV_2001 Warehouse | `form/warehouse-setup` | built |
| INV_2002 Rack/Shelf | `form/rack-shelf-setup` | built |
| INV_2003 Stock Transfer | `form/stock-transfer` | built |
| INV_2004 Batch & Expiry | `form/batch-expiry-management` | built |
| INV_2005 Reorder Level | `form/reorder-level-setup` | built |

**Engine built:** `InvStockPostingService` (the single write path), `InvDocSequenceService`.
**Entities built (20):** product/variant/attribute/barcode/uom(+conversion), category, brand, warehouse, rack,
reorder, batch, stock, stock_ledger, valuation_layer, adjustment(+dtl), transfer(+dtl).

**INV tables specced but NOT built:**

| Table | Blueprint § | Needed for |
|---|---|---|
| **`inv_reservation`** | inv-business §2.22 | **POS hold/draft — blocking** |
| `inv_product_price` | inv-business §2.20 | branch/tier pricing + promotions — POS pricing step 1 |
| `inv_serial` | inv-business §2.13 | serial-tracked sale/return (electronics) |
| `inv_physical_count` (+`_dtl`) | inv-business §2.19 | stocktake — module completeness |
| `inv_bundle_dtl` | inv-business §2.21 | combo/kit products at POS |

## 1.3 PUR — Purchase (8 of 8 planned forms built)

**Built (Java backend + Angular frontend, both):** PUR_1001 Supplier · PUR_1002 Supplier Price List ·
PUR_1101 Purchase Order · PUR_1102 Purchase Invoice · PUR_1103 Purchase Return · PUR_1104 Supplier Payment ·
PUR_1105 GRN · PUR_1106 Landed Cost. Plus `PurApLedgerService` and `PurApprovalListener`.

**Tables built (14):** supplier, supplier_product, supplier_ledger, order(+dtl), receipt(+dtl), invoice(+dtl),
return(+dtl), payment(+alloc), landed_cost(+alloc).

**PUR is functionally complete for the POS story.** It is the stock-inflow side and needs no new work to make
POS run. Remaining PUR items are reporting/polish only (§2.3).

## 1.4 SAL — Sales (4 of 7 planned forms built; the POS three are the missing ones)

| Form | Backend | Frontend | Verdict |
|---|---|---|---|
| SAL_1101 Customer Management | ✅ `Sal1101Service` | ✅ `form/customer-management` | **built** |
| SAL_1102 Customer Due Collection | ✅ `Sal1102Service` + GL | ✅ `form/customer-receipt` | **built** |
| SAL_1103 Sales Return | ✅ `Sal1103Service` + GL | ✅ `form/sales-return` | **built** |
| SAL_1001 Sales Invoice | ⚠️ `Sal1001Service` | ⚠️ `form/sales-invoice` | **built as a credit invoice, NOT as POS** |
| **SAL_1002 Draft/Hold** | ❌ | ❌ | **absent** |
| **SAL_1003 POS Closing** | ❌ | ❌ | **absent** |
| **SAL_1104 Promotions** | ❌ | ❌ | **absent** |

**What `SAL_1001` is today (read from `Sal1001Service.java` + `Sal1001InvoiceDto.java`):**
a conventional document form — header + lines, save/submit/reject/approve/post/cancel/delete, stock relief
via `InvStockPostingService` (`REF_SAL_INV = 4`), AR ledger write, `SalesInvoicePosted` emit, COGS computed.
It carries `sale_type` (1=POS, 2=Credit) and a **single scalar `paid_amount`**.

**What that means concretely:** today the system can record *that* a sale was paid, but not *how*. There is no
split tender (cash + bKash), no drawer, no session, no cashier attribution, no barcode-add endpoint, no
hold/resume, no idempotency key. `sale_type=1` is a label with no machinery behind it.

**The frontend POS page is an empty stub:** `features/sal/pages/pos/pos.component.html` is 17 bytes, the
`.scss` is 0 bytes, and `pos` is **not referenced in `sales.routes.ts`** — it is unreachable dead scaffold.

**SAL tables specced but NOT built** (confirmed absent from `db_schema_dump.tsv`):

| Table | Blueprint § | Blocking |
|---|---|---|
| **`sal_pos_terminal`** | sal-business §2.3 | **yes** |
| **`sal_pos_session`** | sal-business §2.4 | **yes** |
| **`sal_invoice_payment`** | sal-business §2.7 | **yes** — split tender lives here |
| `sal_customer_group` | sal-business §2.2 | no — pricing tiers |
| `sal_promotion` (+`_dtl`) | sal-business §2.11 | no — SAL_1104 |
| `sal_loyalty_txn` | sal-business §2.12 | no — optional |

**`sal_invoice` column gaps** (spec sal-business §2.5 vs live schema):

| Missing column | Purpose | Blocking |
|---|---|---|
| **`client_uuid`** | offline/double-click idempotency; **unique incl. soft-deleted** | **yes** |
| **`pos_session_no`** | binds the sale to a drawer period | **yes** |
| **`terminal_no`** | which register rang it | **yes** |
| `invoice_time` | POS needs time-of-day, not just date | yes (Z-report) |
| `bill_discount_type` / `_value` / `_amount` | header-level discount, distributed back to lines | yes |
| `promotion_discount` | promo total | no |
| `salesperson_employee_no` | commission / attribution | no |

**`sal_invoice_dtl` column gaps:** `mrp`, `line_discount_type`, `line_discount_value`, `is_tax_inclusive`,
`promotion_no`, `promotion_discount`, `is_free_item`. (Live table already has `line_discount_pct`,
`line_discount_amount`, `unit_cost`, `line_cost`, `returned_qty`, `vat_tax_no` — the costing/tax core is fine.)

## 1.5 Dual backend — the parity problem, stated plainly

There are **two live backends**. Both must be considered before a line of POS code is written.

| Module | Java (`sme-software-backend`) | .NET (`sme-dotnet-backend`) |
|---|---|---|
| SYS | complete | complete (23 controllers) |
| HRM | complete | complete |
| FIN | complete (17 forms) | `FinControllers.cs` present |
| PUR | complete (8 forms) | **complete — all 8 routed** (`api/v1/pur/forms/pur1001…1106`) |
| SAL | 4 forms | **same 4 forms routed** (`sal1001/1101/1102/1103`) — identical gap |
| INV | complete (13 forms) | ⚠️ **services exist for all 13, but only 4 are routed** |

The .NET INV hole is specific and worth naming: `Inv1002Service`, `Inv1006Service`, `Inv1101Service`,
`Inv1102Service`, `Inv2001–2005Service` **all exist** in `src/Modules/Inv/…/Services`, and are DI-registered in
`Program.cs`, but **no controller exposes them**. Only `api/v1/inv/forms/inv1001|inv1003|inv1004|inv1005` are
reachable. So on .NET today you can define a product but cannot receive opening stock, adjust stock, create a
warehouse, or transfer — i.e. **.NET cannot currently run a POS sale end-to-end** because there is no way to get
stock into it through the API.

**This is decision D0 (§10) and it gates everything.**

---

# 2. THE GAP, IN THREE TIERS

## 2.1 Tier 1 — POS-blocking (nothing sells without these)

1. `sal_pos_terminal` + `sal_pos_session` tables + service — no drawer, no Z-report, no cash accountability.
2. `sal_invoice_payment` table — split tender. **This is the single biggest functional gap.**
3. `sal_invoice.client_uuid` + unique index — idempotency. Without it, a double-click or an offline replay
   creates a duplicate sale that relieves stock twice.
4. `sal_invoice.pos_session_no` / `terminal_no` / `invoice_time`.
5. A **server-side pricing endpoint** (`POST /sales/quote-price`) — the cart must be priced by the server, per
   sal-business §3.1. Today `Sal1001Service.recomputeLines` recomputes totals but there is no price *resolution*
   (no tier, no promo, no min-price guard).
6. A **barcode-add endpoint** for the cashier flow — already available as
   `GET /api/v1/inv/forms/inv1001/resolve-barcode?barcode=` (returns product/variant/uom/**pack_qty**).
7. The **POS screen itself** — currently a 17-byte stub.

> **Hold is a client-side drawer, not a document** *(decided 2026-08-13)*. Parked carts live in the POS
> screen's own right-hand drawer, persisted to `localStorage`. There is no `sal_invoice` row, no
> `SAL_1002` form, and **no stock reservation** while a cart is held.
>
> The consequence, stated plainly: two terminals can both park a cart containing the last unit, and the
> second one to tender loses at confirm time when the posting engine's negative-stock guard fires. For a
> single-till SME that is the right trade — it removes `inv_reservation`, the expiry sweeper, and a whole
> release/consume lifecycle from v1. Revisit when a customer runs more than one till against one warehouse.

## 2.2 Tier 2 — module completeness (the "properly" in the ask)

- **INV:** `inv_product_price` (branch/tier/qty-break pricing), `inv_physical_count` (stocktake — a retailer
  *will* ask for this in month one), `inv_serial` (if electronics), `inv_bundle_dtl` (combos).
- **SAL:** SAL_1002 hold tray, SAL_1003 closing + Z-report, SAL_1104 promotions, `sal_customer_group`.
- **PUR:** nothing structural. Reporting only (§2.3).
- **Cross-cutting:** FIN_1202/1203 AR/AP sub-ledger reader forms — deferred in the FIN build, and the
  sub-ledger tables (`sal_customer_ledger`, `pur_supplier_ledger`) now exist and are being written, so these
  are now cheap.

## 2.3 Tier 3 — deferrable (do not let these block POS)

Loyalty (`sal_loyalty_txn`), full offline/IndexedDB mode (§9 phases it), materialized views
(`mv_pur_supplier_aging`, `mv_pur_spend_by_category`, sales MVs), fraud/exception dashboards, EMI/installments,
multi-currency POS tendering, gift cards / store credit.

---

# 3. DATABASE DELTA

Everything here goes into **one migration script**, following the house pattern
(`db-migration/YYYY-MM-DD_HHMM_*.sql`, idempotent, `IF NOT EXISTS`, `BEGIN`/`COMMIT`, pretty-printed,
audit-block style). Suggested name: `2026-08_pos_schema.sql`.

> **Generation method (house rule):** write the entities first, run `DdlGenTest`, extract the new
> `CREATE TABLE`s — same as the `inv_*` / `hrm_*` / `fin_*` scripts. Do **not** hand-write the DDL and hope
> the entities match; that is exactly how the `*_code`-vs-`*_id` column mismatches happened in the .NET port.

## 3.1 New tables (5 blocking + 4 completeness)

| # | Table | Blueprint § | Tier | Notes |
|---|---|---|---|---|
| 1 | `inv_reservation` | inv §2.22 | 1 | `status` 1=Active 2=Released 3=Consumed 4=Expired; `ref_doc_type=4` (SalInvoice hold); partial index on `status=1` |
| 2 | `sal_pos_terminal` | sal §2.3 | 1 | `uq_sal_term_id(branch_no, terminal_id) WHERE is_deleted=0`; `warehouse_no` = stock source; `cash_gl_account_no` → drawer account |
| 3 | `sal_pos_session` | sal §2.4 | 1 | **`uq_sal_sess_open ON (terminal_no) WHERE status=1 AND is_deleted=0`** — one open session per terminal, enforced by the DB, not by code |
| 4 | `sal_invoice_payment` | sal §2.7 | 1 | `payment_method` 1–9; `uq_sal_invpay_line(invoice_no, line_no)` |
| 5 | `sal_customer_group` | sal §2.2 | 2 | pricing tier + default terms |
| 6 | `inv_product_price` | inv §2.20 | 2 | branch/tier/qty-break/date-windowed price overrides |
| 7 | `sal_promotion` (+ `_dtl`) | sal §2.11 | 2 | SAL_1104 |
| 8 | `inv_physical_count` (+ `_dtl`) | inv §2.19 | 2 | stocktake |
| 9 | `inv_serial` | inv §2.13 | 2 | only if D5 says serial tracking is in v1 |

## 3.2 Column additions

```sql
-- sal_invoice (POS-blocking)
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS client_uuid    UUID;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS invoice_time   TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS terminal_no    BIGINT;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS pos_session_no BIGINT;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS bill_discount_type   SMALLINT NOT NULL DEFAULT 1;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS bill_discount_value  NUMERIC(20,4) NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS bill_discount_amount NUMERIC(20,4) NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS promotion_discount   NUMERIC(20,4) NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice ADD COLUMN IF NOT EXISTS salesperson_employee_no BIGINT;

-- backfill then enforce: existing rows need a uuid before the unique index can land
UPDATE sal_invoice SET client_uuid = gen_random_uuid() WHERE client_uuid IS NULL;
ALTER TABLE sal_invoice ALTER COLUMN client_uuid SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_sal_inv_uuid ON sal_invoice(client_uuid);   -- incl. soft-deleted, by design
CREATE INDEX IF NOT EXISTS idx_sal_inv_session ON sal_invoice(pos_session_no);

-- sal_invoice_dtl
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS mrp                  NUMERIC(20,4);
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS line_discount_type   SMALLINT NOT NULL DEFAULT 1;
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS line_discount_value  NUMERIC(20,4) NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS is_tax_inclusive     SMALLINT NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS promotion_no         BIGINT;
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS promotion_discount   NUMERIC(20,4) NOT NULL DEFAULT 0;
ALTER TABLE sal_invoice_dtl ADD COLUMN IF NOT EXISTS is_free_item         SMALLINT NOT NULL DEFAULT 0;

-- inv_stock: reservation accounting (verify first — may already exist)
-- qty_reserved must be non-null and >= 0; available = qty_on_hand - qty_reserved
```

> **Verify before writing:** `db_schema_dump.tsv` is a 2026-08-10 snapshot and two later migrations exist
> (`2026-08-10_0925_gl_voucher_no_columns.sql`, `2026-08-11_1600_fin_gl_map_sal_reversals_company2.sql`).
> Re-dump the live schema before finalizing the ALTERs so nothing is added twice.

## 3.3 The one invariant that must hold from day one

```
available_qty = inv_stock.qty_on_hand − inv_stock.qty_reserved
```

Every reservation write must move `qty_reserved` **inside the same transaction and under the same row lock**
that `InvStockPostingService` already takes on the `inv_stock` cell. If reservations are written outside that
lock, two terminals will oversell the last unit — the exact edge case inv-business §1.5 calls out.

---

# 4. SHARED SERVICES DELTA (build once, no UI, unit-tested hard)

Mirrors the FIN discipline: services land before the forms that call them.

## 4.1 `InvReservationService` (new, in `aidly.inv.service`)

```
reserve(ReservationCommand) → List<reservationNo>
release(refDocType, refDocNo)                  // hold voided / expired
consume(refDocType, refDocNo)                  // sale confirmed; reserved → consumed
releaseExpired()                               // @Scheduled sweep
```
- Same lock discipline as `InvStockPostingService`: `SELECT … FOR UPDATE` the `inv_stock` cell, then bump
  `qty_reserved`. **Reuse the existing pessimistic finder — do not add a second one.**
- Idempotent on `(ref_doc_type, ref_doc_no)`, exactly like the posting engine.
- Negative-availability guard honors the branch's allow/deny-negative-stock policy.

## 4.2 `SalPricingService` (new)

Implements sal-business §3.1 **in order**, server-side, authoritative:

1. base price ← `inv_product_price` (branch, tier, qty-break, date) → variant `sale_price` → product `sale_price`
2. line discount (manual, capped by role; below-cap needs `can_approve`)
3. promotions (`sal_promotion` by priority; stackable vs highest-value; BuyXGetY emits `is_free_item=1` lines)
4. bill discount, distributed proportionally back to lines (so tax and margin stay correct)
5. tax per line `vat_tax_no`, inclusive → extract / exclusive → add
6. round-off to currency decimals

Exposed as `POST /sales/quote-price` (cart in → priced cart out) so the screen never computes money.
`Sal1001Service.recomputeLines` gets refactored to call this instead of carrying its own arithmetic.

## 4.3 `SalPosService` (new)

```
openSession(terminalNo, openingFloat) → session      // fails if an open session exists (DB unique index)
currentSession(terminalNo)
sessionSummary(sessionNo)                            // expected by tender, from sal_invoice_payment
closeSession(sessionNo, countedCash, remarks)        // variance + approval gate + GL emit
```
- `closeSession` computes `expected_*` by grouping `sal_invoice_payment` for the session **minus returns**, sets
  `cash_variance = counted − (opening_float + expected_cash)`, gates beyond-tolerance variance behind
  `can_approve`, then emits `PosSessionClosePosted`.
- Session running totals (`total_sales`, `invoice_count`) updated **under row lock** in the sale's own transaction.

## 4.4 `Sal1001Service` — the refactor (not a rewrite)

The existing service already does stock relief, COGS, AR ledger, GL emit, approval, cancel/reverse. It gains:

| Addition | Detail |
|---|---|
| tender lines | persist `sal_invoice_payment[]`; `paid_amount = Σ amount`; `change_amount` from cash tendered |
| session binding | require an open session when `sale_type=1` (or auto-open per D3) |
| idempotency | on `client_uuid` collision, **return the existing posted document** — do not error, do not re-post |
| hold/resume/void | `status` 2=Hold; reserve on hold, release on void, consume on confirm |
| pricing | delegate to `SalPricingService` |
| barcode add | `GET /sales/barcode/{code}` → `(product_no, variant_no, uom_no, pack_qty)`; scanned qty × `pack_qty` |
| credit-limit gate | `current_due + new_due > credit_limit` → block unless `can_approve` (per D4) |

---

# 5. BUILD WAVES

Dependency-safe. **A wave is not done until its verification block (§11) is green.** Same cadence as FIN:
backend → DB migration → frontend → menu seed → enroll (company 2) → verify.

## Wave POS-0 — Decisions + foundations *(half a day)*

- Confirm **D0–D6** (§10). D0 (which backend) gates every later wave.
- Re-dump the live schema; reconcile against §3.2 so no ALTER is written twice.
- Register doc types and sequences: `SAL_POS_SESSION` in the approval registry; `sys_doc_sequence` rows for
  `SAL_SESSION`, and per-terminal `receipt_prefix`.
- Write the `fin_gl_map` seed skeleton for the new events (§6) — **before** anything can post.

## Wave POS-1 — POS data model *(DB + entities, no behaviour yet)*

| Item | Detail |
|---|---|
| `sal_pos_terminal` + `sal_pos_session` + `sal_invoice_payment` tables | §3.1 |
| `sal_invoice` / `sal_invoice_dtl` ALTERs + `client_uuid` backfill + unique index | §3.2 |
| .NET entities + `SalDbContext` registration | mirrors the real columns exactly |
| **SAL_1005 Terminal Setup** — backend + Angular form at `/sal/form/pos-terminal` | a session cannot open without a terminal |
| Update `database-schemas/sal-db.md` + Applied Migration Log | mandatory per backend CLAUDE.md |

`inv_reservation` is **not** in this wave — holds are client-side (§2.1), so nothing reserves stock.

## Wave POS-2 — POS services *(no UI)*

| Item | Detail |
|---|---|
| `SalPosService` | §4.3 — open / current / summary / close |
| `Sal1001Service` refactor | §4.4 — tender lines, session binding, `client_uuid` idempotency |
| `SalPricingService` | §4.2 — steps 1, 2, 4, 5, 6 (promotions land in POS-5) |
| `POST /sales/quote-price` | prices a cart server-side; the screen never computes money |

**Idempotency test is mandatory here:** POST the same `client_uuid` twice concurrently → one invoice, one stock
movement, one GL event, second call returns the first document.

## Wave POS-3 — The POS screen (SAL_1001) *(the big one)*

Full spec in §8. Route it at `sal/pos` (replacing the dead stub) as a **full-screen, no-sidebar layout**.
Ships with: barcode add, cart, quote-price round-trip, tender modal with split payment, change calc,
**the hold drawer**, receipt print, keyboard map, double-confirm block.

**Do not** build this as a standard `app-page-form-layout` form. It is a signal-store screen; per
sal-business §4.2 a reactive `FormGroup` per line is too slow for scan-heavy use.

### The hold drawer (replaces SAL_1002)

A right-hand slide-over inside the POS screen, not a routed form:

- **Storage:** `localStorage`, keyed per company + branch + terminal so two tills on one machine never
  see each other's carts. One entry = `{ id, label, customer_no, lines[], created_at }`.
- **Actions:** Hold (park current cart, clear the till), Resume (load into the cart, drop from the drawer),
  Discard. A badge on the drawer toggle shows the parked count.
- **Re-price on resume** — a cart parked before a price change must not tender at the stale price; the
  resume path calls `quote-price` exactly like a scan does.
- **No stock is reserved.** Availability is only checked at confirm, by the posting engine.
- **Bounded:** cap the drawer (e.g. 20 carts) and stamp `created_at` so stale carts can be pruned —
  `localStorage` has no expiry of its own.

## Wave POS-4 — Session close *(SAL_1003)* — BUILT 2026-08-13

- **SAL_1003** at `sal/form/pos-closing` — session picker, Z-report (takings by tender, drawer
  reconciliation, sales/returns/credit), denomination-grid cash count with a manual-total fallback,
  variance highlight, supervisor sign-off, print layout.
- `PosSessionClosePosted` → GL, mapped by `2026-08-13_1100_fin_gl_map_pos_session_company2.sql`.

**The close gate.** A drawer out by more than 0.01 refuses to close without *both* an explanation and
an explicit supervisor approval, and the approver is stamped into `variance_approved_by`. Enforced
server-side in `SalPosService.CloseAsync` — the UI mirrors it so the button is honest, but the rule
does not live in the browser. A drawer that does not reconcile is the most useful fraud signal a till
produces; letting it close on a shrug throws that away.

**Tolerance is 0.01** — rounding slack, not a policy allowance. Make it per-company config if a
business wants a wider band; do not widen the constant.

## Wave POS-5 — Promotions + customer groups *(SAL_1104)*

`sal_promotion` (+`_dtl`) + `sal_customer_group`, rule builder UI, `POST /promotions/evaluate`.
`SalPricingService` step 3 goes live here. Percent/amount/BuyXGetY, priority, stackability, usage limits,
time/weekday windows, `max_discount_amount`.

## Wave POS-6 — INV/PUR/SAL module completeness ✅ *(built 2026-08-14)*

| Module | Work | State |
|---|---|---|
| INV | `inv_physical_count` (+`_dtl`) + stocktake form (INV_2006); `inv_serial` if D5=yes; `inv_bundle_dtl` for combos | ✅ INV_2006 built. Serial tracking **not built** — D5 still open. Bundles **not built**. |
| SAL | sales reports: daily sales summary, sales by product/category/cashier, margin report, hourly heat-map for staffing | ✅ SAL_1301 — daily / by-product / by-cashier / by-hour |
| PUR | supplier aging + spend-by-category + supplier performance (MVs per pur-business §6) | ✅ PUR_1301 — aging + spend. Built as **live queries, not materialised views**: an SME's bill count does not justify a refresh cycle, and a stale aging report is worse than a slow one. Supplier performance **not built**. |
| FIN | **FIN_1202 AP / FIN_1203 AR** reader forms over the now-populated sub-ledgers (deferred in the FIN build; cheap now) | ✅ Built as **reconciliations**, not plain readers — see below |

**FIN_1202/1203 turned out to be worth more than "readers."** A reader over a sub-ledger
duplicates FIN_1307, which already ages the same parties from the GL. What neither side had
was the comparison: the sub-ledger (PUR/SAL) and the control account (FIN) are written from
the same transaction, so they agree by construction — and when they stop agreeing, nothing
was reporting it. These forms put the two side by side per party, worst mismatch first.

Three states the form distinguishes, because they need different responses:

| State | Meaning |
|---|---|
| Reconciled | The two sets of books agree. |
| Out by *N* across *k* parties | Something wrote one side and not the other. Drill into the party statement. |
| No control account | `fin_account.control_type` is unset for this side, so the GL has nothing to say. A setup gap, **not** a break — reporting the whole sub-ledger as a difference would be alarming and wrong. |

The form also surfaces **unattributed GL** — control-account movement naming no party, which is
almost always a manual journal posted straight to the control account. It cannot be aged or
chased by any party statement, and it is the usual reason a control account drifts.

**Module boundary:** FIN does not touch `pur_supplier_ledger` or `sal_customer_ledger`.
PUR and SAL each expose a read-only contract (`IPurApLedgerReader`, `ISalArLedgerReader`)
implemented in their own Infrastructure; FIN.Application references only `.Contracts`.
All 84 boundary tests pass with the new references.

**One screen, two forms.** AR and AP are the same reconciliation with the sign flipped, so
`SubLedgerComponent` serves both routes and the backend takes a `partyType`. The routes,
menu rows and form ids stay separate — `X-Form-Id` resolves from the URL, so each keeps its
own permission, and AP and AR are often different people's jobs.

## Wave POS-7 — Offline POS *(only if D6 = yes)*

See §9. Phase it; do not attempt full offline in the first release.

---

# 6. `fin_gl_map` SEED ROWS (else events park as Failed)

`FinPostingService` parks any event whose leg has no map row — it never guesses. Existing SAL/PUR events are
already seeded for company 2 (`2026-08-11_1100_fin_gl_map_seed_company2.sql`,
`2026-08-11_1600_fin_gl_map_sal_reversals_company2.sql`). The **new** POS events need rows added:

| Event | Legs (legKey) | Dr/Cr |
|---|---|---|
| `PosSessionClosePosted` | `CASH_IN_TRANSIT` | Dr (deposit) |
| | `DRAWER_CASH` | Cr |
| | `CASH_OVER_SHORT` | Dr/Cr (variance → expense/income) |
| `SalesInvoicePosted` *(extend existing)* | per-tender legs: `CASH`, `CARD_CLEARING`, `MOBILE_CLEARING`, `BANK` | Dr, by `payment_method` via `subKey` |

**Use `subKey` for the tender split** — one `SalesInvoicePosted` map row per `payment_method`, rather than a new
event type. That keeps the emitter's leg-building loop trivial and matches how `fin_gl_map(company, eventType,
legKey, subKey)` already resolves.

**VAT routing is decision D1** — `fin_gl_map` `OUTPUT_VAT` leg vs `sys_vat_tax.gl_account_no` per tax code.
Pick one source before the emitter is written; both work, ambiguity does not.

---

# 7. MENU SEED · RBAC · SEQUENCES · APPROVALS

Follow the FIN pattern exactly (`2026-06_fin_menu_seed.sql`): idempotent pgAdmin script, module + submodules +
menus with `route_path` matching the Angular routes, then **enroll for company 2** (`branch_no NULL` = all
branches). Forms do not appear and RBAC does not resolve until this runs.

**New menus** — seeded 2026-08-13 by `db-migration/2026-08-13_1200_sal_pos_menu_seed.sql`:
`SAL_1001` (POS, route `/sal/pos`), `SAL_1003` (`/sal/form/pos-closing`),
`SAL_1005` (`/sal/form/pos-terminal`). Still to come: `SAL_1104`, `INV_1007`, `INV_2006`.

> **A form needs all three of these, not just the menu row:** a `sys_menu` row under a live
> submodule/module, a `sys_enroll_menu` row for the company, and a `sys_role_permission` row with
> `can_view = 1`. Miss any one and the screen simply never appears — which is nearly always why a
> freshly built form is invisible.

> **SAL_1001 has two menu rows on purpose.** The till (`/sal/pos`) and the credit-invoice form
> (`/sal/form/sales-invoice`) are the same form id, because they are the same `sal_invoice` document
> rung up two ways (sal-business §1.1). The cost: permissions are per form id, so a role that can
> open the till can also open the credit-invoice form. If those ever need separating, give the till
> its own form id — `SAL_1002` is free since the hold drawer replaced it — and move the POS
> controller's route prefix to match.

**`sys_doc_sequence` rows:** `SAL_SESSION`; per-terminal `receipt_prefix` on `sal_pos_terminal`.

**Approval registry (`document_type`):** `SAL_POS_SESSION` (variance sign-off). `SAL_INVOICE` joins only if
**D4** says credit sales need a credit-limit gate.

**Permissions worth calling out** (sal-business §7.1): `can_approve` on SAL_1001 is what unlocks discount over
cap, sale below `min_sale_price`, over-credit-limit, void of a posted sale, and blind return. The POS screen
should ask for a **manager PIN (re-auth)** at that moment rather than hiding the button — that is the retail
idiom and it produces a better audit trail.

---

# 8. THE POS SCREEN (SAL_1001) — frontend spec

Route `sal/pos`, full-screen, no sidebar, touch-friendly. Per sal-business §4.2.

**Layout** — two panes. Left: dense cart grid. Right: totals + tender. Top: always-autofocused barcode/search
input, customer selector (defaults to walk-in), warehouse/terminal/session badge.

**State** — signal store, not `FormGroup` per line: `cart = signal<CartLine[]>`,
`computed` for subtotal/discount/tax/total/change, `activePayments = signal<Payment[]>`. Validate on confirm.

**Add item** — barcode resolve → `inv_product_barcode` returns `(product_no, variant_no, uom_no, pack_qty)`;
scanned qty × `pack_qty`. Miss → search modal. A hidden focused input captures scanner keystrokes (scanners
type fast then Enter) — the same trick INV_1102 already uses.

**Pricing** — debounced `POST /sales/quote-price` on cart change. The screen displays money; the server owns it.

**Tender modal** — numeric keypad, method buttons (Cash / Card / bKash / Nagad / Bank / Credit), split rows,
change computed, Enter adds a tender, Confirm enabled when `paid ≥ total` (or due allowed for a credit customer).

**Keyboard** — `F1` search · `F2` qty · `F3` customer · `F4` discount · `F8` hold · `F9` tender · `F10` confirm ·
`Del` remove line · `+/−` qty · `Esc` cancel. On-screen `?` help overlay.

**Receipt** — 80mm thermal template (logo, items, tax breakdown, tenders, change, QR of `invoice_id`, footer),
auto-print on confirm, reprint from invoice list. Plus A4 tax invoice with company VAT/BIN from `sys_company`.

**Safety** — disable the confirm button on click **and** send `client_uuid`; 409 → reload the invoice.

**Grid note:** the house table is now `<app-erp-grid>` (AG Grid), with `common-table` legacy. For the POS cart
specifically, evaluate whether AG Grid's overhead is worth it for a ≤50-row, scan-heavy, keyboard-driven list —
a plain signal-rendered list may be both faster and simpler here. Use `<app-erp-grid>` for the hold tray,
invoice list, and session summary, where the grid features actually earn their keep.

---

# 9. OFFLINE STRATEGY — phased, not all-or-nothing

Full offline POS (IndexedDB catalog, client-side pricing, queued sync, local receipt numbering) is a large,
risky subsystem. Phase it:

| Phase | Capability | When |
|---|---|---|
| **0** — Online only | Clear "offline" banner; confirm blocked while disconnected; cart preserved in memory | POS-3 |
| **1** — Resilient | Cart persisted to `localStorage` (survives refresh/crash); retry-on-reconnect for a submit that timed out; `client_uuid` makes the retry safe | POS-3 |
| **2** — Read-offline | IndexedDB snapshot of product/barcode/price list pulled at session open; cart building and pricing continue offline; **tender still requires connectivity** | POS-7 |
| **3** — Full offline | Queued sale submission, background sync, server re-prices and validates on sync, discrepancy → manager resolution; offline sales cash-only; credit-limit checks advisory | POS-7 |

**The `client_uuid` unique index (Wave POS-2) is what makes every later phase safe.** Put it in early even if
offline never ships — it is also what makes a double-click harmless.

---

# 10. DECISIONS TO CONFIRM BEFORE WAVE POS-2

| # | Decision | Why it is expensive later |
|---|---|---|
| **D0** | **Which backend does POS ship on — Java, .NET, or both in parity?** | Java is complete through INV; .NET has INV services but only 4 routed controllers, so it cannot get stock in via API today. Building POS on Java widens the port gap by a whole module; building on .NET first requires closing the INV controller hole. Picking "both" doubles every wave. **This gates everything.** |
| **D1** | VAT account routing: `fin_gl_map` `OUTPUT_VAT` leg **or** `sys_vat_tax.gl_account_no` per code | The emitter must be unambiguous; changing it after sales are posted means re-mapping live GL history |
| **D2** | Does `InvStockPostingService` return the costed value of an OUT post? | COGS legs depend on it; if it doesn't, it's a small engine addition in POS-1 and a painful retrofit in POS-3 |
| **D3** | No open session at POS: **block** the sale, or **auto-open** a session? | Changes the tender path and the cashier's first-touch UX |
| **D4** | Do POS credit sales need an approval gate (credit-limit), or only overrides/returns? | Sets whether `SAL_INVOICE` joins the approval registry — retrofitting an approval gate onto a posted-document flow is invasive |
| **D5** | Is serial-number tracking in v1? | `inv_serial` + serial capture at sale and return; affects the cart line model |
| **D6** | Is offline POS in v1? | Phase 2/3 of §9 is a subsystem, not a feature |
| **D7** | Receipt numbering: server `sys_doc_sequence` only, or terminal `receipt_prefix` + local counter reconciled on sync? | Only matters if D6 = yes, but the printed receipt format depends on the answer |

---

# 11. DEFINITION OF DONE + VERIFICATION

## 11.1 Per-wave verification (run all three, every wave)

```bash
cd sme-software-backend && ./mvnw -o -q compile
```

```bash
cd sme-software-backend && ./mvnw -o test -Dtest=AidlyApplicationTests
```

```bash
cd sme-software-frontend && npx nx build web-client
```

If .NET is in scope (D0):

```bash
cd sme-dotnet-backend && dotnet build
```

Then, for the wave's new forms: run the DB migration in pgAdmin → run the menu seed → enroll company 2 →
click through the form in the running app.

## 11.2 Module definition of done (mirrors FIN)

Masters → shared posting services → documents → settle → reports, with:

- stock posted **only** via `InvStockPostingService` — no module writes `inv_stock` directly
- GL posted **only** via the outbox → `FinPostingService` loop, with party-tagged AR/AP legs
- approvals via `ApprovalService` + SYS_1108 config
- period guard honored (FIN_1401)
- gap-free document numbering via `sys_doc_sequence`
- posts idempotent on `(ref_doc_type, ref_doc_no)` — and on `client_uuid` for sales
- soft-delete + audit throughout
- sub-ledger (`sal_customer_ledger`) and GL party tags written **from the same transaction, with the same
  numbers** — they reconcile by construction (pur-business §2)

## 11.3 The POS acceptance test (the one that proves it)

A single end-to-end scenario that must pass before calling POS done:

1. Open a session on terminal T1 with a 5,000 float.
2. Scan 3 items (one batch-tracked, one pack-barcode with `pack_qty=12`).
3. Apply a line discount over cap → manager PIN prompt → approve.
4. Tender split: 2,000 cash + rest bKash. Change computed correctly.
5. Confirm. Verify: one `sal_invoice`, N `sal_invoice_payment` rows, stock relieved once, `sal_customer_ledger`
   correct, one `SalesInvoicePosted` in the outbox → one **balanced** voucher in `fin_ledger`.
6. Re-POST the same `client_uuid` → same invoice returned, **no** second stock movement, **no** second voucher.
7. Hold a second cart → `qty_reserved` rises → available drops → void → `qty_reserved` returns to prior value.
8. Return one line from sale #1 → stock back to the **same batch**, COGS reversed, refund tendered.
9. Close the session: expected cash = float + cash tenders − cash refunds; enter a short count; variance flagged;
   manager signs off; `PosSessionClosePosted` → balanced deposit voucher; Z-report prints.
10. Trial Balance (FIN_1301) still balances. Aging (FIN_1307) reflects any credit portion.

If step 6 or step 10 fails, POS is not done regardless of how good the screen looks.

---

# APPENDIX A — Enum reference (POS additions)

| Field | Values |
|---|---|
| `sal_invoice.sale_type` | 1=POS · 2=CreditInvoice · 3=Quotation |
| `sal_invoice.status` | 1=Draft · 2=Hold · 3=Confirmed · 4=PartiallyReturned · 5=Returned · 6=Cancelled |
| `sal_invoice.payment_status` | 1=Unpaid · 2=Partial · 3=Paid |
| `sal_invoice_payment.payment_method` | 1=Cash · 2=Card · 3=MobileBanking · 4=BankTransfer · 5=Cheque · 6=Credit/Due · 7=LoyaltyPoints · 8=GiftCard · 9=StoreCredit |
| `sal_pos_session.status` | 1=Open · 2=Closing · 3=Closed |
| `inv_reservation.status` | 1=Active · 2=Released · 3=Consumed · 4=Expired |
| `inv_stock_ledger.ref_doc_type` | …4=SalInvoice · 5=SalReturn (already in use by `Sal1001Service`) |
| discount type (`line_` / `bill_`) | 1=Amount · 2=Percent |

---

# APPENDIX B — Form registry after this plan

| Form | Name | State after plan | Wave |
|---|---|---|---|
| INV_1001–1006 | catalog masters | built | — |
| **INV_1007** | **Product Price List** | new | POS-1 |
| INV_1101, INV_1102 | opening stock, adjustment | built | — |
| INV_2001–2005 | warehouse, rack, transfer, batch, reorder | built | — |
| **INV_2006** | **Physical Count / Stocktake** | new | POS-6 |
| PUR_1001–1106 | all 8 purchase forms | built | — |
| **SAL_1001** | **POS Sales Screen** | rebuilt as POS | POS-3 |
| **SAL_1002** | **Draft / Hold tray** | new | POS-4 |
| **SAL_1003** | **POS Closing + Z-report** | new | POS-4 |
| **SAL_1005** | **POS Terminal Setup** | new | POS-2 |
| SAL_1101, SAL_1102, SAL_1103 | customer, due collection, return | built | — |
| **SAL_1104** | **Promotions / Discount Setup** | new | POS-5 |
| FIN_1202 / FIN_1203 | AP / AR sub-ledger readers | deferred → build | POS-6 |

---

# APPENDIX C — Dual-backend parity ledger

Fill this in as D0 is decided; it exists so the two backends never silently diverge again.

| Capability | Java | .NET | Parity action |
|---|---|---|---|
| INV 1002/1006/1101/1102/2001–2005 HTTP routes | ✅ routed | ❌ services exist, **no controllers** | add controllers (or accept Java-only) |
| SAL POS tables + services | ❌ (this plan) | ❌ (this plan) | build once on the chosen backend per D0 |
| `fin_gl_map` seeds | live in `sme-dotnet-backend/db-migration/` | same files | **shared** — keep one canonical location |
| Column-name drift | — | known: business keys mapped to non-existent `*_code`; real schema uses `*_id` | re-verify each new POS entity against the real schema before shipping |

> **Standing rule:** any new POS table gets its entity written **once**, DDL generated from the entity, and the
> generated DDL diffed against the live schema before the migration is committed. That is the check the earlier
> `*_code` mismatches skipped.

---

*This document is the engineering plan for the POS capability and the completion of INV / PUR / SAL. §1 is a
verified audit — trust it over memory. §3–§4 are the exact delta. §5 is the work order. §10 must be answered
before Wave POS-2. It deliberately mirrors the FIN build cadence (masters → shared services → documents →
settle → reports, verified at every wave) because that cadence is what got FIN to feature-complete.*

---

## Runtime verification — 2026-08-14

The first time the API was actually **booted and probed** rather than only compiled.
Everything below was invisible to the build, the unit tests and the architecture tests.

### 1. POS could not serve a single request (fixed)

```
Sal1001Service -> IApprovalService -> IEnumerable<IApprovalCompletedListener>
               -> FinApprovalListener -> IFin1101Service -> IApprovalService   (cycle)
```

`ApprovalService` takes `IEnumerable<IApprovalCompletedListener>` in its constructor, and
`Fin1101Service` takes `IApprovalService`. `FinApprovalListener` injecting `IFin1101Service`
closed the loop, so **every controller downstream of `IApprovalService` threw at construction** —
the whole of POS, returning 500 on every endpoint. `HrmApprovalListener` had the identical defect
(all three of its services take `IApprovalService`); it simply had not surfaced yet.

The app still **starts** with this defect present, because the container only walks the graph when
a controller is first constructed. Nothing catches it earlier.

**Rule:** an `IApprovalCompletedListener` must resolve its document services from `IServiceProvider`
at callback time, never inject them. This is the same rule the approval docs already state for
`ApprovalService` itself; it extends to anything the engine constructs. The scoped provider hands
the listener the engine's own DbContext and transaction, so nothing else about the flow changes.

After the fix, all **112 controllers** construct cleanly and every POS endpoint reaches its service.

### 2. Audit logging failed on every mutating request (migration written, not yet run)

`sys_log.ip_address` and `sys_login_attempt.ip_address` are `inet`; the code writes strings.
Every POST/PUT/PATCH/DELETE — every POS sale — failed its audit-log insert with `42804`.
`sys_login_attempt` is what the brute-force limiter reads, so sign-in is affected too.
See `db-migration/2026-08-14_1100_sys_ip_address_to_varchar.sql`.

**`SchemaDriftTests` does not catch this** — it compares column *names*, not types. Extending it to
compare types is the obvious follow-up; the same gap also hid a `gl_voucher_no` doc/schema drift.

### 3. Promotions will fail at runtime

`sal_customer_group`, `sal_promotion` and `sal_promotion_dtl` do not exist in the database —
`2026-08-13_1300_sal_promotion_schema.sql` has not been run. The POS-5 engine queries them.

### Confirmed working

- The application boots against the live database and serves requests.
- All 112 controllers resolve their dependencies — no other DI cycles anywhere.
- POS core tables (`sal_pos_terminal`, `sal_pos_session`, `sal_invoice_payment`, and the
  `sal_invoice` / `sal_invoice_dtl` additions) exist, and **every mapped column matches** the database.
- Every POS route the frontend calls exists on the backend and reaches its service.

### Still not verified

No authenticated request has been made, so no sale has gone through the till. Everything above
tests routing, wiring and schema — not business behaviour. A real till run needs credentials.

---

## End-to-end till run — 2026-08-14

A real sale, driven through the API against the live database with a real login.
Everything here was invisible to the build, the unit tests and the architecture tests.

### The sale that worked

```
Session SES000001 · float 1000
2 x Samsung Galaxy A55 @ 1000  ->  R1000002 · cash 2000
```

| Check | Result |
|---|---|
| Stock | 100 → 98 |
| Tender row | cash 2000, change 0 |
| Replay of the same `client_uuid` | returned the original invoice, no second sale |
| GL voucher | Dr Cash 2000 / Cr Revenue 2000 · Dr COGS 1600 / Cr Inventory 1600 — balanced |
| Z-report | expected drawer 3000, counted 3000, variance 0 |
| SAL_1301 | 1 invoice, margin 400 (20%) |
| FIN_1203 | sub-ledger 0 = GL 0, reconciled |

A second sale (1 × iPhone 15) then posted to the GL **unattended on the first
attempt** — no manual re-drive — confirming the atomicity fix below.

### Three NOT NULL columns the code never set

Each one made a whole path impossible, and all three compiled and passed every test:

| Column | Effect |
|---|---|
| `sal_invoice.fin_year_no` | POS resolved the year during *posting*, but the invoice is saved before that. The till could never insert a sale. |
| `sal_customer.is_credit_allowed`, `loyalty_points` | Unmapped on the entity, so no customer could be created — and POS requires one. |
| `fin_voucher_dtl.company_no`, `branch_no` | Unmapped, while `FinVoucher` and `FinLedger` both carry them. **No voucher this backend writes could save** — the entire GL pipeline, not just POS. |

**`SchemaDriftTests` cannot catch these.** It verifies that mapped columns exist in
the database. These are the reverse: database columns that are required and *not*
mapped. Extending it in that direction is the obvious follow-up — this one gap hid
three separate outages.

### Voucher creation was neither atomic nor correctly numbered (fixed)

`Fin1101Service` inserted the header with `voucher_id = "TEMP"`, saved to obtain the
voucher number, stamped the real number, then saved the lines — two saves, no
transaction. A failure in between left a header with **zero lines and the literal id
"TEMP"**, and because the source-doc unique index had already accepted that row, every
later retry of the document collided with the wreckage. The FIN_1201 re-drive escape
hatch was unusable exactly when it was needed.

Both halves are fixed: the number is drawn from the doc sequence **before** the insert
so no placeholder is ever written, and the two saves are wrapped in one transaction
that rolls back together. The transaction is skipped when one is already ambient (the
posting engine opens its own) and when the provider is non-relational (the unit tests
use the in-memory store, which throws rather than ignoring `BeginTransaction`).

### The posting failure reason was being discarded (fixed)

`MarkFailedAsync` took an `ex.Message` parameter and never used it, so FIN_1201 showed
Failed events with nothing to diagnose them by. It now logs event number, type,
aggregate and reason. There is no `error_message` column on `sys_event_outbox`; adding
one would put the reason where the operator actually looks.

### Two lookup bugs

`hrm_employee` has no `full_name` column (it stores first/middle/last), so every
party-type-3 lookup failed with 42703. Separately, SAL_1301 was passing a `sys_user`
key to that employee lookup — a cashier is a user, and the two are different key
spaces. Added `IPartyLookup.GetUserNamesAsync` and pointed the report at it.

### Setup gaps

Nothing in SAL/INV was enrolled for company 2 — granted at role level, no enrolment
row, so 403 everywhere including the POS sale endpoint. There is also **no walk-in
customer seed**, though POS requires a customer.

### Still open

- **`GlPosted` events fail**, so `sal_invoice.gl_voucher_no` is never stamped back.
  The GL voucher itself is correct and complete; this is the callback that links the
  document to it. `status = 3` with `retry_count = 1` matches neither branch of
  `MarkFailedAsync`, so something else is setting it — needs its own look.
- `inv_physical_count_dtl.variance_qty` is a `GENERATED ALWAYS` column, which Postgres
  reports as nullable; EF maps it non-nullable and will try to insert it. INV_2006 has
  not been exercised.
- Two GL maps (`CustomerReceiptReversed`, `SalesReturnReversed`) whose seed file is
  still unrun.

---

## Live FIN → POS → Sales Return flow — 2026-08-14

Driven through the API against the live database, verifying each step's effect.

| Step | Result |
|---|---|
| FIN trial balance (baseline) | 28 accounts, Dr = Cr = 148,700 |
| Open till session | float 500 |
| Quote 2 × Samsung A55 | sub 2000, tax 0, grand 2000 |
| Confirm sale | `R1000014`, status Posted, COGS 1600 |
| Create return (1 unit, restock) | `RET-000001`, reason 1, refund_method 1 (Cash) |
| Post return | status Posted, total_cost 800 |
| Stock | 90 − 1 − 2 + 1 restocked = **88** |
| Trial balance after | Dr = Cr = 154,100 |

The two vouchers mirror each other exactly:

```
SALE    Dr Cash 2000        Cr Revenue 2000
        Dr COGS 1600        Cr Inventory 1600

RETURN  Dr Sales Return 1000  Cr Cash 1000
        Dr Inventory 800      Cr COGS 800
```

**Every field fixed earlier in the session round-tripped**: the return persisted
`return_reason`, `refund_method`, `net_unit_price` and `restock_flag`, all of which
had previously been sent by the form and silently dropped.

### Two defects this flow exposed

**1. An unrun migration broke ALL GL posting.** Adding `pattern` / `doc_sub_type` /
`menu_no` / `starting_no` to the `DocSequence` entity for SYS_1301 without running
`2026-08-14_1400` meant every voucher post failed with
`42703: column s.doc_sub_type does not exist` — because voucher creation draws its
number from `sys_doc_sequence`. `SchemaDriftTests` had reported exactly those four
columns; the warning was read as a benign pending migration rather than a live
outage. **An entity column added ahead of its migration is not inert — it breaks
every query that touches the table.**

**2. Sales-return GL mapping was incomplete in three ways.** `Sal1103Service` emits
six leg keys; only SALES_RETURN, VAT_OUTPUT and RECEIVABLE were mapped, and
`SalesReturnReversed` had nothing at all. A cash refund — the ordinary counter case —
could never post, and neither could the stock side of any return. Fixed by
`2026-08-14_1500`.

### Design gap worth deciding on

**A cash refund does not touch the till.** `sal_return` has no session or terminal
link, so the session summary still reported `returns 0` after a cash refund was
posted. The drawer believes it holds money that was handed back, and the Z-report
will reconcile against the wrong expected cash. A return taken at the counter should
attach to the open session; a return taken at a back office should not.
