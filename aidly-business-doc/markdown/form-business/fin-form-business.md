# FIN Form Business

| Form | Name | How It Works |
| --- | --- | --- |
| FIN_1001 | Chart of Accounts | Maintains account tree, account code/name/type/control behavior, and active status. |
| FIN_1002 | Account Group | Maintains grouping metadata for accounts and reports. |
| FIN_1003 | Voucher Type | Maintains voucher/document type rules and default debit/credit accounts. |
| FIN_1004 | Opening Balance | Records opening balances by account/party/cost center and posts verified opening ledger entries. |
| FIN_1005 | Bank Account | Maintains bank/cash account setup, linked GL account, branch/company scope, and active status. |
| FIN_1006 | GL Mapping | Maps event type and leg key to GL account for automatic postings from INV, PUR, SAL, and HRM. |
| FIN_1101 | Voucher Entry | Manual/system voucher entry with draft/post/cancel state and immutable ledger write on post. |
| FIN_1102 | Bank Reconciliation | Reconciles bank ledger against bank statement/reference data. |
| FIN_1201 | Auto-Posting Monitor | Monitors `sys_event_outbox` posting events, failures, retries, and unmapped GL legs. |
| FIN_1202 | AP Ledger | Supplier payable/sub-ledger reader, tied to PUR operational ledger and FIN party-tagged GL. |
| FIN_1203 | AR Ledger | Customer receivable/sub-ledger reader, tied to SAL operational ledger and FIN party-tagged GL. |
| FIN_1301 | Trial Balance | Reads ledger balances by account and period. |
| FIN_1302 | Account Ledger | Account-wise transaction ledger with filters and drilldown. |
| FIN_1303 | Day Book | Day-wise voucher/ledger activity report. |
| FIN_1304 | Profit & Loss | Income statement generated from ledger account classifications. |
| FIN_1305 | Balance Sheet | Asset/liability/equity statement generated from ledger balances. |
| FIN_1306 | Cash Flow | Cash movement report from ledger cash/bank activity. |
| FIN_1307 | AR / AP Aging | Party aging report from party-tagged ledger entries. |
| FIN_1401 | Period / Year-End Close | Closes periods/years, blocks posting into closed periods, and performs year-end closing behavior. |

## Form Business Guide

This section explains why each finance form exists, when users should use it, and what happens in the business if the form is not configured correctly.

For user training and the full recommended operating sequence, read `fin-user-manual.md`.

