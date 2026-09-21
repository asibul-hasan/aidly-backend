# FIN User Manual

This manual explains the practical order for using the Finance / Accounts module. Use it for user training, UAT, and go-live setup.

## 1. Before Finance Setup

Before entering finance data, confirm these system forms are ready:

| Step | Form | Why It Is Needed |
| --- | --- | --- |
| 1 | SYS_1001 Company Setup | Finance data belongs to one company. |
| 2 | SYS_1002 Branch Setup | Vouchers and opening balances post by branch. |
| 3 | SYS_1003 Financial Year Setup | Voucher dates must fall inside an open financial year and open period. |
| 4 | SYS_1006 Currency Setup | Needed if voucher or bank accounts use currency. |
| 5 | SYS_1104 User Branch Mapping | Users need access to the branch where they will post finance entries. |

Do not start GL posting until the financial year and period are open.

## 2. Finance Setup Flow

Follow this order for a new company:

| Order | Form | What To Do | Why This Comes Here |
| --- | --- | --- | --- |
| 1 | FIN_1002 Account Group | Create the COA tree: Assets, Liabilities, Equity, Income, Expense, and child groups like Cash, Bank, Sales, Purchase, Expenses. | Accounts need a group before they can be created. The group decides Balance Sheet / Profit & Loss placement. |
| 2 | FIN_1001 Chart of Accounts | Create GL accounts under the groups. Mark control accounts like AR, AP, Bank, Cash, Inventory, Tax, Retained Earnings. | Every voucher, report, bank setup, and auto-posting needs GL accounts. |
| 3 | FIN_1003 Voucher Type | Create document types like Journal, Payment, Receipt, Contra, Opening, Closing. | Voucher entry and system posting need voucher types and number prefixes. |
| 4 | FIN_1005 Bank Account | Link each real bank/cash account to a GL account with Bank/Cash control type. | Bank reconciliation and cash flow need this link. |
| 5 | FIN_1006 GL Mapping | Map system event legs to GL accounts, such as PayrollPosted/EXPENSE or StockAdjustmentPosted/INVENTORY. | Auto-posting cannot work until the system knows which GL account each event should use. |
| 6 | FIN_1004 Opening Balance | Enter beginning debit/credit balances and post them as one opening voucher. | Reports need opening balances before daily transactions begin. |
| 7 | FIN_1101 Voucher Entry | Start daily manual vouchers and corrections. | Setup and opening balances should already be stable. |

## 3. Account Group Setup

Use **FIN_1002 Account Group** first.

Create high-level and child groups. Example:

| Root Type | Example Groups |
| --- | --- |
| Asset | Current Assets, Cash and Bank, Accounts Receivable, Inventory |
| Liability | Current Liabilities, Accounts Payable, VAT Payable |
| Equity | Owner Capital, Retained Earnings |
| Income | Sales Income, Service Income |
| Expense | Salary Expense, Rent Expense, Office Expense, Cost of Goods Sold |

Rules:

- Asset and Expense normally use Debit balance.
- Liability, Equity, and Income normally use Credit balance.
- Do not change root type or normal balance after accounts are mapped to the group.

## 4. Chart of Accounts Setup

Use **FIN_1001 Chart of Accounts** after account groups are ready.

Create one account for each balance the company needs to track.

Important fields:

| Field | How To Use |
| --- | --- |
| Account Group | Select the group where this account belongs. |
| Control Type | Use only when the account has special meaning, like AR, AP, Bank, Cash, Inventory, Tax, Retained Earnings. |
| Postable | Check this for accounts where voucher lines can be posted. Uncheck it for header/summary accounts. |
| Party Required | Check this for accounts that must track Customer, Supplier, or Employee. AR/AP accounts should require party. |
| Cost Center Required | Check this when every posting must identify a department/project/cost center. |
| Active | Keep active for accounts users can use. Inactive accounts are kept for history but blocked for new posting. |

### Party Checkbox Training Example

If you create **Accounts Receivable** and check **Party Required**, every voucher line must identify the customer. Then AR Ledger and Aging can show who owes money.

If you do not check **Party Required**, the total AR balance can still post to GL, but the system cannot show which customer owes the amount.

Use the same idea for **Accounts Payable** and suppliers.

## 5. Voucher Type Setup

Use **FIN_1003 Voucher Type** after COA setup.

Recommended minimum voucher types:

| Base Kind | Example ID | Purpose |
| --- | --- | --- |
| Journal | JV | Manual journal adjustments and system auto-posting fallback. |
| Payment | PV | Cash/bank payments. |
| Receipt | RV | Cash/bank receipts. |
| Contra | CV | Bank-to-bank or cash-to-bank movement. |
| Opening | OP | Opening balance voucher. |
| Closing | CL | Year-end closing voucher. |

Keep voucher types active if users or system posting should use them. Do not delete voucher types after vouchers exist; deactivate instead.

## 6. Bank Account Setup

Use **FIN_1005 Bank Account** after Bank/Cash GL accounts are created.

Steps:

1. Create a GL account in FIN_1001 with Control Type = Bank or Cash.
2. Open FIN_1005.
3. Create a bank/cash setup and link it to the GL account.
4. Enter bank name, branch, account title, account number, routing/SWIFT if available.

