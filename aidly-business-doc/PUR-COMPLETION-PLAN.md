# PUR module — completion plan (2026-08-17)

Derived by enumerating the surface, not by running paths one at a time:
every emitted GL event x leg vs `fin_gl_map`; every stock-posting call site;
every service operation; every posting path vs its period guard; every document
table vs its GL traceability column.

## A. Correctness / accounting
| # | Defect | Evidence | Status |
|---|---|---|---|
| A1 | Invoice re-posts stock the GRN already received | ledger 26+27, both +6 for the same 6 units | code done |
| A2 | `PurchaseReturnPosted/Reversed.CASH` unmapped | gl-map coverage query | to do |
| A3 | Landed cost applies with no fin-period guard | `Pur1106Service` has 0 `PeriodStatus != 1` | to do |
| A4 | `gl_voucher_no` missing on receipt/return/landed_cost | information_schema | to do |
| A5 | `GlPosted` events keep failing | external instance 3.216.155.203 drains the outbox | needs owner decision |
| A6 | inv sub-ledger != GL 1104 | re-measure after A1 | to do |
| A7 | `stock_value != qty x avg_cost`; `qty_available > qty_on_hand` | inv_stock rows 1,2 | to do |

## B. Paths never exercised
B1 purchase return post+cancel · B2 landed cost apply · B3 every cancel/reversal
(PO, GRN, invoice, payment) · B4 PO->GRN receipt matching (`received_qty_base`)

## C. UI / standard
C1 first Save click swallowed while a grid cell is open ·
C2 forms hand-roll `col-md-*` instead of `app-form-section`/`app-form-field` ·
C3 breadcrumb reads `goodsReceiptNot`

## D. Data
D1 `GRN000011` saved with 0 lines · D2 historical double-counted stock

## Order
A1 -> A2 -> A3 -> A4 (all posting correctness) -> B1..B4 (prove every path) ->
A6/A7 (reconcile) -> C1 -> C3 -> D1 -> C2 (largest, purely cosmetic) -> A5/D2 (owner call)