| Form | Purpose | When To Use | If This Is Missing / Wrong |
| --- | --- | --- | --- |
| FIN_1001 Chart of Accounts | Creates the GL accounts used by every finance transaction and report. It defines whether an account is a normal account or a special control account like AR, AP, Bank, Cash, Inventory, Tax, or Retained Earnings. | Use it before voucher entry, bank setup, GL mapping, opening balance, auto-posting, and reports. Create one account for each ledger balance the company needs to track. | Vouchers and auto-posting cannot post to the right accounts. Reports like Trial Balance, Balance Sheet, Profit & Loss, Cash Flow, and Aging will be incomplete or wrong. |
| FIN_1002 Account Group | Builds the chart of accounts structure. It tells the system whether accounts belong to Asset, Liability, Equity, Income, or Expense. | Use it before creating accounts. Create groups like Current Assets, Fixed Assets, Current Liabilities, Sales Income, Direct Expense, and Admin Expense. | Accounts will not have the correct financial statement classification. Balance Sheet and Profit & Loss may place balances in the wrong area. |
| FIN_1003 Voucher Type | Defines the document types used for accounting entries, such as Journal, Payment, Receipt, Contra, Sales, Purchase, Opening, and Closing. | Use it before voucher entry and system posting. Create voucher types that match the company's document process. | Users cannot create the correct voucher documents, numbering becomes unclear, and system postings may not know which voucher type to use. |
| FIN_1004 Opening Balance | Enters starting ledger balances when the company begins using the system or starts a new implementation. | Use it once before normal transaction posting, or when migrating from another system. Enter opening debit/credit balances for each account. | Reports start from zero or wrong balances. Trial Balance, Balance Sheet, AR/AP, and Bank balances will not match real business records. |
| FIN_1005 Bank Account | Connects real bank/cash accounts with GL Bank/Cash control accounts. It stores bank details and supports reconciliation/cash flow. | Use it before bank reconciliation, payments, receipts, or cash flow reporting. Create one setup per bank/cash account. | Bank reconciliation cannot work properly and cash flow may not identify bank/cash movement correctly. |
| FIN_1006 GL Mapping | Tells the auto-posting engine which GL account to use for system events from HRM, INV, PUR, SAL, and other modules. | Use it before relying on automatic postings like payroll posting, stock adjustment, opening stock, purchase invoice, or sales invoice. | Auto-posting events fail or stay in the monitor because the system cannot decide the debit/credit accounts. |
| FIN_1101 Voucher Entry | Records manual accounting vouchers and controls submit, approve, post, cancel, and ledger update behavior. | Use it for journal adjustments, manual payments/receipts, corrections, and finance entries that do not come automatically from another module. | Manual finance transactions will not reach the ledger. Reports will miss those business events. |
| FIN_1102 Bank Reconciliation | Matches bank/cash ledger entries against the real bank statement and marks cleared/outstanding items. | Use it monthly or whenever bank statements are reviewed. Select bank account, statement date, statement balance, and cleared lines. | Book balance and bank statement balance may drift. Outstanding cheques, deposits, and missing bank entries become hard to identify. |
| FIN_1201 Auto-Posting Monitor | Shows system posting events, failed events, pending events, and allows retry after configuration is fixed. | Use it when automatic posting does not appear in the ledger, or after configuring GL mappings. Finance/admin users can drain or redrive events. | Failed system events stay invisible to finance users and module transactions may not reach GL. |
| FIN_1202 AP Ledger | Reads supplier payable movement and outstanding payable from party-tagged GL/AP entries. | Use it to review supplier-wise payable once purchase/payment flows are active. | Finance cannot easily compare supplier payable with GL control account balance. |
| FIN_1203 AR Ledger | Reads customer receivable movement and outstanding receivable from party-tagged GL/AR entries. | Use it to review customer-wise due once sales/receipt flows are active. | Finance cannot easily compare customer receivable with GL control account balance. |
| FIN_1301 Trial Balance | Shows debit/credit balance of all accounts as of a date. It is the main check that ledger postings are balanced. | Use it during period review, before financial statements, and after posting batches. | Finance cannot confirm whether the ledger is balanced or which accounts hold balances. |
| FIN_1302 Account Ledger | Shows detailed transaction movement for one account with opening, running, and closing balance. | Use it when investigating an account balance, checking vouchers, or reviewing audit details. | Users only see totals, not the transactions that created the balance. |
| FIN_1303 Day Book | Shows day-wise voucher/ledger activity for a selected date range. | Use it for daily transaction review and audit checking. | Finance cannot quickly review what was posted on a day or period. |
| FIN_1304 Profit & Loss | Calculates income, expense, and net profit/loss for a period. | Use it monthly, quarterly, yearly, or for management reporting. | Management cannot see period profit/loss from the ledger. |
| FIN_1305 Balance Sheet | Shows assets, liabilities, equity, and current earnings as of a date. | Use it at month-end/year-end or whenever financial position is needed. | The company cannot see its financial position from the system. |
| FIN_1306 Cash Flow | Shows cash/bank inflow and outflow for a period based on Bank/Cash control accounts. | Use it to understand cash movement, receipts, payments, and closing cash position. | Finance may know profit but not actual cash movement. |
| FIN_1307 AR / AP Aging | Buckets customer receivable or supplier payable by age. It depends on party-tagged AR/AP postings. | Use it for collection follow-up, supplier payment planning, and overdue tracking. | Outstanding balances cannot be aged by party, so collection/payment follow-up becomes weak. |
| FIN_1401 Period / Year-End Close | Controls open/closed/locked periods and performs year-end closing to retained earnings. | Use it after period review is complete, before locking old periods, and at year-end after final statements are ready. | Users may keep posting into old periods, reports may keep changing, and year-end profit/loss may not transfer to retained earnings. |

## Field-Wise Business Guide

Use this section when building or reviewing the front end. Every finance form should explain fields in a simple business way: "if I select this, this will happen" and "if I do not select this, this will not happen".

Finance Dr/Cr selector fields must use text values `DR` and `CR`, not numeric `1` and `2`. Numeric debit and credit amount columns remain numeric money columns.