This setup is required for bank reconciliation and cash flow reporting.

## 7. GL Mapping Setup

Use **FIN_1006 GL Mapping** before relying on automatic postings from HRM, INV, PUR, or SAL.

Each mapping answers this question:

> When this event happens, which GL account should this accounting leg hit?

Examples:

| Event Type | Leg Key | Account |
| --- | --- | --- |
| PayrollPosted | EXPENSE | Salary Expense |
| PayrollPosted | PAYABLE | Salary Payable |
| StockAdjustmentPosted | INVENTORY | Inventory |
| StockAdjustmentPosted | ADJUSTMENT | Stock Adjustment Gain/Loss |
| OpeningStockPosted | INVENTORY | Inventory |
| OpeningStockPosted | OPENING_EQUITY | Opening Balance Equity |
| SalesInvoicePosted | RECEIVABLE | Accounts Receivable |
| SalesInvoicePosted | SALES | Sales Income |
| PurchaseInvoicePosted | INVENTORY | Inventory |
| PurchaseInvoicePosted | PAYABLE | Accounts Payable |

If a mapping is inactive, auto-posting ignores it. Failed events can be reviewed and re-driven from FIN_1201.

## 8. Opening Balance Posting

Use **FIN_1004 Opening Balance** after COA, voucher type, and financial periods are ready.

Steps:

1. Select voucher date inside the open financial year.
2. Select the currency. If it is not the base currency, confirm the exchange rate.
3. Add detail rows in the table: choose Debit/Credit type, account, and amount.
4. Review entered currency totals, base currency debit/credit totals, and Difference.
5. Make sure Difference is zero.
6. Post opening balances.

Important:

- Opening balance is blocked if the same branch/year already has an opening posting.
- Users can enter foreign-currency opening amounts, but the posted voucher and ledger store base-currency values.
- If debit and credit do not match, the Post button stays disabled and the API rejects the batch.
- After posting, check FIN_1301 Trial Balance.

## 9. Daily Voucher Entry

Use **FIN_1101 Voucher Entry** for manual finance entries.

Steps:

1. Select voucher type.
2. Select voucher date.
3. Enter reference and narration.
4. Add at least two lines.
5. Debit total must equal credit total.
6. Save draft.
7. Submit/Post or approve based on permission.

Rules:

- Voucher date must be in an open period.
- Header accounts cannot be posted.
- Inactive accounts cannot be posted.
- Accounts marked Party Required must include party.
- Accounts marked Cost Center Required must include cost center.
- Posted vouchers are not edited; cancellation creates reversing ledger entries.

## 10. Auto-Posting Monitor

Use **FIN_1201 Auto-Posting Monitor** when system-generated postings do not appear in GL.

Typical process:

1. Filter failed events.
2. Read the failed message.
3. Fix missing GL Mapping in FIN_1006.
4. Redrive the event.
5. Confirm voucher appears in FIN_1101 or reports.

## 11. Bank Reconciliation

Use **FIN_1102 Bank Reconciliation** after bank transactions are posted.

Steps:

1. Select Bank Account.
2. Enter statement date.
3. Enter statement balance.
4. Mark ledger lines that appear in the bank statement as cleared.
5. Save reconciliation.

Unchecked lines remain outstanding.

## 12. Reporting Flow

Use reports in this order during review:

| Order | Report | Purpose |
| --- | --- | --- |
| 1 | FIN_1301 Trial Balance | Check all account balances and confirm debit/credit balance. |
| 2 | FIN_1302 Account Ledger | Drill into one account to see transaction details. |
| 3 | FIN_1303 Day Book | Review day-wise posting activity. |
| 4 | FIN_1304 Profit & Loss | Review income, expense, and net profit/loss. |
| 5 | FIN_1305 Balance Sheet | Review assets, liabilities, equity, and current earnings. |
| 6 | FIN_1306 Cash Flow | Review bank/cash receipts and payments. |
| 7 | FIN_1307 AR / AP Aging | Review customer receivable or supplier payable by age. |

## 13. Period and Year-End Close

Use **FIN_1401 Period / Year-End Close** after reports are reviewed.

Monthly process:

1. Review Trial Balance, Account Ledger, Day Book, P&L, Balance Sheet.
2. Fix wrong postings through correction vouchers.
3. Close or lock the period.

Year-end process:

1. Confirm all periods are reviewed.
2. Select Retained Earnings account.
3. Run year-end close.
4. System posts closing voucher and locks the year/periods.

## 14. Recommended Training Scenario

For training, use this simple scenario:

1. Create account groups: Cash and Bank, Accounts Receivable, Sales Income, Office Expense, Retained Earnings.
2. Create accounts: Cash in Hand, Bank Account, Customer Receivable, Sales Revenue, Office Expense, Retained Earnings.
3. Create voucher types: JV, RV, PV, OP, CL.
4. Create bank account linked to Bank GL.
5. Post opening balance.
6. Enter one receipt voucher.
7. Enter one expense payment voucher.
8. Check Trial Balance.
9. Open Account Ledger for Cash.
10. Review P&L and Balance Sheet.

After users can complete this flow, they understand the core accounting module.
