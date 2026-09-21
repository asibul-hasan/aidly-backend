# SAL Form Business

<!-- Consolidated from the previous aidly-business-doc/backend, frontend, memory, and planning files. -->
# SAL Module â€” Perâ€‘Form Design (Business Â· Middleware Â· Frontend)

> Companion to **`sal-db.md`** (DB blueprint), same format as `inv-forms-design.md`.
> Defines, **per SAL form**, the *business*, the *middleware* (Spring Boot), and the *frontend* (Angular 21).
>
> **Backend pkg:** `com.infoaidtech.aidly.sal` Â· **Frontend feature:** `apps/web-client/src/app/features/sal`
> **API base:** `/api/v1/sal/forms/{formId}` (formâ€‘wise) Â· generic at `/api/v1/sal/{resource}`
> **Hard dependency:** the **INV module must exist first** â€” Sales never writes `inv_stock`; it calls
> `InvStockPostingService` (movement 4 OUT / 5 IN), and reads `inv_product`, `inv_product_barcode`,
> `inv_batch`, `inv_warehouse`, `inv_product_price`. Also reuses `sys_doc_sequence`,
> `sys_event_outbox`, `sys_vat_tax`, `sys_currency`, `sys_fin_year(_dtl)`.
> **Conventions:** entities extend `BaseEntity` (company_no+branch_no); DTOs snake_case;
> `ApiResponse<T>`; soft delete; `row_version`; RBAC by `form_id`; **server is the sole authority for
> every price/tax/total/COGS/due â€” client totals are inputs only** (sal-db.md Â§7.4).

---

## Build order (dependency tiers)

| Tier | Build | Why |
|---|---|---|
| 0 â€” Masters/config | Customer Group, **POS Terminal**, **SAL_1101 Customer**, **SAL_1104 Promotions** | sale needs a customer, a terminal, and a price/promo source |
| 1 â€” Sellâ€‘side services | `SalPricingService`, `SalArLedgerService`, `SalSalePostingService` (internal) | the shared confirm/post path that wraps `InvStockPostingService` |
| 2 â€” POS | **SAL_1003 POS Session**, **SAL_1001 POS Sales**, **SAL_1002 Hold/Draft** | the live selling motion |
| 3 â€” Postâ€‘sale | **SAL_1103 Return**, **SAL_1102 Due Collection** | depend on a posted invoice + AR ledger |

SAL as a whole sits **after INV**. Within SAL, tier *n* waits on tier *< n*.

---

## A. Shared backend infrastructure (build once)

### A.1 `SalPricingService` â€” deterministic pricing (sal-db.md Â§3.1)
For each line, serverâ€‘computes in fixed order: **base price** (`inv_product_price` by branch/tier/qtyâ€‘break/date â†’ variant â†’ product `sale_price`; enforce `â‰¥ min_sale_price` (override = `can_approve`) and `â‰¤ mrp`) â†’ **line discount** (within role cap) â†’ **promotions** (via A.2) â†’ **bill discount** spread proportionally to lines â†’ **tax** per line `vat_tax_no` (inclusive extract / exclusive add) â†’ **roundâ€‘off**. Returns a fullyâ€‘priced cart; **clientâ€‘sent totals are ignored** (or 400 in strict mode).

### A.2 `SalPromotionEvaluator`
Evaluates active `sal_promotion` against a cart by `priority`; nonâ€‘stackable picks highest value, stackable accumulates; BuyXGetY injects free lines (`is_free_item=1`, price 0); respects `usage_limit`, time/weekday window, `max_discount_amount`. Drives both POS pricing and the SAL_1104 "evaluate" preview.

### A.3 `SalSalePostingService` â€” the confirm/post choke point (sal-db.md Â§3.2)
One `@Transactional` per posted sale: (1) resolve batches FEFO + validate availability under the engine's `FOR UPDATE`; (2) call `InvStockPostingService` movement 4 (OUT) â†’ fills `unit_cost`/COGS, consumes any reservation; (3) compute payments, `due = grand_total âˆ’ paid`, `payment_status`; (4) `SalArLedgerService.debit(customer, grand_total)` for the credit portion; (5) emit `fin_voucher` (sal-db.md Â§3.5); (6) update `sal_pos_session` running totals; (7) outbox `SaleConfirmed`/`LowStockDetected`. **Idempotent on `client_uuid`** (returns the existing doc on replay). Creditâ€‘limit guard preâ€‘post (`current_due+new_due > credit_limit` â‡’ `can_approve`).