### FIN_1001 Chart of Accounts

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Account Code | This becomes the short business code used in lookups, vouchers, reports, and integrations. Example: `1010-001` for Cash in Hand. | The account cannot be saved because users and postings need a stable code. |
| Account Name | This is the readable account name shown in voucher entry, ledger, trial balance, and reports. | The account cannot be saved because reports would not have a meaningful label. |
| Account Group | The account inherits its root type and normal balance from the group, such as Asset/Debit or Income/Credit. Reports also use this grouping. | The account cannot be saved because the system will not know where it belongs in the chart of accounts. |
| Control Type | Marks a special account used by system logic. AR tracks customer receivable, AP tracks supplier payable, Bank/Cash works in bank forms and cash flow, Inventory works for stock posting, Tax works for tax posting, Retained Earnings works in year-end close. | The account remains a normal/plain GL account. It can still be used in vouchers, but special forms and automatic posting will not treat it as AR, AP, Bank, Cash, Inventory, Tax, or Retained Earnings. |
| Postable | If checked, users and auto-posting can post debit/credit lines directly to this account. | If unchecked, the account is only a summary/header account. Users cannot post vouchers to it directly. |
| Cost Center Required | If checked, every voucher line for this account must include a cost center. This is useful for expenses or income that must be tracked by department/project/location. | Voucher lines can be posted without a cost center, so cost center reports will not split this account's amount. |
| Party Required | If checked, every voucher line for this account must include a party, such as Customer, Supplier, or Employee. This is required for AR/AP style accounts so aging and party ledgers work. | The voucher can be posted without party information. Then the balance stays only in the GL account and will not appear correctly in customer/supplier/employee aging or party-wise outstanding reports. |
| Opening Balance | Adds the starting balance for this account before regular transactions begin. | The account starts from zero unless opening is entered later through FIN_1004 Opening Balance. |
| Opening Dr/Cr | Tells whether the opening balance is debit or credit. Assets/expenses usually open as Debit; liabilities/equity/income usually open as Credit. | The system cannot correctly understand the side of the opening balance. |
| Active | If active, the account is available for selection and posting. | If inactive, users should not use it for new postings, but old transactions and reports still keep history. |

#### Party Checkbox Example

If you create **Accounts Receivable** and check **Party Required**, every posting must say which customer owes the money. Then AR Ledger and Aging can show customer-wise due.

If you do not check **Party Required**, the same receivable amount can be posted only to the GL account. The total balance may be correct, but the system cannot answer "which customer owes this amount".

For **Accounts Payable**, checking Party Required means every payable posting must say which supplier is owed money. If it is not checked, AP total may exist but supplier-wise payable and aging will be incomplete.

#### Account Group Lookup Rule

FIN_1001 must use its own form-specific account group lookup (`/api/v1/fin/forms/fin1001/account-groups`) for the Account Group selector. This lookup returns only selectable child groups and displays them as `Group Name(Parent Group Name)`, such as `Current Assets(Assets)`. FIN_1002 remains the master Account Group API and must return the full raw tree, including root groups, without changing `group_name` for another form's display need.

### FIN_1002 Account Group

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Group ID | Creates a short unique code for the group, such as `AST-CA`. | The group cannot be saved because it needs a stable identifier. |
| Group Name | Shows the group name in the chart of accounts tree and reports. | The group cannot be saved because users cannot identify it. |
| Root Type | Decides whether this group belongs to Asset, Liability, Equity, Income, or Expense. Reports like Balance Sheet and Profit & Loss depend on it. | The group cannot be saved because financial statements will not know where to place it. |
| Parent Group | Places this group under another group in the COA tree. | The group becomes a top-level group under its root type. |
| Normal Balance | Sets the usual Dr/Cr side for accounts under this group. Normally it is derived from Root Type. | The system uses the root type default where possible. |
| Order | Controls display order in lists and reports. | The group still works, but ordering may follow default/system order. |

### FIN_1003 Voucher Type

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Voucher Type ID | Creates the short code for the voucher type, such as `JV`, `PV`, or `RV`. | The voucher type cannot be saved. |
| Type Name | Shows the readable name users select in voucher entry. | The voucher type cannot be saved. |
| Base Kind | Decides the business behavior: Journal, Payment, Receipt, Contra, Sales, Purchase, Opening, or Closing. | The voucher type cannot be saved because posting workflow needs a base kind. |
| Number Prefix | Prefixes voucher numbers, such as `JV000001`. | The system may still create numbers, but users lose a clear document identity by type. |
| Order | Controls display order in voucher type lists. | The type still works, but list order may be default. |
| Active | If active, users can create vouchers with this type. | If inactive, it should not be used for new vouchers, but old vouchers remain valid. |

### FIN_1004 Opening Balance

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Voucher Date | Sets the date for the opening balance posting. The date must be in an open period. | Opening cannot be posted safely because the ledger needs a posting date. |
| Currency | Sets the currency users enter opening amounts in. The form still converts and posts ledger values in company base currency. | The form cannot know how to display or convert the entered opening amounts. |
| Exchange Rate | Converts the selected currency amount into company base currency before posting. Base currency uses rate `1`. | Foreign-currency opening amounts cannot be posted because the ledger stores base currency. |
| Narration | Adds a note to explain the opening entry. | Posting still works, but future review will have less context. |
| Type | Chooses whether the line is Debit or Credit. | The form cannot place the opening amount on the correct accounting side. |
| Account | Selects which GL account gets an opening amount. | The line cannot be posted because every opening line must belong to an account. |
| Amount | Enters the opening amount in the selected currency. The form calculates base amount, base debit, and base credit. | No amount is posted for that line. |
| Difference | Shows total base debit minus total base credit in real time. Posting is allowed only when this is zero. | Users may try to post an unbalanced opening batch, which the UI and API block. |

