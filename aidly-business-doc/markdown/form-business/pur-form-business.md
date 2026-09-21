# PUR Form Business

<!-- Consolidated from the previous aidly-business-doc/backend, frontend, memory, and planning files. -->
# PUR Module â€” Perâ€‘Form Design (Business Â· Middleware Â· Frontend)

> Companion to **`pur-db.md`** (DB blueprint), same format as `inv-forms-design.md`.
> Defines, **per PUR form**, the *business*, the *middleware* (Spring Boot), and the *frontend* (Angular 21).
>
> **Backend pkg:** `com.infoaidtech.aidly.pur` Â· **Frontend feature:** `apps/web-client/src/app/features/pur`
> **API base:** `/api/v1/pur/forms/{formId}` (formâ€‘wise) Â· generic at `/api/v1/pur/{resource}`
> **Hard dependency:** the **INV module must exist first** â€” Purchase never writes `inv_stock`; it calls
> `InvStockPostingService` (movement 2 IN / 3 OUT), **creates `inv_batch`** at receipt, and reads
> `inv_product`, `inv_warehouse`, `inv_uom_conversion`. Reuses `sys_doc_sequence`, `sys_event_outbox`,
> `sys_vat_tax`, `sys_currency`, `sys_fin_year(_dtl)`.
> Purchase is the inbound mirror of Sales: it sets the **landed cost** that becomes inventory valuation
> (so downstream COGS/margins are correct) and owns the supplier + **AP/payable** ledger + buyâ€‘side GL.
> **Server is the sole authority for costs/tax/totals/landed allocation/payable** (pur-db.md Â§7.4).

---

## Build order (dependency tiers)

| Tier | Build | Why |
|---|---|---|
| 0 â€” Masters | **PUR_1001 Supplier**, Supplier Price List | every document needs a supplier |
| 1 â€” Buyâ€‘side services | `PurReceivingPostingService`, `PurApLedgerService`, `PurLandedCostAllocator` (internal) | the shared receive/post path wrapping `InvStockPostingService` |
| 2 â€” Commitment | **PUR_1101 Purchase Order** | no stock/GL â€” pure commitment + approval |
| 3 â€” Receiving & payable | **PUR_1105 GRN** (optional), **PUR_1102 Purchase Invoice**, **PUR_1106 Landed Cost** (optional) | bring goods in at landed cost + create AP |
| 4 â€” Settle | **PUR_1103 Purchase Return**, **PUR_1104 Supplier Payment** | reduce/settle the payable |

PUR as a whole sits **after INV**. Within PUR, tier *n* waits on tier *< n*.

---

## A. Shared backend infrastructure (build once)

### A.1 `PurReceivingPostingService` â€” the receive/post choke point (pur-db.md Â§3.2)
One `@Transactional` per posted receiving document:
- **Oneâ€‘step (`receive_mode=1`, SME default):** per line `final_unit_cost = (line_total_ex_tax +
  landed_alloc) / qty_base`; resolve/create `inv_batch` (code+mfg+expiry) for batchâ€‘tracked products;
  call `InvStockPostingService` movement 2 (IN) at `final_unit_cost`; update PO `received_qty_base`;
  `PurApLedgerService.credit(supplier, grand_total)`; emit `fin_voucher` (Dr Inventory + Input VAT, Cr AP).
- **Twoâ€‘step (`receive_mode=2`):** GRN already posted stock at provisional PO cost (Dr Inventory, Cr
  GRNâ€‘Clearing); the **invoice does not reâ€‘receive** â€” it clears GRNâ€‘Clearing + posts AP; price â‰  GRN cost
  â‡’ **purchaseâ€‘price variance** (revalue onâ€‘hand portion, PPV for consumed portion).
- Idempotent on `(ref_doc_type, ref_doc_no)`; duplicate supplier bill blocked by `uq_pur_inv_sup`.

### A.2 `PurApLedgerService` â€” AP subsidiary ledger
Appendâ€‘only `pur_supplier_ledger` (credit=invoice, debit=payment/return); `pur_supplier.current_payable`
= last `balance_after`. Never edited; corrections are reversing rows. Source for payable aging.