### A.4 `SalArLedgerService` â€” AR subsidiary ledger
Appendâ€‘only `sal_customer_ledger` (debit=invoice, credit=receipt/return); `sal_customer.current_due` = last `balance_after`. Never edited; corrections are reversing rows. Source for aging.

### A.5 Reused: `InvDocSequenceService` (SAL_INV/SAL_RET/SAL_RCPT), period guard, UOM `toBaseQty`, tenant/RBAC, `sys_event_outbox`.

### A.6 Shared frontend pieces (sal-db.md Â§4.7)
`sal-customer-picker`, `tender-modal` (numeric keypad + split methods + change), `cart-line`,
`receipt-print` (80mm), `aging-badge`, plus reused `inv-product-picker`/`inv-batch-picker`.

---

## B. Supporting masters (Tier 0 â€” SYS1007â€‘style CRUD)

| Master | Table | Form id (suggested) | Notes |
|---|---|---|---|
| Customer Group | `sal_customer_group` | SAL_1105 | default tier/creditâ€‘days/discount; flat CRUD |
| POS Terminal | `sal_pos_terminal` | SAL_1106 | binds a `warehouse_no` (stock source) + receipt prefix + cash GL account; `device_uuid` for offline |

Both = `page-form-layout` list + form, company/branch scoped, unique id per branch, `performSoftDelete()`.

---

## 1. SAL_1101 â€” Customer Management

### Business
Buyer master: credit control (`is_credit_allowed`, `credit_limit`, `credit_days`), tier (â†’ `inv_product_price`),
loyalty points, tax IDs, opening balance. **`current_due` is derived** (AR ledger only â€” never edited
on the form). Walkâ€‘in is a seeded `customer_type=1`, no credit. Delete blocked when dueâ‰ 0 or invoices
exist (discontinue instead). RBAC: Sales CRUD Â· Manager (credit limit) Â· Admin delete.

### Middleware â€” `/api/v1/sal/forms/sal1101`
Tables: `sal_customer` (+ read `sal_customer_ledger`). DTOs: `Sal1101CustomerDto`, `Sal1101LedgerRow`.

| Method | Path | Purpose |
|---|---|---|
| GET | `/customers`, `/customers/page?q` | list / trgm typeahead |
| GET | `/customers/{no}` | detail |
| GET | `/customers/{no}/ledger` | AR running balance + aging buckets |
| POST | `/customers` | upsert (`current_due`, `loyalty_points` ignored from client) |
| DELETE | `/customers/{no}` | soft delete (409 if due/invoices) |
| GET | `/lookups` | groups / currencies / warehouses / tiers |

Rules: unique `customer_id` & `mobile_no` per company (live); credit fields gated to Manager; opening_balance posts an `Opening` AR ledger row on create.

### Frontend â€” `sal/forms/sal1101` (masterâ€‘detail + ledger tab)
`page-form-layout`: left list (search, **due badge**); right `.form-tab-btn` tabs **General Â· Credit Â·
Address Â· Ledger**. Ledger tab = `app-common-table` of `sal_customer_ledger` (debit/credit/balance) +
aging summary. `current_due` readâ€‘only; creditâ€‘limit field disabled unless `canApprove()`.

---

## 2. SAL_1104 â€” Promotions / Discount Setup

### Business
Ruleâ€‘driven automatic offers, timeâ€‘ and conditionâ€‘bound (sal-db.md Â§2.11): `promo_type`
1â€“8 (LinePct/LineAmt/BillPct/BillAmt/BuyXGetY/QtyBreak/Coupon/Bundle), `scope_type`
(product/category/brand/all/customer), conditions (min qty/amount, buy/get qty), `priority`,
`is_stackable`, `usage_limit`, validity window (dates + time + weekday mask). Targets live in
`sal_promotion_dtl` (`target_role` 1=condition/buy, 2=reward/get). RBAC: Manager CRUD.