### FIN_1005 Bank Account

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Bank Account ID | Creates the business code for this bank/cash setup. | The bank account cannot be saved. |
| Linked GL Account | Connects this bank setup to a GL account with Bank or Cash control type. Reconciliation and cash flow use this link. | The bank account cannot be saved because the system will not know which ledger account it represents. |
| Opening Balance | Stores the starting bank/cash balance for this setup. | The account starts from zero in this setup unless opening is posted elsewhere. |
| Bank Name / Branch Name | Records bank identity and branch. | The setup still works, but bank details are incomplete. |
| Account Title / Account Number | Records actual bank account information. | The setup still works, but payment/reconciliation users have less reference information. |
| Routing Number / Swift Code | Helps with bank transfer or external bank reference. | Normal internal accounting still works, but bank transfer details are incomplete. |
| Active | If active, the bank/cash account can be used. | If inactive, it should not be used for new bank work. |

### FIN_1006 GL Mapping

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Event Type | Names the source event, such as `PayrollPosted`, `StockAdjustmentPosted`, or `OpeningStockPosted`. | Auto-posting cannot match the event to a GL account. The event will fail or wait in the posting monitor. |
| Leg Key | Names the accounting leg, such as `EXPENSE`, `PAYABLE`, `INVENTORY`, or `OPENING_EQUITY`. | Auto-posting cannot decide which account this leg should use. |
| Sub Key | Adds a more specific mapping when one event/leg needs different accounts by type. | The mapping works as a general/default mapping for that event and leg. |
| Account | The selected GL account receives that automatic posting leg. | Auto-posting cannot create a balanced voucher for that leg. |
| Active | If active, the mapping is available to the posting engine. | If inactive, the mapping is ignored and matching events may fail. |

### FIN_1101 Voucher Entry

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Voucher Type | Decides the voucher behavior and number prefix. | The voucher cannot be saved. |
| Voucher Date | Posts the voucher into that accounting period. The period must be open. | The voucher cannot be posted because the ledger needs a date. |
| Reference No | Stores cheque number, bill number, or outside reference. | Voucher still works, but external tracking is weaker. |
| Narration | Explains why the voucher exists. | Voucher still works, but audit review is harder. |
| Line Account | Selects the postable GL account for each debit/credit line. | The line cannot be added. |
| Debit / Credit | Enters the accounting amount. Total debit must equal total credit before posting. | A line with no amount is invalid. If total debit and credit do not match, posting is blocked. |
| Line Narration | Explains that specific line. | Line still works, but detailed explanation is missing. |
| Party | Required when the selected account has Party Required. | If the account requires party, posting is blocked. If not required, voucher can post without party tracking. |
| Cost Center | Required when the selected account has Cost Center Required. | If the account requires cost center, posting is blocked. If not required, voucher can post without cost center split. |

### FIN_1102 Bank Reconciliation

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Bank Account | Loads uncleared ledger lines for that bank/cash GL account. | The worksheet cannot load. |
| Statement Date | Reconciles transactions up to that bank statement date. | The worksheet cannot know the cutoff date. |
| Statement Balance | Compares bank statement balance with book balance. | The system cannot calculate reconciliation difference. |
| Cleared Lines | Marking a line means it appeared in the bank statement. | Unchecked lines remain outstanding/uncleared. |
| Narration | Adds a note for this reconciliation. | Save still works, but review context is weaker. |

### FIN_1201 Auto-Posting Monitor

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Status Filter | Shows waiting, posted, failed, or selected event status. | The monitor shows the default/all relevant event list. |
| Drain | Runs the posting worker to process waiting events. | Waiting events remain in queue until scheduler or another drain runs. |
| Redrive | Tries a failed event again after mapping/config is fixed. | The failed event stays failed and no GL voucher is created from it. |

### FIN_1301 Trial Balance

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| As Of Date | Shows account balances up to that date. | The report uses its default date behavior and may not match the period you want. |

### FIN_1302 Account Ledger

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Account | Shows only the ledger movement for that account. | The report cannot load account-wise ledger detail. |
| From Date / To Date | Limits movement to the selected date range and calculates opening/running balance. | The report may use default dates or return a wider/narrower period than intended. |

### FIN_1303 Day Book

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| From Date / To Date | Shows vouchers and ledger movement for that day/range. | The report may use default dates and may not match the desired day. |

### FIN_1304 Profit & Loss

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| From Date / To Date | Calculates income, expense, and profit/loss for that period only. | The report may use default range and profit/loss may not match the intended period. |

### FIN_1305 Balance Sheet

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| As Of Date | Shows asset, liability, equity, and current earning balance up to that date. | The report may use default date and not match the requested statement date. |