### A.3 `PurLandedCostAllocator` (PUR_1106)
Allocate freight/duty/clearing/insurance across received lines by basis (value/qty/weight, Î£ = total) â†’
bump `final_unit_cost` â†’ engine writes a **valueâ€‘only revaluation** to `inv_stock`/layers for onâ€‘hand qty;
the share attributable to alreadyâ€‘sold qty posts to COGS/landedâ€‘costâ€‘variance.

### A.4 PO state engine
PO totals serverâ€‘computed from lines; approval gate by value threshold (`can_approve`);
`received_qty_base` incremented by GRN/invoice; header autoâ€‘advances Approvedâ†’PartiallyReceivedâ†’Received
within over/under tolerance; overâ€‘receipt beyond tolerance blocked unless `can_approve`;
`on_order = Î£(order_qty_base âˆ’ received_qty_base)` over open POs feeds INV reorder suggestions.

### A.5 Reused: `InvDocSequenceService` (PUR_PO/PUR_GRN/PUR_INV/PUR_RET/PUR_PAY), period guard, UOM `toBaseQty`, FX conversion, tenant/RBAC, `sys_event_outbox`.

### A.6 Shared frontend pieces (pur-db.md Â§4.6)
`pur-supplier-picker`, reused `inv-product-picker`/`inv-batch-picker`, `po-pull-modal`,
`payment-allocation-grid`, `aging-badge`, `batch-input`.

---

## B. Supporting master (Tier 0)

| Master | Table | Form id (suggested) | Notes |
|---|---|---|---|
| Supplier Price List | `pur_supplier_product` | PUR_1002 | perâ€‘supplier SKU/price/leadâ€‘time/MOQ + `is_preferred`; feeds PO line default price; usually a tab inside PUR_1001 plus a standalone grid |

---

## 1. PUR_1001 â€” Supplier Management

### Business
Vendor master: terms (`payment_terms`, `credit_days`, `credit_limit`), tax IDs (VAT/TIN/BIN/trade
license), banking + MFS, opening payable, lead time, rating. **`current_payable` is derived** (AP ledger
only). Delete blocked when payableâ‰ 0 or documents exist (discontinue). Referenced by `inv_reorder`,
`inv_batch`, `inv_serial` (`preferred_supplier_no`). RBAC: Purchaser CRUD Â· Manager (credit terms) Â· Admin delete.

### Middleware â€” `/api/v1/pur/forms/pur1001`
Tables: `pur_supplier` (+ `pur_supplier_product`, + read `pur_supplier_ledger`). DTOs: `Pur1001SupplierDto`, `Pur1001PriceListRow`, `Pur1001LedgerRow`.

| Method | Path | Purpose |
|---|---|---|
| GET | `/suppliers`, `/suppliers/page?q` | list / trgm typeahead |
| GET | `/suppliers/{no}` | detail (+ price list) |
| GET | `/suppliers/{no}/ledger` | AP running balance + aging |
| POST | `/suppliers` | upsert (`current_payable` ignored from client) |
| DELETE | `/suppliers/{no}` | soft delete (409 if payable/docs) |
| GET/POST | `/suppliers/{no}/products` | priceâ€‘list rows |
| GET | `/lookups` | currency / tax / product pickers |

Rules: unique `supplier_id` & `mobile_no` per company (live); creditâ€‘term fields gated to Manager; opening_balance posts an `Opening` AP ledger row.

### Frontend â€” `pur/forms/pur1001` (masterâ€‘detail + tabs)
`page-form-layout`: left list (search, **payable badge**); right tabs **General Â· Terms Â· Banking Â·
Price List Â· Ledger**. Price List tab = editable grid (`pur_supplier_product`). Ledger tab =
`pur_supplier_ledger` running balance + aging. `current_payable` readâ€‘only; banking + credit terms gated.

---

## 2. PUR_1101 â€” Purchase Order

### Business
Procurement commitment with approval; tracks ordered vs received. **No stock/GL impact.** `Draft â†’
Submitted â†’ Approved â†’ (PartiallyReceived â†’ Received) / Closed / Cancelled`. Approval (`can_approve`)
required above a value threshold before goods can be received against it. RBAC: Purchaser create/submit Â·
**Manager approve** Â· Admin delete.