### Middleware â€” `/api/v1/sal/forms/sal1104`
Tables: `sal_promotion(_dtl)`. DTOs: `Sal1104PromotionDto` + `Sal1104TargetDto`.

| Method | Path | Purpose |
|---|---|---|
| GET | `/promotions`, `/promotions/{no}` | list / detail |
| POST | `/promotions` | upsert header + targets (reconcile) |
| DELETE | `/promotions/{no}` | soft delete |
| POST | `/promotions/evaluate` | cart â†’ applicable promos (preview, uses `SalPromotionEvaluator`) |
| GET | `/lookups` | product/category/brand pickers |

Rules: `end_date â‰¥ start_date`; unique `promotion_id` per company; coupon code unique among active; validate target_role/qty for BuyXGetY.

### Frontend â€” `sal/forms/sal1104` (rule builder)
`page-form-layout` list + form. Promoâ€‘type select **drives which condition fields show** (pct/amount/buyâ€‘get/qtyâ€‘break). Schedule row (dates, optional time, weekday checkboxes â†’ mask). Targets grid: two sections (Condition / Reward) populated via `inv-product-picker` + category/brand selects. A "Test against a sample cart" panel calls `/evaluate`.

---

## 3. SAL_1003 â€” POS Session / Closing

### Business
A cashier's drawer period on a terminal; **one open session per terminal** (`uq_sal_sess_open`).
`Open â†’ Closing â†’ Closed`. On close, system computes expected tender totals from
`sal_invoice_payment` (minus returns); cashier enters counted cash; `cash_variance = counted âˆ’
(opening_float + expected_cash)`; variance beyond tolerance â†’ manager signâ€‘off; deposit voucher
posted; Zâ€‘report; no further sales on a closed session. RBAC: Cashier open/close Â· Manager variance.

### Middleware â€” `/api/v1/sal/forms/sal1003`
Tables: `sal_pos_session`. DTOs: `Sal1003SessionDto`, `Sal1003SummaryDto`, `Sal1003CloseRequest`.

| Method | Path | Purpose |
|---|---|---|
| POST | `/sessions/open` | open with `opening_float` (reject if terminal already open) |
| GET | `/sessions/current?terminalNo` | the live session |
| GET | `/sessions/{no}/summary` | expected tenders by method, invoice count, sales/returns |
| POST | `/sessions/{no}/close` | counted cash â†’ variance â†’ (approval if over tolerance) â†’ deposit voucher â†’ `status=Closed` |

Rules: session_id from docâ€‘sequence; close requires `can_approve` when |variance| > tolerance; emit `SessionClosed`.

### Frontend â€” `sal/forms/sal1003`
Open card (terminal, opening float). Closing screen: summary by tender + invoice count; cashâ€‘count entry (optional denomination breakdown); **variance highlight**; manager signâ€‘off block (PIN reâ€‘auth) when over tolerance; Zâ€‘report print.

---

## 4. SAL_1001 â€” POS Sales Screen  *(reference; performanceâ€‘critical)*

### Business
The fast walkâ€‘in sell motion (and the same `sal_invoice` model serves credit invoicing). Cart â†’ customer
(default walkâ€‘in) â†’ discounts/promotions â†’ split tender until paid â‰¥ total (or due for credit) â†’ confirm:
stock relieved (reserveâ†’consume) via the engine, AR/cash + revenue + VAT posted, receipt printed. A POS
sale must belong to an **open session**. Budget < 300ms p95 (sal-db.md Â§8). RBAC: Cashier; Manager
override (discount over cap, below min price, over credit limit, void).

### Middleware â€” `/api/v1/sal/forms/sal1001`
Tables: `sal_invoice(_dtl)`, `sal_invoice_payment`, `inv_reservation` (hold). Uses `SalPricingService` +
`SalSalePostingService`. DTO: `Sal1001SaleDto` (+ line + payment DTOs, sal-db.md Â§5.2); `status` and all money serverâ€‘controlled.