### FIN_1306 Cash Flow

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| From Date / To Date | Shows cash/bank receipts and payments for the selected period. | The report may use default dates and cash movement may not match the desired period. |

### FIN_1307 AR / AP Aging

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Party Type | Customer shows AR aging; Supplier shows AP aging. | The report cannot know whether to show receivable or payable aging. |
| As Of Date | Buckets outstanding balances by age as of that date. | Aging may use default date and may not match the review date. |

### FIN_1401 Period / Year-End Close

| Field | If I select / enter this | If I do not select / check this |
| --- | --- | --- |
| Financial Year | Loads periods for that year and controls close actions for that year. | Period close and year-end close cannot run. |
| Period Status | Open allows posting, Closed blocks normal posting, Locked blocks final changes. | The period remains in its current status. |
| Retained Earnings Account | Receives the year-end profit/loss transfer during close. | Year-end close cannot post the closing entry safely. |
| Close Year | Posts closing entry and locks the year/periods. | The year remains open and users may continue posting if periods are open. |

## Build Progress and Rules

---
name: fin-build-progress
description: "FIN (Finance/GL) module BUILD progress â€” what's coded vs. remaining"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9cca2c5d-341a-4a5a-8fbf-926297681c99
---

Building the FIN module from `aidly-business-doc/fin-db.md`. Backend pkg `com.infoaidtech.aidly.fin`. FIN entities extend `AuditEntity` + declare own `company_no` (NOT NULL) + `branch_no` (nullable for masters) â€” like `InvWarehouse`, NOT `BaseEntity`. **Company-scoped isolation enforced explicitly** (repos filter `company_no`; `CompanyBranchContext.getCompanyNo()` in services) since FIN extends AuditEntity (no auto tenant @Filter). PKs are IDENTITY for now (blueprint suggests SEQUENCE allocationSizeâ‰¥50 for `fin_ledger`/`fin_voucher_dtl` later).

## DONE (backend, compiles + AidlyApplicationTests BUILD SUCCESS)
- **9 entities** (full data model): `FinAccountGroup`, `FinAccount`, `FinVoucherType`, `FinVoucher`, `FinVoucherDtl`, `FinLedger` (append-only), `FinAccountBalance`, `FinGlMap`, `FinBankAccount`.
- **9 repositories** (company-scoped finders + delete-guard `existsByAccountNoAndIsDeleted` on ledger/voucherDtl).
- **FIN_1002 Account Group** (`/api/v1/fin/forms/fin1002/groups`) â€” COA tree; normal_balance auto-derives from root_type (Asset/Expense=Dr, Liab/Equity/Income=Cr); delete blocked if child groups or accounts reference it.
- **FIN_1001 Chart of Accounts** (`/fin1001/accounts`) â€” root_type/normal_balance denormalized from group; control_type 1-7; AR/AP control auto-forces requires_party; opening_balance locked once posted; delete blocked if used in ledger/voucher.
- **FIN_1003 Voucher Type** (`/fin1003/voucher-types`) â€” base_kind 1-8 drives the single voucher form; system types undeletable.
- **FIN_1101 Voucher Entry â€” THE VOUCHER ENGINE DONE** (`/fin1101/vouchers` + /submit /approve /reject /cancel). `Fin1101Service` DOC_TYPE=`FIN_VOUCHER`. Header+lines (replace-lines on edit); validateLines (one of dr/cr>0, postable acct, requires_party/cost_center enforced, Î£dr=Î£cr within 0.0001); period guard via sys `FinYear`/`FinYearDtl` (branch-scoped! period_status==1=Open required to post); submitâ†’approvalService.raiseâ†’auto post OR pending; `applyApprovalOutcome`â†’post/clear; **post writes immutable `FinLedger` + upserts `FinAccountBalance`**; cancel of Posted writes reversing ledger (is_reversal=1) + undoes balances; delete Draft-only. voucher_id = prefix+`%06d`(voucherNo) [TODO: per-type sys_doc_sequence for gap-free]. `FinApprovalListener` wired (case FIN_VOUCHER). Verified: AidlyApplicationTests BUILD SUCCESS.
- NOTE: `FinYear`/`FinYearDtl` are **branch-scoped** (branch_no NOT NULL, NO company_no) â€” period resolved by voucher's branch_no via `findByBranchNoAndIsDeletedAndStartDateLessThanEqualAndEndDateGreaterThanEqual(branch, 0, date, date)`.