### Middleware â€” `/api/v1/pur/forms/pur1101`
Tables: `pur_order(_dtl)`. DTOs: `Pur1101OrderDto` + line.

| Method | Path | Guard |
|---|---|---|
| GET | `/orders`, `/orders/page`, `/orders/{no}` | can_view |
| POST | `/orders` | draft upsert (server totals) â€” can_insert/update |
| POST | `/orders/{no}/submit` | Draftâ†’Submitted |
| POST | `/orders/{no}/approve` | Submittedâ†’Approved â€” **can_approve** |
| POST | `/orders/{no}/cancel` | Draft/Submitted/Approvedâ†’Cancelled (if nothing received) |
| GET | `/orders/open?supplierNo` | open POs for the invoice/GRN "pull" modal |

Rules: totals from lines (price âˆ’ discount + tax); `order_id` from docâ€‘sequence at submit; default line price from `pur_supplier_product.last_price`; cannot cancel once partially received.

### Frontend â€” `pur/forms/pur1101` (document + approval)
`page-form-layout`; header (supplier, warehouse, expected date, currency) + lines grid (product picker,
qty, price, discount, tax) + totals. Workflow buttons Save/Submit/**Approve**(`canApprove`)/Cancel by
status; status chip; "Convert to Invoice/GRN" action; printable PO (A4).

---

## 3. PUR_1105 â€” Goods Receipt Note (GRN)  *(optional, twoâ€‘step)*

### Business
Physical receipt that **increases stock at provisional cost** (Dr Inventory, Cr GRNâ€‘Clearing) before the
invoice arrives; captures batch/expiry; enables 3â€‘way match. `Draft â†’ Posted â†’ Invoiced â†’ Cancelled`.
Skipped entirely in oneâ€‘step mode (`is_grn_required=0`). RBAC: Receiving create Â· Manager.

### Middleware â€” `/api/v1/pur/forms/pur1105`
Tables: `pur_receipt(_dtl)`. Uses `PurReceivingPostingService` (provisional). DTOs: `Pur1105ReceiptDto` + line (incl. batch fields).

| Method | Path | Guard |
|---|---|---|
| GET | `/receipts`, `/receipts/{no}` | can_view |
| POST | `/receipts` | draft upsert (pull from approved PO) |
| POST | `/receipts/{no}/post` | stock IN at PO cost + GRNâ€‘Clearing voucher + PO received_qty â€” can_insert |
| POST | `/receipts/{no}/cancel` | reverse stock + voucher â€” can_approve |

Rules: receive only against an Approved PO when `po_required=1`; overâ€‘receipt tolerance; create `inv_batch`; period guard.

### Frontend â€” `pur/forms/pur1105`
Header (supplier, PO pull, warehouse, date) + lines (ordered vs receiving qty with variance highlight,
perâ€‘line batch/expiry inputs). Post â†’ stock IN; printable GRN note. Tabletâ€‘friendly for the warehouse.

---

## 4. PUR_1102 â€” Purchase Invoice / Bill  *(reference screen)*

### Business
Creates the **payable** and (oneâ€‘step) **receives stock** in one document. `Draft â†’ Posted â†’
PartiallyReturned/Returned / Cancelled`. Oneâ€‘step: Dr Inventory + Input VAT, Cr AP, stock IN at landed
cost. Twoâ€‘step: clears GRNâ€‘Clearing + posts AP, reconciles price/qty variance (PPV). **Cannot cancel if
goods already sold** (use a return). Duplicate supplier bill blocked (`uq_pur_inv_sup`). RBAC: Clerk/Acct
create Â· Manager (variance/overâ€‘receipt).

### Middleware â€” `/api/v1/pur/forms/pur1102`
Tables: `pur_invoice(_dtl)` (+ optional linked `pur_payment`). Uses `PurReceivingPostingService` +
`PurApLedgerService`. DTOs: `Pur1102InvoiceDto` + line + optional `Pur1102PaymentOnReceiptDto` (pur-db.md Â§5.2).

| Method | Path | Guard |
|---|---|---|
| GET | `/invoices`, `/invoices/{no}` | can_view |
| POST | `/invoices` | draft upsert | can_insert/update |
| POST | `/invoices/quote` | server totals/final_unit_cost preview |
| GET | `/po/{orderNo}/pull`, `/grn/{receiptNo}/pull` | copy remaining lines |
| POST | `/invoices/{no}/post` | stock IN (oneâ€‘step) + AP + Input VAT + GL + PO received_qty â€” engine |
| POST | `/invoices/{no}/cancel` | reverse (block if goods sold) â€” can_approve |

Rules: `supplier_invoice_no` unique per supplier (dupâ€‘bill block); overâ€‘receipt beyond tolerance â‡’ `can_approve`; FX at posting; create `inv_batch`; period guard; optional paymentâ€‘onâ€‘receipt creates linked `pur_payment`.

### Frontend â€” `pur/forms/pur1102`
`page-form-layout`; header (supplier, supplierâ€‘invoiceâ€‘no+date, warehouse, currency, due date,
`receive_mode`) + lines grid + totals/landed/payment panel. **Load PO / Load GRN** modal copies remaining
lines (qty = outstanding, variance highlight). Perâ€‘line batch/expiry inputs when batchâ€‘tracked;
`final_unit_cost` readâ€‘only. Optional immediate payment block. Inline dupâ€‘bill error; overâ€‘receipt approval
prompt; closedâ€‘period block; 409 reload. Shortcuts `F2`/`F3`/`F4`/`Ctrl+S`/`Ctrl+Enter`. A4 invoice print.

---

## 5. PUR_1106 â€” Landed Cost  *(optional)*

### Business
Allocate freight/duty/clearing/insurance/handling across received invoice lines so unit cost (and
inventory valuation) reflects true landed cost. Onâ€‘hand share revalues stock/layers; sold share goes to
COGS/landedâ€‘costâ€‘variance. RBAC: Accountant Â· Manager.

### Middleware â€” `/api/v1/pur/forms/pur1106`
Tables: `pur_landed_cost(_alloc)`. Uses `PurLandedCostAllocator` + engine revaluation. DTOs: `Pur1106LandedCostDto` + alloc.

| Method | Path | Purpose |
|---|---|---|
| GET/POST | `/landed-costs`, `/landed-costs/{no}` | list / draft |
| POST | `/landed-costs/{no}/apply` | allocate â†’ revalue stock/layers + GL |

Rules: Î£ allocations = total charge; basis (value/qty/weight) validated; revaluation ledger written, not a qty movement.

### Frontend â€” `pur/forms/pur1106`
Pick invoice(s) â†’ charge lines (type, amount, basis) â†’ allocation preview grid (per line bump) â†’ Apply. Printable landedâ€‘cost sheet.

---

## 6. PUR_1103 â€” Purchase Return / Debit Note

### Business
Send goods back to supplier (defective/excess). `Draft â†’ Approved/Posted â†’ Cancelled`. Validate `qty â‰¤
received âˆ’ already_returned` against the original invoice line; stock OUT movement 3 at original landed
cost; AP **debit** (debit note) or cash refund; GL reversal. RBAC: Clerk create Â· **Manager post/blind**.

### Middleware â€” `/api/v1/pur/forms/pur1103`
Tables: `pur_return(_dtl)`. Uses engine (movement 3) + `PurApLedgerService`. DTOs: `Pur1103ReturnDto` + line; `Pur1103ReturnableRow`.

| Method | Path | Guard |
|---|---|---|
| GET | `/returns`, `/returns/{no}` | can_view |
| GET | `/invoices/{invoiceNo}/returnable` | returnable lines (qty cap, landed cost) |
| POST | `/returns` | draft upsert â€” can_insert |
| POST | `/returns/{no}/approve` | post â†’ stock OUT + AP/GL â€” **can_approve** |
| POST | `/returns/{no}/cancel` | reverse â€” can_approve |

Rules: `settlement_mode` (adjust payable / cash refund / replacement); restock OUT from the same batch; period guard.

### Frontend â€” `pur/forms/pur1103`
Originalâ€‘invoice lookup â†’ returnable lines (qty cap) â†’ pick qty + reason + settlement mode â†’ totals;
approval gate. Printable debit note.

---

## 7. PUR_1104 â€” Supplier Payment

### Business
Settle AP. `Draft â†’ Posted â†’ Cancelled`. Allocate to invoices (explicit or FIFO oldestâ€‘first); apply debit
notes (`pur_return`) as negative allocations; advances â†’ `unallocated_amount`. GL Dr AP, Cr Cash/Bank;
advance â†’ Dr Supplier Advance. Cancel reverses + reâ€‘opens dues. RBAC: Accountant Â· Manager (cancel);
banking/payment restricted to finance roles.

### Middleware â€” `/api/v1/pur/forms/pur1104`
Tables: `pur_payment(+_alloc)`. Uses `PurApLedgerService`. DTOs: `Pur1104PaymentDto` + alloc; `Pur1104OpenInvoiceRow`.

| Method | Path | Purpose |
|---|---|---|
| GET | `/suppliers/{no}/open-invoices` | open bills (due, age) + open debit notes |
| POST | `/payments` | post payment + allocations (Î£ alloc â‰¤ amount) |
| POST | `/payments/auto-allocate` | FIFO preview |
| POST | `/payments/{no}/cancel` | reverse â€” Manager |

Rules: allocation per invoice â‰¤ due; `payment_id` from docâ€‘sequence (PUR_PAY); period guard; FX.

### Frontend â€” `pur/forms/pur1104`
Supplier select â†’ openâ€‘bills grid (due, age `aging-badge`) + allocate inputs + **Autoâ€‘allocate (FIFO)**;
apply debit notes as negative rows; header (method/amount/bank/cheque); `unallocated_amount` shown for
advances. Printable payment voucher.

---

## 8. Crossâ€‘cutting: documentâ€‘form wiring checklist
For each posted document form (PUR_1101/1102/1103/1104/1105/1106):
1. Entities + repositories (header/detail cascade; `uq_pur_inv_sup` on invoice).
2. DTOs (`@Valid @NotEmpty` lines; serverâ€‘controlled `status`/cost/tax/totals).
3. `PurXXXXService`: draft upsert â†’ action methods calling `PurReceivingPostingService` /
   `PurApLedgerService` / `PurLandedCostAllocator` + `InvStockPostingService`; docâ€‘sequence; period guard;
   dupâ€‘bill + idempotency; manual toDto.
4. `PurXXXXController` at `/api/v1/pur/forms/purXXXX`; `ApiResponse<T>`.
5. Frontend `data.service`/`model.service`/`component` + route in `pur.routes.ts`.
6. DB seed: `sys_menu` row (`form_id=PUR_xxxx`, `route_path`) + `sys_doc_sequence` + `fin_account` map
   (inventory/GRNâ€‘clearing/inputâ€‘VAT/AP/PPV/cash/supplierâ€‘advance) + company config
   (`is_grn_required`, `po_required`, tolerances, PO approval threshold) + `sys_enroll_menu`.
7. Tests: cost/landed math, over/underâ€‘receipt tolerance, state machine, atomic post + rollback, dupâ€‘bill
   block, AP invariant `current_payable == last balance_after == Î£ invoice âˆ’ Î£ payment âˆ’ Î£ return`.

Masters (PUR_1001/1002) skip the posting/sequence steps.

---

## 9. Suggested implementation waves
- **Wave I (master):** PUR_1001 Supplier + Supplier Price List â€” CRUD; verify compile + tsc.
- **Wave II (buyâ€‘side core):** `PurReceivingPostingService` + `PurApLedgerService` + `PurLandedCostAllocator` (unitâ€‘tested hard, no UI).
- **Wave III (commitment):** PUR_1101 PO.
- **Wave IV (receiving):** PUR_1105 GRN (if `is_grn_required`), PUR_1102 Invoice, PUR_1106 Landed Cost.
- **Wave V (settle):** PUR_1103 Return, PUR_1104 Payment.
- **Wave VI:** reports/MVs (`mv_pur_supplier_aging`, `mv_pur_spend_by_category`, `mv_pur_supplier_performance`).

Each wave verified with `./mvnw -o -q compile` and `tsc --noEmit -p apps/web-client/tsconfig.app.json`.