| Method | Path | Purpose |
|---|---|---|
| POST | `/sales/quote-price` | serverâ€‘price a cart (returns priced lines + totals) |
| POST | `/sales` | confirm/post (body carries `client_uuid`; idempotent) |
| POST | `/sales/hold` | park cart (SAL_1002): reserve stock, `status=Hold` |
| GET | `/sales/held` | hold tray |
| POST | `/sales/{no}/resume` | reâ€‘price + return held cart |
| POST | `/sales/{no}/void` | release reservations, `status=Cancelled` |
| GET | `/sales/{no}` | reprint |
| GET | `/barcode/{code}` | resolve `{product,variant,uom,pack_qty}` |

Rules: reâ€‘price serverâ€‘side on confirm (client values advisory); creditâ€‘limit/minâ€‘price/discountâ€‘cap â†’ `ValidationException` unless `can_approve`; `client_uuid` unique â†’ replay returns original; require open session for POS.

### Frontend â€” `sal/pages/pos` (fullâ€‘screen, signal store)
Two panes: left dense cart `app-common-table`; right totals + tender. Alwaysâ€‘autofocused barcode/search
input; customer selector (walkâ€‘in default); terminal/warehouse badge. **Signal store, not a FormGroup per
line**, for speed; `computed` subtotals/tax/total/change. Tender modal (keypad, method buttons
Cash/Card/bKash/Nagad/Bank/Credit, split rows). Hold/Resume â†’ hold tray. 80mm thermal receipt autoâ€‘print
on confirm (logo, tax breakdown, tenders, change, QR of invoice_id). Keyboard map `F1` search Â· `F2` qty Â·
`F3` customer Â· `F4` discount Â· `F8` hold Â· `F9` tender Â· `F10` confirm Â· `Del` remove. Manager PIN reâ€‘auth
for overrides. **Offlineâ€‘safe** (sal-db.md Â§5.5): IndexedDB catalog/price snapshot, local pricing, queued
submit by `client_uuid`, offline banner. Disable confirm on submit + idempotency key to block doubleâ€‘post.

---

## 5. SAL_1002 â€” Draft / Hold Sales

### Business
Park an inâ€‘progress cart so a cashier can serve the next customer: creates `inv_reservation` rows
(`qty_reserved`â†‘ with `expires_at`) â€” **no ledger, no GL** â€” then resume or void. A scheduled job releases
expired holds (so a held cart never strands stock forever).

### Middleware
Served by the SAL_1001 endpoints (`/sales/hold`, `/sales/held`, `/sales/{no}/resume`, `/sales/{no}/void`).
Hold writes reservations; resume reâ€‘prices; void releases them. No separate controller required.

### Frontend â€” hold tray (panel in POS, or `sal/forms/sal1002` list)
Chips/cards per held cart (customer, amount, time, line count) with Resume / Void. Lives as a slideâ€‘in tray inside the POS screen; a standalone list route mirrors it for managers.

---

## 6. SAL_1102 â€” Customer Due Collection

### Business
Money received against AR, allocated to invoices (explicit `sal_receipt_alloc` or autoâ€‘FIFO oldestâ€‘first);
leftover â†’ `unallocated_amount` (advance/onâ€‘account). Each allocation: AR ledger **credit**, reduce
`sal_invoice.due_amount`, bump `payment_status`; GL Dr Cash/Bank, Cr AR. Cancel posts reversing ledger +
voucher and reâ€‘opens dues. RBAC: Cashier/CS Â· Manager (cancel).

### Middleware â€” `/api/v1/sal/forms/sal1102`
Tables: `sal_receipt(+_alloc)`. Uses `SalArLedgerService`. DTOs: `Sal1102ReceiptDto` + `Sal1102AllocDto`, `Sal1102OpenInvoiceRow`.

| Method | Path | Purpose |
|---|---|---|
| GET | `/customers/{no}/open-invoices` | due invoices (amount, age) |
| POST | `/receipts` | post receipt + allocations (sum allocations â‰¤ amount) |
| POST | `/receipts/auto-allocate` | FIFO allocate a given amount â†’ preview |
| POST | `/receipts/{no}/cancel` | reverse (Manager) |

Rules: `amount > 0`; allocation per invoice â‰¤ its due; receipt_id from docâ€‘sequence (SAL_RCPT); period guard.