- **FIN_1006 GL Map** (`/fin1006/maps`), **FIN_1005 Bank Account** (`/fin1005/bank-accounts`, links control_type 3/4 GL acct) â€” setup masters DONE.
- **Reports DONE** (read-only over `fin_ledger`, the source of truth) â€” `FinReportService` + 3 controllers:
  - **FIN_1301 Trial Balance** (`/fin1301/trial-balance?asOf=`) â€” JPQL group-by per account, netâ†’Dr/Cr column.
  - **FIN_1302 Account Ledger** (`/fin1302/ledger?accountNo=&from=&to=`) â€” opening + running balance + rows.
  - **FIN_1303 Day Book** (`/fin1303/day-book?from=&to=`).
  - All verified: AidlyApplicationTests BUILD SUCCESS (JPQL projections DrCr/AccountTotal validate).
- **GL CORE IS USABLE END-TO-END on backend**: define accounts (FIN_1001/1002) â†’ voucher types (FIN_1003) â†’ post vouchers (FIN_1101) â†’ see Trial Balance / Ledger / Day Book.

## FRONTEND DONE (all 9 touched forms, nx build SUCCESS)
- Scaffolding: `features/fin/services/data.service.ts` (FinDataService, modulePath='fin'), `features/fin/constants/fin.constants.ts` (rootTypes/drCr/controlTypes/voucherBaseKinds/voucherStatuses/partyTypes â€” DB-first numeric), `features/fin/fin.routes.ts` (9 routes), app.routes mount `path:'fin'` already wired.
- Setup masters (master-detail): FIN_1001 `form/chart-of-accounts`, FIN_1002 `form/account-group`, FIN_1003 `form/voucher-type`, FIN_1005 `form/bank-account`, FIN_1006 `form/gl-mapping`.
- FIN_1101 `form/voucher-entry` â€” master + editable working-line set (add-line row + common-table w/ delete action + live Î£dr/Î£cr balanced indicator) + workflow (submit/approve/reject/cancel).
- Reports (no-sidebar + common-table): FIN_1301 `report/trial-balance` (asOf + balanced check), FIN_1302 `report/account-ledger` (account+date range, opening/closing), FIN_1303 `report/day-book`.
- Lookups via cross-form reads (accounts from forms/fin1001, voucher-types from forms/fin1003) â€” matches existing HRM practice.

## DB MIGRATION DONE
`sme-software-backend/db-migration/fin_schema_postgres.sql` â€” 9 `fin_*` tables + 20 indexes, pretty-printed, IF NOT EXISTS, BEGIN/COMMIT (generated via DdlGenTest â†’ extract, same as inv/hrm scripts). Run in pgAdmin.

## POSTING ENGINE DONE (the outbox consumer â€” verified compile + AidlyApplicationTests BUILD SUCCESS + nx build)
- **Contract**: `GlPostingPayload` (voucherDate, narration, branchNo, legs[{legKey, subKey, amount, drCr, costCenterNo, partyType, partyNo}]) â€” standard JSON envelope any emitter produces (decoupled; emitters build raw JSON, no FIN import).
- **`FinPostingService`** (`@Scheduled` every 30s via `@EnableScheduling` on AidlyApplication): drains `sys_event_outbox` (status=1) â†’ resolves each leg's account via `fin_gl_map(company,eventType,legKey,subKey)` â†’ `Fin1101Service.postSystemVoucher(...)` (reuses validate+period-guard+postâ†’ledger+balance). Per-event REQUIRES_NEW tx via TransactionTemplate; **idempotent** on (source_doc_type=eventType, source_doc_no=aggregate_id); unmapped leg/missing-config â†’ **parks** event as Failed (status=3), never blocks queue. Uses a self-built ObjectMapper (app has NO ObjectMapper bean â€” important gotcha).
- `Fin1101Service` added: `postSystemVoucher`, `resolveSystemJournalType` (first base_kind=1 type), `alreadyPosted`.
- **`EventOutboxRepository`** (sys) â€” drain finders (Limit-based).
- **FIN_1201 Auto-Posting Monitor** â€” backend (`/fin1201/events?status`, `/drain`, `/events/{no}/redrive`) + frontend (`form/posting-monitor`, status filter + Drain + re-drive row action).
- **Real emitter wired**: `Hrm1202Service.emitPayrollPosted(run)` on payroll approval â†’ outbox `PayrollPosted` (legs EXPENSE=gross Dr / PAYABLE=net Cr / STATUTORY=deduction Cr). Loop is real: payroll approve â†’ outbox â†’ FinPostingService â†’ balanced GL voucher in ledger. Needs FIN_1006 maps: PayrollPosted/EXPENSE, /PAYABLE, /STATUTORY.
- DB migration `fin_schema_postgres.sql` now also includes `sys_event_outbox` (prerequisite, IF NOT EXISTS). 10 tables total.

## FINANCIAL STATEMENTS DONE (FIN_1304 P&L + FIN_1305 Balance Sheet â€” compile + AidlyApplicationTests BUILD SUCCESS + nx build)
- `FinReportService.profitAndLoss(from,to)` â€” uses `ledgerRepository.totalsBetween(company,from,to)`; root_type 4 Income (credit-debit), root_type 5 Expense (debit-credit); net_profit = income-expense. DTOs `Fin1304PnlDto` {from_date,to_date,income[],expense[],total_income,total_expense,net_profit} + `FinStatementRowDto` {account_no,account_code,account_name,amount}.
- `FinReportService.balanceSheet(asOf)` â€” uses `ledgerRepository.trialBalance(company,asOf)`; Asset(1) debit-credit, Liability(2)/Equity(3) credit-debit; current_earnings = Î£income-Î£expense folded into total_equity; balanced if |assets-(liab+equity)| < 0.005. DTO `Fin1305BalanceSheetDto`.
- Controllers: `Fin1304Controller` (`/api/v1/fin/forms/fin1304/profit-loss?from&to`), `Fin1305Controller` (`/fin1305/balance-sheet?asOf`).
- Repo: added `FinLedgerRepository.totalsBetween(companyNo,from,to)â†’List<AccountTotal>` (group-by within window).
- Frontend: FIN_1304 `report/profit-loss` (from/to + income|expense common-tables side-by-side + Net Profit/Loss summary), FIN_1305 `report/balance-sheet` (asOf + Assets | Liabilities+Equity columns + Current-Year-Earnings + Balanced indicator). Routes added to fin.routes.ts. No new DB tables (reports over fin_ledger).

## MENU SEED + ENROLLMENT DONE
`db-migration/2026-06_fin_menu_seed.sql` â€” idempotent pgAdmin script: module FIN (order 40, icon account_balance) + 3 submodules (FIN-SETUP/FIN-TXN/FIN-REPORT) + 12 menus (FIN_1001/1002/1003/1005/1006 setup, FIN_1101/1201 txn, FIN_1301-1305 reports) with route_path `/fin/form/*` & `/fin/report/*` (matches fin.routes mounted at app.routes path:'fin'), + enroll ALL FIN menus for company 2 (branch_no NULL = all branches). Same pattern as `2026-06_hrm_inv_menu_seed.sql`. Run in pgAdmin so forms appear + RBAC resolves.

## INV EMITTER WIRED (compile + AidlyApplicationTests BUILD SUCCESS)
- **`Inv1102Service` (Stock Adjustment) now emits GL events** to `sys_event_outbox` (same decoupled raw-JSON pattern as Hrm1202): added `EventOutboxRepository` dep + static `OUTBOX_MAPPER`. `approve()` (post) â†’ `emitGlEvent(a,"StockAdjustmentPosted",false)`; `cancel()` (reverse) â†’ `emitGlEvent(a,"StockAdjustmentReversed",true)`. Net journal: net = total_in_value âˆ’ total_out_value; Dr INVENTORY / Cr ADJUSTMENT when stock rises (flipped when it falls); pure reclassification (net 0) emits nothing; `reverse=true` swaps Dr/Cr. aggregateType="INV_ADJ", aggregateId=adjustmentNo (numeric â€” required by FinPostingService.parseLong), eventType distinct for reversal so idempotency (eventType+aggregateId) doesn't dedupe the cancel.
- **`Inv1101Service` (Opening Stock) now emits** `OpeningStockPosted` on `post()` â€” Dr INVENTORY (total_in_value) / Cr OPENING_EQUITY; aggregateType="INV_OPENING", aggregateId=adjustmentNo. Same EventOutboxRepository+OUTBOX_MAPPER pattern.
- **Needs FIN_1006 maps** (else events park as Failed for FIN_1201 to re-drive): `StockAdjustmentPosted/INVENTORY`, `StockAdjustmentPosted/ADJUSTMENT`, `StockAdjustmentReversed/INVENTORY`, `StockAdjustmentReversed/ADJUSTMENT`, `OpeningStockPosted/INVENTORY`, `OpeningStockPosted/OPENING_EQUITY`.
- NOTE: PUR & SAL modules do NOT exist yet (only sys/hrm/inv/fin packages) â€” no purchase/sales emitters possible until those modules are built.