### Frontend â€” `sal/forms/sal1102`
Customer select â†’ openâ€‘invoices grid (due, age `aging-badge`) with allocate inputs + **Autoâ€‘allocate (FIFO)**
button; receipt header (method/amount/ref/bank/cheque). Post â†’ toast + refreshed dues + moneyâ€‘receipt print.

---

## 7. SAL_1103 â€” Sales Return / Refund

### Business
Return against an original invoice (or **blind**, always `can_approve`). `Draft â†’ Approved(Posted) â†’
Cancelled`. Validate `qty â‰¤ original_qty âˆ’ already_returned`; refund basis = **net price actually paid**
(postâ€‘discount/promo), not list; restock movement 5 (IN) to the **same batch/serial** (or to Damage
warehouse when `restock_flag=0`); reverse revenue/VAT/COGS proportionally; update
`sal_invoice.returned_amount`/line `returned_qty`/status â†’ PartiallyReturned/Returned; AR credit if applied
to due. RBAC: CS create Â· **Manager post/blind**.

### Middleware â€” `/api/v1/sal/forms/sal1103`
Tables: `sal_return(_dtl)`. Uses engine (movement 5) + `SalArLedgerService` + GL reversal. DTOs: `Sal1103ReturnDto` + line; `Sal1103ReturnableRow`.

| Method | Path | Guard |
|---|---|---|
| GET | `/returns`, `/returns/{no}` | can_view |
| GET | `/invoices/{invoiceNo}/returnable` | returnable lines (qty cap, net price) |
| POST | `/returns` | draft upsert â€” can_insert |
| POST | `/returns/{no}/approve` | post â†’ engine + GL â€” **can_approve** |
| POST | `/returns/{no}/cancel` | reverse â€” can_approve |

Rules: blind return (no `original_invoice_no`) forces `can_approve` + fraud flag; restock to same batch; `client_uuid` idempotency; period guard.

### Frontend â€” `sal/forms/sal1103`
Scan/enter original invoice â†’ load lines with returnable qty; per line pick qty + reason + restock/scrap;
refundâ€‘method select; totals computed serverâ€‘side; approval gate (PIN) for blind/over. Printable credit note.

---

## 8. Crossâ€‘cutting: documentâ€‘form wiring checklist
For each posted document form (SAL_1001/1102/1103, and SAL_1003 close):
1. Entities + repositories (header/detail cascade; `client_uuid` unique where applicable).
2. DTOs (`@Valid @NotEmpty` lines; serverâ€‘controlled `status`/money).
3. `SalXXXXService`: draft upsert â†’ action methods that call `SalSalePostingService`/`SalArLedgerService` + `InvStockPostingService`; docâ€‘sequence; period guard; `client_uuid` idempotency; manual toDto.
4. `SalXXXXController` at `/api/v1/sal/forms/salXXXX`; `ApiResponse<T>`.
5. Frontend `data.service`/`model.service`/`component` + route in `sal.routes.ts`.
6. DB seed: `sys_menu` row (`form_id=SAL_xxxx`, `route_path`) + `sys_doc_sequence` + `fin_account` map (revenue/COGS/AR/VAT/cash) + `sys_enroll_menu`.
7. Tests: pricing/promo/tax math, state machine, atomic post + rollback, idempotent replay, AR invariant `current_due == last balance_after`.

Masters (B, SAL_1101/1104) skip the posting/sequence steps.

---

## 9. Suggested implementation waves
- **Wave I (masters):** Customer Group, POS Terminal, SAL_1101 Customer, SAL_1104 Promotions â€” CRUD; verify compile + tsc.
- **Wave II (sellâ€‘side core):** `SalPricingService` + `SalPromotionEvaluator` + `SalArLedgerService` + `SalSalePostingService` (unitâ€‘tested hard, no UI).
- **Wave III (POS):** SAL_1003 Session, SAL_1001 POS + SAL_1002 Hold.
- **Wave IV (postâ€‘sale):** SAL_1103 Return, SAL_1102 Due Collection.
- **Wave V:** reports/MVs (`mv_sal_daily_summary`, `mv_sal_customer_aging`), loyalty (optional), offline sync hardening.

Each wave verified with `./mvnw -o -q compile` and `tsc --noEmit -p apps/web-client/tsconfig.app.json`.