## REMAINING FORMS BUILT (all compile + AidlyApplicationTests BUILD SUCCESS + nx build SUCCESS)
- **FIN_1004 Opening Balance** (`/fin/form/opening-balance`) â€” `Fin1004Service.post()` builds per-account opening `dr`/`cr` lines, converts selected-currency entry amounts to base currency, requires total debit and total credit to match, and posts ONE balanced Opening voucher (base_kind 7) via `Fin1101Service.postSystemVoucher` (sourceDocType FIN_OPENING). Endpoints `/fin1004/accounts`, `/fin1004/post`. FE: compact master currency/exchange-rate header + inline-edit common-table detail + live entered/base totals/difference; Post stays disabled while difference is non-zero.
- **FIN_1306 Cash Flow** (`/fin/report/cash-flow`) â€” `FinReportService.cashFlow(from,to)`: opening (trialBalance up to from-1) + receipts(Dr)/payments(Cr) (totalsBetween) over control_type 3/4 accounts; per-account rows + totals. `/fin1306/cash-flow`.
- **FIN_1307 AR/AP Aging** (`/fin/report/aging`) â€” `FinReportService.aging(partyType,asOf)`: groups `FinLedgerRepository.findPartyLedger(company,accNos,asOf)` by party for control_type 1(AR)/2(AP), buckets by doc age 0-30/31-60/61-90/90+. `/fin1307/aging?partyType&asOf`.
- **FIN_1401 Period/Year-End Close** (`/fin/form/period-close`) â€” `Fin1401Service`: getYears/getPeriods (branch-scoped FinYear/FinYearDtl), setPeriodStatus (1 Open/2 Closed/3 Locked), yearEndClose â†’ posts Closing voucher (base_kind 8) zeroing income/expense into a retained-earnings account (totalsBetween over year start..end), then sets FinYear.isClosed=1 + all periods Locked; idempotent on (FIN_YEAR_CLOSE, finYearNo). FE: year select + periods common-table w/ status actions + RE-account + close button (RE accounts via cross-form read fin1004/accounts).
- **FIN_1102 Bank Reconciliation** (`/fin/form/bank-reconciliation`) â€” **NEW TABLES** `fin_bank_recon` + `fin_bank_recon_line` (entities extend AuditEntity, company_no NOT NULL, branch_no nullable, IDENTITY PK; repos `FinBankReconRepository`/`FinBankReconLineRepository` w/ `findClearedLedgerNos`). `Fin1102Service`: getBankAccounts (control_type 3), getWorksheet (book balance via openingBefore + uncleared ledger lines up to statement date), save (persist recon header difference=stmt-book + cleared FinBankReconLine rows; a ledger line clears at most once), getList/getDetail/delete. FE: account+date+balance â†’ worksheet common-table w/ toggle-cleared action + difference/cleared summary + save. **DDL added to `fin_schema_postgres.sql`** (2 tables + 4 indexes, audit-column style).

## DB MIGRATION + MENU SEED UPDATED
- `fin_schema_postgres.sql` now also has `fin_bank_recon` + `fin_bank_recon_line` (before COMMIT).
- `2026-06_fin_menu_seed.sql` now seeds 17 FIN menus (added FIN_1004, FIN_1102, FIN_1401, FIN_1306, FIN_1307); the company-2 enroll JOIN auto-covers them (idempotent). fin.constants.ts gained `periodStatuses`.

## REMAINING (roadmap)
- More emitters: PUR/SAL once those modules exist (HRM payroll + INV adjustment + INV opening wired so far).
- FIN_1202/1203 dedicated AR/AP sub-ledger forms (the aging report FIN_1307 already reads party outstanding) â€” best built alongside PUR/SAL party flows.
- **FIN MODULE IS FEATURE-COMPLETE for the current module set**: setup (COA/groups/types/bank/GL-map/opening) â†’ transactions (voucher entry, bank rec, auto-posting monitor, period/year close) â†’ reports (TB/ledger/daybook/P&L/BS/cash-flow/aging). 17 forms, backend+frontend+DDL+menu seed all built & verified.
- Menu seed + company enrollment for FIN_* forms (needed for forms to appear + RBAC).
- `FinPostingService`: drain `sys_event_outbox` (PayrollPosted/*InvoicePosted/*PaymentPosted/StockAdjustmentPosted) â†’ balanced voucher via `fin_gl_map`; idempotent on (source_doc_type, source_doc_no); emit `GlPosted`. FIN_1201 monitor. (Reuse Fin1101Service post path or a dedicated builder that creates a FinVoucher with source_module set + auto-posts.)
- FIN_1102 Bank Recon; FIN_1202/1203 AR/AP; FIN_1301-1307 reports (TB/GL/Daybook/P&L/BS/CashFlow/Aging â€” all GROUP BY fin_ledger); FIN_1401 period/year close.
- **Frontend** (`features/fin/...`) for every form; **app route** mount `path:'fin'`; **constants** `fin/constants/fin.constants.ts` (rootTypes, normalBalances, controlTypes, voucherBaseKinds, voucherStatuses â€” DB-first numeric).
- **DB**: fin_* tables don't exist yet (ddl-auto none) â€” generate via `DdlGenTest` â†’ extract `fin_*` CREATE TABLEs (same as the inv/hrm pgAdmin scripts). Then menu seed + enroll (FIN_* forms) like the hrm/inv menu seed.

