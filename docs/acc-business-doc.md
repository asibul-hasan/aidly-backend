# Functional & Technical Requirements Specification: ERP Accounting Module

> **Module Code:** `ACC` / `FIN` (Finance & General Ledger)  
> **System Architecture:** Aidly ERP .NET 10 Backend & Angular Web Client  
> **Standard Compliance:** GAAP / IFRS / Double-Entry General Ledger  
> **Document Status:** Official Business & Technical Architecture Specification  

---

## Executive Summary

The **Accounting (`ACC` / `FIN`) Module** serves as the central financial sink and double-entry system of record for Aidly ERP. Every value-moving transaction originated across operational modules—including Human Resource Management (`HRM`), Inventory Management (`INV`), Procurement (`PUR`), and Sales & Distribution (`SAL`)—culminates in an immutable, balanced general ledger posting within this engine.

This document establishes the comprehensive functional, technical, database, API, and user interface specifications required to build, maintain, and extend the enterprise accounting engine.

---

## 1. Complete Account Module Structure & Form Inventory

```text
ACCOUNTING MODULE
├── Master & Setup Data
│   ├── Company & Global Settings
│   ├── Chart of Accounts (COA) / Account Head
│   ├── Fiscal Year & Accounting Period
│   ├── Cost Center & Accounting Dimensions
│   ├── Currency & Currency Exchange
│   ├── Mode of Payment
│   └── Payment Terms Template
├── General Ledger & Adjustments
│   ├── Journal Entry (General, Opening, Inter-Company, Forex Adjustment)
│   ├── Period Closing Voucher
│   └── Opening Invoice Creation Tool
├── Accounts Receivable (AR)
│   ├── Sales Invoice (Standard, POS, Recurring, Credit Note)
│   ├── Sales Taxes and Charges Template
│   ├── Payment Request & Payment Gateway Account
│   └── Dunning / Payment Reminders
├── Accounts Payable (AP)
│   ├── Purchase Invoice (Standard, Debit Note / Return)
│   └── Purchase Taxes and Charges Template
├── Payments & Treasury Management
│   ├── Payment Entry (Receive, Pay, Internal Transfer)
│   ├── Payment Reconciliation Tool
│   ├── Bank & Bank Account
│   ├── Bank Statement Import
│   └── Bank Reconciliation Tool
├── Budgeting & Cost Allocation
│   └── Budget (Cost Center / Project / Dimension-based)
├── Revenue & Expense Accruals (Deferred Accounting)
│   ├── Deferred Revenue Processing
│   └── Deferred Expense Processing
├── Asset Accounting (Fixed Assets)
│   ├── Asset Category
│   ├── Asset Master & Depreciation Schedule
│   ├── Asset Depreciation Entry
│   ├── Asset Value Adjustment
│   └── Asset Repair / Scrapping / Disposal
├── Equity & Subscriptions
│   ├── Shareholder & Share Transfer
│   ├── Subscription Plan & Subscription
└── Financial Reporting & Analytics
    ├── General Ledger & Account Balance
    ├── Accounts Receivable & Payable Aging
    ├── Trial Balance
    ├── Profit and Loss Statement
    ├── Balance Sheet
    └── Cash Flow Statement
```

---

## 2. Comprehensive Form Inventory

| Form / DocType Name | Submodule | Type | Purpose | Allowed Roles | Dependencies | Key Concepts |
|---|---|---|---|---|---|---|
| **Company Master** | Setup | Config | Establishes legal reporting entity, base currency, chart of accounts roots, and tax identifiers. | Admin, Finance Head | None | Legal Entity, Base Currency |
| **Chart of Accounts (COA)** | Setup / GL | Master | Hierarchical tree of ledger heads (Assets, Liabilities, Equity, Income, Expense). | Finance Mgr, CA | Company Master | Tree Hierarchy, Leaf vs Group Nodes |
| **Fiscal Year & Periods** | Setup | Config | Defines financial year boundaries, quarters, and monthly periods with locking controls. | Finance Mgr, CA | Company Master | Accounting Calendar, Period Guardrails |
| **Cost Center & Dimensions** | Setup | Master | Defines cost/profit centers and analytical dimensions for departmental accounting. | Finance Mgr, AO | Company Master | Cost Allocation, Profit Center |
| **Currency & Exchange** | Setup | Master | Multi-currency master with historical exchange rate tables for foreign transactions. | Finance Mgr, AO | None | ISO Currency, FX Fluctuation |
| **Mode of Payment** | Setup | Master | Defines payment channels (Cash, Bank Wire, Cheque, Card) mapped to default GL heads. | Finance Mgr, AO | COA | Cash/Bank Clearing |
| **Payment Terms Template**| Setup | Master | Configures milestone payment schedules, credit limits, and grace periods. | Finance Mgr, AO | None | Credit Control, Installments |
| **Journal Entry** | General Ledger | Transaction | Records multi-line manual adjustments, opening balances, contra transfers, and accruals. | CA, AO | COA, Cost Center, Currency | Double-Entry Balancing, Narration |
| **Period Closing Voucher** | General Ledger | Transaction | Closes operational P&L balances at year-end and rolls net profit into Retained Earnings. | CA (Chief Accountant) | COA, Fiscal Year | Year-End Rollup, Equity Transfer |
| **Opening Invoice Tool** | General Ledger | Tool | Bulk creates legacy migration opening receivables and payables without double-hitting GL. | CA, Finance Mgr | COA, Customer/Supplier | Data Migration, Sub-ledger Sync |
| **Sales Invoice** | Accounts Receivable | Transaction | Generates customer billing, tax calculation, revenue recognition, and AR ledger entries. | CA, AO, AR | Customer, COA, Tax Template | Accrual Revenue, Credit Notes |
| **Sales Taxes Template** | Accounts Receivable | Master | Preset rule table for automated calculation and liability posting of VAT/GST on sales. | Finance Mgr, AO | COA | Output VAT/GST, Surcharges |
| **Payment Request & Gateway** | Accounts Receivable | Tool | Dispatches payment links to customers and routes online payment provider settlements. | AR, AO | Sales Invoice, Bank Account | Payment Link, Gateway Clearing |
| **Dunning / Reminders** | Accounts Receivable | Tool | Manages aging overdue notices, late fees, interest penalties, and payment collection tracking. | AR, AO | Sales Invoice, Customer | Credit Collection, Overdue Tracing |
| **Purchase Invoice** | Accounts Payable | Transaction | Records supplier bills, input tax credits, expense allocation, and AP ledger liabilities. | CA, AO, AP | Supplier, COA, Tax Template | Accrual Expense, Debit Notes |
| **Purchase Taxes Template** | Accounts Payable | Master | Rule table for calculating and posting recoverable input VAT/GST on procurement. | Finance Mgr, AO | COA | Input VAT/GST Credit |
| **Payment Entry** | Treasury | Transaction | Liquidates AR/AP invoices, records customer advances, supplier payments, and contra transfers. | CA, AO, AP, AR | COA, Party Masters, Invoices | Liquidations, Advance Payments, FX Gain/Loss |
| **Payment Reconciliation** | Treasury | Tool | Batches unallocated advances and debit/credit adjustments against outstanding invoices. | CA, AO | Customer, Supplier, Invoices | Sub-ledger Clearing, Reallocation |
| **Bank Account Master** | Treasury | Master | Holds institutional bank account details, routing numbers, and associated COA GL heads. | Finance Mgr, AO | COA, Company | Bank Ledger Sync |
| **Bank Statement Import** | Treasury | Tool | Ingests electronic bank statements (CSV, OFX, MT940, CAMT.053) for clearing reconciliation. | AO, AP, AR | Bank Account Master | Statement Ingestion, Parser |
| **Bank Reconciliation Tool** | Treasury | Tool | Matches imported statement rows with system payment entries and generates bank charges. | CA, AO | Bank Account, Payment Entry | Value Date, Book vs Statement Balance |
| **Budget Master** | Budgeting | Master/Config | Sets periodic financial spending limits per cost center, project, or general expense head. | CA, Finance Head | Cost Center, COA, Fiscal Year | Budget Variance, Spending Guardrail |
| **Deferred Revenue Engine**| Accruals | Process | Monthly automated recognition of unearned revenue liabilities into earned revenue accounts. | CA, AO | Sales Invoice, COA | ASC 606 / IFRS 15 Amortization |
| **Deferred Expense Engine**| Accruals | Process | Monthly automated amortization of prepaid expense assets into operational expense accounts. | CA, AO | Purchase Invoice, COA | Prepaid Amortization |
| **Asset Master & Schedule** | Fixed Assets | Master | Registers capitalized property, plant & equipment with depreciation schedules (SLM/WDV). | CA, AO | COA, Asset Category | Capitalization, Useful Life, Salvage |
| **Asset Depreciation Entry**| Fixed Assets | Transaction | Automated periodic execution posting depreciation expense and accumulated contra-asset. | CA, AO | Asset Master, COA | Depreciation Ledger, Book Value |
| **Asset Disposal & Repair** | Fixed Assets | Transaction | Handles scrapping, sale, write-down, and maintenance capitalization of fixed assets. | CA, Finance Mgr | Asset Master, COA | Gain/Loss on Sale, De-capitalization |
| **Shareholder & Transfer** | Equity | Master | Manages corporate equity structure, shareholder registry, and share transfer transactions. | CA, Admin | COA, Company | Equity Ledger, Paid-up Capital |
| **Subscription Billing** | Subscriptions | Transaction | Automated recurring invoice generator with flexible billing cycles and dunning hooks. | AO, AR | Customer, Item, Tax Template | Recurring Revenue, MRR/ARR |
| **General Ledger Report** | Reporting | Analytics | Detailed chronological debit/credit statement of all postings across specific account heads. | All Finance Roles, Auditor | GL Entries, COA | Running Balance, Source Tracking |
| **Trial Balance** | Reporting | Analytics | Global arithmetic verification proving $\sum \text{Debits} \equiv \sum \text{Credits}$ across COA. | CA, AO, Auditor | GL Entries, COA | Balancing Proof, Group Rollups |
| **Profit and Loss (P&L)** | Reporting | Analytics | Measures periodic operational performance: Revenue minus COGS minus OPEX = Net Income. | CA, AO, Auditor | GL Entries, COA | Operating Margin, EBIT, Net Profit |
| **Balance Sheet** | Reporting | Analytics | Point-in-time financial position snapshot asserting: $\text{Assets} = \text{Liabilities} + \text{Equity}$. | CA, AO, Auditor | GL Entries, COA | Working Capital, Financial Solvency |
| **Cash Flow Statement** | Reporting | Analytics | Direct/indirect categorization of cash movements: Operating, Investing, and Financing. | CA, AO, Auditor | GL Entries, Bank Masters | Cash Flow Liquidity |

---

## 3. Detailed Field-Level Specifications

### 3.1. Chart of Accounts (`tabAccount`)

```text
Form Structure: [Account Name | Parent Account | Is Group Checkbox]
├── 1. Core Properties: Account Number, Account Type, Currency, Company
├── 2. Balance & Status: Current Balance (Read-Only), Freeze Account
└── 3. System Flags: Root Type (Asset / Liability / Equity / Income / Expense)
```

| Field Name | Data Type | Required | Default | Editable | Validation / Rules | Source | Business & Accounting Meaning |
|---|---|---|---|---|---|---|---|
| `account_name` | `String(140)` | Yes | None | Yes | Unique per Parent node | User Input | Name displayed across forms and financial reports. |
| `account_number` | `String(20)` | No | None | Yes | Alphanumeric formatting; unique per company | User Input | Statutory or internal account code (e.g., `1110`). |
| `parent_account` | `Link` | Conditional | Root Node | Yes | Must not equal self; Target must be `is_group = 1` | `tabAccount` | Parent node forming the hierarchical tree. |
| `is_group` | `Boolean` | Yes | `0` (No) | Yes (if no GL) | Cannot make group if GL entries exist against this account | User Input | Determines if the account holds sub-accounts (`1`) or transactions (`0`). |
| `root_type` | `Select` | Yes | Inferred | Only at Root | `Asset`, `Liability`, `Equity`, `Income`, `Expense` | System | Fundamental balance classification driving financial statements. |
| `account_type` | `Select` | No | None | Yes | `Bank`, `Cash`, `Receivable`, `Payable`, `Tax`, `Stock`, `Fixed Asset`, `Cost of Goods Sold`, etc. | Static Options | Enforces validation on specialized transaction forms. |
| `account_currency` | `Link` | Yes | Company Base | Yes (if no GL) | Must be a valid ISO Currency code | `tabCurrency` | Currency denomination; restricts non-base transactions if fixed. |
| `freeze_account` | `Select` | Yes | `"No"` | Yes | `"Yes"` prevents posting any new transactions | Static Options | Hard stop for auditing or discontinued heads. |

---

### 3.2. Sales Invoice (`tabSales Invoice`)

```text
[Header: Customer | Posting Date | Due Date | Company | Currency & Rate]
├── Line Items Table: [Item, Description, Income Account, Qty, Rate, Amount, Cost Center]
├── Taxes & Charges Table: [Type, Account Head, Rate, Tax Amount, Total]
├── Payment Terms Schedule: [Due Date, Invoice Portion %, Payment Amount]
└── Totals & Summary: Net Total, Total Tax, Grand Total, Outstanding Balance
```

| Field Name | Data Type | Required | Default | Editable | Validation / Rules | Source | Business & Accounting Meaning |
|---|---|---|---|---|---|---|---|
| `customer` | `Link` | Yes | None | Yes (Draft) | Must be an active debtor customer | `tabCustomer` | Debtor entity against whom AR is posted. |
| `posting_date` | `Date` | Yes | `Today()` | Yes (Draft) | Must be within an open Fiscal Year and unlocked period | System / User | General Ledger posting date for revenue accrual. |
| `due_date` | `Date` | Yes | Auto-calc | Yes (Draft) | Must be $\ge \text{posting\_date}$ | Payment Terms | Date after which the invoice is flagged as overdue. |
| `company` | `Link` | Yes | Default | Yes (Draft) | Must match user branch/company permissions | `tabCompany` | Legal reporting entity. |
| `currency` | `Link` | Yes | Base | Yes (Draft) | Valid ISO currency code | `tabCurrency` | Invoicing/Billing currency. |
| `conversion_rate` | `Float` | Yes | `1.00` | Yes (Draft) | Must be $> 0$ | Exchange Rate | FX rate to convert line items to base currency. |
| `debit_to` | `Link` | Yes | Auto | Yes (Draft) | Must have `account_type = 'Receivable'` and `is_group = 0` | `tabAccount` | Specific Debtor Asset account head in COA. |
| `items` | `Table` | Yes | None | Yes (Draft) | Minimum 1 valid row required | Child Doc | Itemized rows determining revenue, COGS, and tax base. |
| `income_account` *(Row)* | `Link` | Yes | Auto | Yes (Draft) | Must have `root_type = 'Income'` and `is_group = 0` | `tabAccount` | Credit target for revenue recognition. |
| `cost_center` *(Row)* | `Link` | Yes | Default | Yes (Draft) | Must be active Leaf Cost Center (`is_group = 0`) | `tabCost Center` | Department/project allocation node. |
| `taxes_and_charges` | `Table` | No | Auto | Yes (Draft) | Tax Rate $\ge 0$ | Child Doc | Statutory output tax rows (GST/VAT/Excise). |
| `grand_total` | `Currency` | Yes | Auto-calc | Read-Only | Line Items Net Total + Total Taxes and Charges | Calculated | Total payable amount by customer. |
| `outstanding_amount` | `Currency` | Yes | Auto-calc | Read-Only | Grand Total minus payments cleared | Ledger Balance | Active balance remaining to collect. |
| `is_return` | `Boolean` | Yes | `0` (No) | Yes (Draft) | Reverses signs; requires link to original invoice | User Input | Marks document as a formal Credit Note. |

---

### 3.3. Payment Entry (`tabPayment Entry`)

| Field Name | Data Type | Required | Default | Editable | Validation / Rules | Source | Business & Accounting Meaning |
|---|---|---|---|---|---|---|---|
| `payment_type` | `Select` | Yes | `"Receive"` | Yes (Draft) | Options: `Receive`, `Pay`, `Internal Transfer` | Static Options | Defines transaction direction and account head filters. |
| `party_type` | `Select` | Conditional | None | Yes (Draft) | `Customer`, `Supplier`, `Employee`, `Shareholder` | Static Options | Master entity category for sub-ledger accounting. |
| `party` | `Link` | Conditional | None | Yes (Draft) | Valid active master ID matching `party_type` | Dynamic Link | Specific debtor/creditor being cleared. |
| `paid_from` | `Link` | Yes | None | Yes (Draft) | If `Pay`: Bank/Cash; If `Receive`: Receivable head | `tabAccount` | Credit account head in General Ledger. |
| `paid_to` | `Link` | Yes | None | Yes (Draft) | If `Pay`: Payable head; If `Receive`: Bank/Cash | `tabAccount` | Debit account head in General Ledger. |
| `paid_amount` | `Currency` | Yes | `0.00` | Yes (Draft) | Must be $> 0$ | User Input | Exact amount transferred in source currency. |
| `received_amount` | `Currency` | Yes | `0.00` | Yes (Draft) | Must be $> 0$ | User Input | Exact amount deposited in target currency. |
| `source_exchange_rate` | `Float` | Yes | `1.00` | Yes (Draft) | Must be $> 0$ | Currency Table | Multi-currency FX conversion factor for source leg. |
| `target_exchange_rate` | `Float` | Yes | `1.00` | Yes (Draft) | Must be $> 0$ | Currency Table | Target FX rate (calculates Forex Realized Gain/Loss). |
| `references` | `Table` | No | None | Yes (Draft) | Outstanding invoices table | Child Doc | Open invoices cleared by this transaction. |
| `allocated_amount` *(Row)* | `Currency` | Yes | Auto | Yes (Draft) | $\le \text{Outstanding Amount}$ | User / Calc | Portion applied against an invoice. |
| `unallocated_amount` | `Currency` | Yes | Auto-calc | Read-Only | $\text{paid\_amount} - \sum \text{allocated\_amount}$ | Calculated | Balance retained as unlinked customer/supplier advance. |
| `reference_no` | `String(100)` | Conditional | None | Yes (Draft) | Mandatory if Mode of Payment is Cheque/Bank Transfer | User Input | Cheque number, UTR, or bank reference ID. |
| `reference_date` | `Date` | Conditional | `Today()` | Yes (Draft) | Valid date | User Input | Value date or cheque issuance date. |

---

### 3.4. Journal Entry (`tabJournal Entry`)

| Field Name | Data Type | Required | Default | Editable | Validation / Rules | Source | Business & Accounting Meaning |
|---|---|---|---|---|---|---|---|
| `voucher_type` | `Select` | Yes | `"Journal Entry"` | Yes (Draft) | `Journal Entry`, `Bank Entry`, `Cash Entry`, `Inter-Company`, `Opening Entry` | Static Options | Categorization for sequential numbering & filtering. |
| `posting_date` | `Date` | Yes | `Today()` | Yes (Draft) | Must be in an unlocked Fiscal Year | System / User | Effective date of financial ledger entry. |
| `company` | `Link` | Yes | Default | Yes (Draft) | Valid active company | `tabCompany` | Entity owning the ledger lines. |
| `accounts` | `Table` | Yes | None | Yes (Draft) | Minimum 2 rows; $\sum \text{Debit} \equiv \sum \text{Credit}$ | Child Doc | Multi-line double entry transaction records. |
| `account` *(Row)* | `Link` | Yes | None | Yes (Draft) | Must be a terminal leaf node (`is_group = 0`) | `tabAccount` | Target Account Head in the Chart of Accounts. |
| `party_type` *(Row)* | `Select` | Conditional | None | Yes (Draft) | Required if Account is `Receivable` or `Payable` | Static Options | Sub-ledger breakdown entity type. |
| `party` *(Row)* | `Dynamic` | Conditional | None | Yes (Draft) | Required if `party_type` is set | Linked Doc | Specific sub-ledger entity (Customer / Vendor / Employee). |
| `debit_in_account_currency` | `Currency` | Yes | `0.00` | Yes (Draft) | $\ge 0$; If Debit $> 0$, Credit must be `0.00` | User Input | Debit amount in local account currency. |
| `credit_in_account_currency` | `Currency` | Yes | `0.00` | Yes (Draft) | $\ge 0$; If Credit $> 0$, Debit must be `0.00` | User Input | Credit amount in local account currency. |
| `cost_center` *(Row)* | `Link` | Conditional | Default | Yes (Draft) | Mandatory for Income & Expense heads | `tabCost Center` | Management profit/cost allocation node. |
| `total_debit` | `Currency` | Yes | Auto-calc | Read-Only | Must strictly equal `total_credit` | Calculated | Total debited value in company base currency. |
| `total_credit` | `Currency` | Yes | Auto-calc | Read-Only | Must strictly equal `total_debit` | Calculated | Total credited value in company base currency. |
| `user_remark` | `Long Text` | No | None | Yes (Draft) | Free-text audit explanation | User Input | Narration printed on vouchers and ledgers. |

---

## 4. End-to-End Accounting Workflows

### 4.1. Order-to-Cash (Receivables & Revenue)

```text
1. Customer Order Confirmation 
  └── 2. Generate Draft Sales Invoice
      ├── Select Customer & Invoice Currency
      ├── Add Line Items (Item, Income Head, Qty, Rate)
      ├── Apply Taxes & Charges Template
      └── Define Payment Schedule (Installments)
          └── 3. Validate & Check Credit Limits
              └── 4. SUBMIT Invoice (Status: Unpaid)
                  ├── Writes General Ledger Entries (AR Dr, Revenue Cr, Tax Cr)
                  └── Emits 'Invoice Issued' Event
                      └── 5. Record Payment Entry (Receive)
                          ├── Allocate against Invoice Outstanding
                          └── SUBMIT Payment (Status: Paid)
                              └── Writes GL Entries (Bank Dr, AR Cr)
```

#### Invoice Lifecycle State Transition

```text
[Draft Sales Invoice] 
    │ (Submit)
    ▼
[Submitted / Unpaid] ──(Full Payment Entry)──► [Paid]
    │
    ├──(Partial Payment Entry)──────────────► [Partially Paid]
    │
    ├──(Sales Return / Credit Note)─────────► [Credit Note Issued]
    │
    └──(Cancel)─────────────────────────────► [Cancelled] (Reverses GL)
```

---

### 4.2. Procure-to-Pay (Payables & Expenses)

```text
1. Purchase Order / Goods Receipt Note
  └── 2. Generate Draft Purchase Invoice
      ├── Select Supplier & Input Invoice Reference
      ├── Enter Expense / Asset Line Items
      ├── Apply Input Tax Template (GST/VAT Input)
      └── 3. SUBMIT Purchase Invoice (Status: Unpaid)
          ├── Writes GL Entries (Expense/Asset Dr, Tax Input Dr, AP Cr)
          └── 4. Process Payment Entry (Pay)
              ├── Select Bank Account & Reference No (Cheque/Wire)
              ├── Allocate to open AP Invoices
              └── SUBMIT Payment Entry
                  └── Writes GL Entries (AP Dr, Bank Cr)
```

---

### 4.3. Bank Reconciliation Flow

```text
1. Import Bank Statement (CSV / MT940 / OFX / CAMT.053)
  └── Generates Bank Transaction Records (Date, Ref, Amount, Value Date)
      └── 2. Open Bank Reconciliation Tool
          ├── System auto-matches Bank Transactions with Payment Entries based on:
          │   (Amount, Reference No, Clearance Date window ± 3 days)
          ├── User manually matches unmatched lines to open Payment Entries / Invoices
          └── User creates direct Journal Entries for bank charges / interest
              └── 3. Confirm Reconciliation
                  └── Updates clearance_date on tabPayment Entry & tabJournal Entry
                  └── Aligns Bank Book Balance with Actual Statement Balance
```

---

### 4.4. Fixed Asset Lifecycle & Depreciation

```text
1. Purchase Invoice (Capitalize Asset -> Asset Clearing Account)
  └── 2. Create Asset Record (Link Item, Asset Category, Cost, Salvage Value, Life in Months)
      └── 3. System calculates Straight-Line / WDV Depreciation Schedule
          └── 4. Monthly Scheduler executes 'Asset Depreciation Entry'
              └── Generates automated Journal Entry:
                  ├── Debit: Depreciation Expense Account (Cost Center)
                  └── Credit: Accumulated Depreciation Account
                      └── 5. Asset Disposal (Scrap / Sale)
                          ├── De-capitalizes Gross Asset Cost
                          ├── Clears Accumulated Depreciation
                          └── Books Gain/Loss on Asset Sale
```

---

## 5. Comprehensive Business Logic & Validation Engine

### 5.1. Debit / Credit Integrity Rules
* **Zero Imbalance Constraint:**
  $$\sum \text{Debits} - \sum \text{Credits} = 0.0000$$
  Strictly enforced before commit across base currency equivalents. No transaction can post to `tabGL Entry` with a non-zero differential ($\Delta \neq 0$).
* **Sign Restriction:** Negative debit or credit amounts are strictly blocked in Journal Entries. Reversals must be executed by swapping debit/credit legs or generating formal Credit/Debit Notes.

### 5.2. Account Hierarchy & Posting Rules
* **Leaf Node Enforcement:** Direct GL entries are strictly blocked against parent/group accounts (`is_group = 1`). Transactions can only hit terminal leaf nodes (`is_group = 0`).
* **Account Type Matching:**
  * Sales Invoice can only debit accounts flagged as `account_type = 'Receivable'`.
  * Purchase Invoice can only credit accounts flagged as `account_type = 'Payable'`.
  * Payment Entry liquidations require valid `Bank` or `Cash` accounts for treasury legs.

### 5.3. Multi-Currency & Forex Rules
* **Dual-Currency Tracking:** Every GL entry stores `account_currency` amount and `company_currency` base amount.
* **Forex Realization:** When liquidating an invoice where the settlement exchange rate differs from the booking rate:
  $$\Delta = (\text{Invoice Amount} \times \text{Payment Rate}) - (\text{Invoice Amount} \times \text{Booking Rate})$$
  * If $\Delta > 0$ for Accounts Receivable: Post credit to **Realized Foreign Exchange Gain Account**.
  * If $\Delta < 0$ for Accounts Receivable: Post debit to **Realized Foreign Exchange Loss Account**.

### 5.4. Fiscal Year & Period Locking
* **Period Boundary Rule:** Transactions cannot have a `posting_date` outside active fiscal year definitions.
* **Closing Lock:** If a Period Closing Voucher exists for Date $T$, any attempt to insert, update, or cancel transactions where $\text{Posting Date} \le T$ is blocked with a security validation error.

### 5.5. Budgetary Control Engine
* **Evaluation Hook:** Checked upon submission of Purchase Invoices, Purchase Orders, and Expense Journal Entries.
* **Calculation:**
  $$\text{Actual Spent (GL)} + \text{Pending Committed (Unbilled POs)} + \text{Current Transaction Amount} \gtrless \text{Allocated Budget}$$
* **Action Matrix:** Based on Budget Master settings:
  * **Stop:** Throw validation error; prevent submission.
  * **Warn:** Show warning banner with budget exceedance percentage; allow submission.
  * **Ignore:** Log variance without stopping user.

---

## 6. Accounting Entries & Posting Engine

```text
                               DOUBLE ENTRY ENGINE
                              
       [TRANSACTION] ──────────────────────────────► [GL POSTING ENGINE]
   (Invoice / Payment / JE)                                  │
                                                             ▼
                                              ┌──────────────────────────────┐
                                              │ Check Fiscal Year Open?      │
                                              │ Check Leaf Accounts Only?    │
                                              │ Validate Cost Centers?       │
                                              │ Assert: Sum(Dr) == Sum(Cr)?  │
                                              └──────────────┬───────────────┘
                                                             │
                                      ┌──────────────────────┴──────────────────────┐
                                      ▼                                             ▼
                         [General Ledger Entry]                          [Sub-Ledger Updates]
                         (tabGL Entry: Dr / Cr)                          (AR / AP Balances)
```

### 6.1. Sales Invoicing & Credit Notes

#### A. Standard Credit Sale (Accrual Basis)
* **Trigger:** Submission of Sales Invoice (`docstatus = 1`).
* **Type:** Automated.

$$\begin{array}{llr} \textbf{Account Head} & \textbf{Classification} & \textbf{Amount} \\ \hline \text{Dr. Accounts Receivable (Customer Head)} & \text{Current Asset} & \$1,150.00 \\ \quad \text{Cr. Sales Revenue Account} & \text{Operating Income} & \$1,000.00 \\ \quad \text{Cr. Output VAT / GST Payable} & \text{Current Liability} & \$150.00 \\ \end{array}$$

#### B. Perpetual Inventory Impact (If delivery included in invoice)
$$\begin{array}{llr} \textbf{Account Head} & \textbf{Classification} & \textbf{Amount} \\ \hline \text{Dr. Cost of Goods Sold (COGS)} & \text{Direct Expense} & \$600.00 \\ \quad \text{Cr. Stock / Inventory In Hand} & \text{Current Asset} & \$600.00 \\ \end{array}$$

#### C. Sales Return / Credit Note
$$\begin{array}{llr} \textbf{Account Head} & \textbf{Classification} & \textbf{Amount} \\ \hline \text{Dr. Sales Return Account (or Revenue)} & \text{Operating Income} & \$1,000.00 \\ \text{Dr. Output VAT / GST Payable} & \text{Current Liability} & \$150.00 \\ \quad \text{Cr. Accounts Receivable (Customer Head)} & \text{Current Asset} & \$1,150.00 \\ \end{array}$$

---

### 6.2. Purchase Invoicing & Debit Notes

#### A. Standard Purchase on Credit
* **Trigger:** Submission of Purchase Invoice (`docstatus = 1`).
* **Type:** Automated.

$$\begin{array}{llr} \textbf{Account Head} & \textbf{Classification} & \textbf{Amount} \\ \hline \text{Dr. Operating Expense / Inventory Clearing} & \text{Expense / Asset} & \$2,000.00 \\ \text{Dr. Input VAT / GST Receivable} & \text{Current Asset} & \$300.00 \\ \quad \text{Cr. Accounts Payable (Supplier Head)} & \text{Current Liability} & \$2,300.00 \\ \end{array}$$

#### B. Purchase Return / Debit Note
$$\begin{array}{llr} \textbf{Account Head} & \textbf{Classification} & \textbf{Amount} \\ \hline \text{Dr. Accounts Payable (Supplier Head)} & \text{Current Liability} & \$2,300.00 \\ \quad \text{Cr. Operating Expense / Inventory Clearing} & \text{Expense / Asset} & \$2,000.00 \\ \quad \text{Cr. Input VAT / GST Receivable} & \text{Current Asset} & \$300.00 \\ \end{array}$$

---

### 6.3. Payments & Liquidations

#### A. Customer Receipt with Cash Discount & Realized Exchange Gain
$$\begin{array}{llr} \textbf{Account Head} & \textbf{Classification} & \textbf{Amount} \\ \hline \text{Dr. Bank Account} & \text{Current Asset} & \$1,100.00 \\ \text{Dr. Sales Discount Allowed} & \text{Operating Expense} & \$50.00 \\ \quad \text{Cr. Accounts Receivable} & \text{Current Asset} & \$1,120.00 \\ \quad \text{Cr. Realized Foreign Exchange Gain} & \text{Other Income} & \$30.00 \\ \end{array}$$

#### B. Internal Bank-to-Cash Transfer (Contra Entry)
$$\begin{array}{llr} \textbf{Account Head} & \textbf{Classification} & \textbf{Amount} \\ \hline \text{Dr. Petty Cash Account} & \text{Current Asset} & \$500.00 \\ \quad \text{Cr. Main Checking Bank Account} & \text{Current Asset} & \$500.00 \\ \end{array}$$

---

### 6.4. Accruals, Amortization & Year-End

#### A. Deferred Revenue Monthly Recognition
$$\begin{array}{llr} \textbf{Account Head} & \textbf{Classification} & \textbf{Amount} \\ \hline \text{Dr. Deferred Revenue (Unearned Income)} & \text{Current Liability} & \$100.00 \\ \quad \text{Cr. Earned Service Revenue} & \text{Operating Income} & \$100.00 \\ \end{array}$$

#### B. Fixed Asset Depreciation Posting
$$\begin{array}{llr} \textbf{Account Head} & \textbf{Classification} & \textbf{Amount} \\ \hline \text{Dr. Depreciation Expense} & \text{Indirect Expense} & \$250.00 \\ \quad \text{Cr. Accumulated Depreciation - Machinery} & \text{Contra Asset} & \$250.00 \\ \end{array}$$

#### C. Period Closing Voucher (Year-End)
$$\begin{array}{llr} \textbf{Account Head} & \textbf{Classification} & \textbf{Amount} \\ \hline \text{Dr. Total Operating Revenues (All Income Heads)} & \text{Income Closing} & \$500,000.00 \\ \quad \text{Cr. Total Operating Expenses (All Expense Heads)} & \text{Expense Closing} & \$350,000.00 \\ \quad \text{Cr. Retained Earnings / Reserves Account} & \text{Equity} & \$150,000.00 \\ \end{array}$$

---

## 7. Form Dependency & System Interaction Map

```text
                          ┌────────────────────────┐
                         │     Company Master     │
                         └───────────┬────────────┘
                                     │
              ┌──────────────────────┼──────────────────────┐
              ▼                      ▼                      ▼
   ┌────────────────────┐ ┌────────────────────┐ ┌────────────────────┐
   │ Chart of Accounts  │ │  Fiscal Year / Dim │ │   Exchange Rates   │
   └──────────┬─────────┘ └──────────┬─────────┘ └──────────┬─────────┘
              │                      │                      │
              ├──────────────────────┴──────────────────────┘
              ▼
  ┌───────────────────────┐
  │ Master Configs:       │
  │ Cost Centers, Taxes,  │
  │ Payment Terms         │
  └───────────┬───────────┘
              │
   ┌──────────┴─────────────────────────────────────────────┐
   ▼                                                        ▼
┌────────────────────────┐                               ┌────────────────────────┐
│  Sales Invoice (AR)    │                               │ Purchase Invoice (AP)  │
└──────────┬─────────────┘                               └───────────┬────────────┘
          │                                                         │
          └──────────────────────────┬──────────────────────────────┘
                                     ▼
                       ┌───────────────────────────┐
                       │   Payment Entry (Pay/Rec) │
                       └─────────────┬─────────────┘
                                     │
              ┌──────────────────────┼──────────────────────┐
              ▼                      ▼                      ▼
   ┌────────────────────┐ ┌────────────────────┐ ┌────────────────────┐
   │ Bank Reconciliation│ │  General Ledger    │ │ Payment Reconcile  │
   │      Tool          │ │     (tabGL Entry)  │ │      Tool          │
   └────────────────────┘ └──────────┬─────────┘ └────────────────────┘
                                     │
        ┌────────────────────────────┼────────────────────────────┐
        ▼                            ▼                            ▼
┌─────────────────┐          ┌─────────────────┐          ┌─────────────────┐
│  Trial Balance  │          │  Profit & Loss  │          │  Balance Sheet  │
└─────────────────┘          └─────────────────┘          └─────────────────┘
```

---

## 8. Database Architecture & Entity Specifications

```text
                       DATABASE SCHEMA (RELATIONAL MODEL)
                      
 ┌─────────────────┐       1:N       ┌────────────────────────┐
 │   tabCompany    │ ───────────────<│       tabAccount       │
 └─────────────────┘                 └───────────┬────────────┘
          │ 1:N                                  │ 1:N
          │                                      ▼
          │                          ┌────────────────────────┐
          │                          │       tabGL Entry      │
          │                          └────────────────────────┘
          │                                      ▲
          ▼ 1:N                                  │ 1:N (Polymorphic DocType)
 ┌─────────────────┐ 1:N             │           │
 │ tabSalesInvoice │ ─────────┐      │           │
 └─────────────────┘          ▼      │           │
          │ 1:N       ┌──────────────────────┐   │
          └──────────<│ tabSalesInvoiceItem  │   │
                      └──────────────────────┘   │
 ┌─────────────────┐                             │
 │ tabPaymentEntry │ ────────────────────────────┘
 └─────────────────┘
```

### 8.1. Data Dictionary

#### `tabAccount` (Chart of Accounts Master)
* `name` **(PK)**: `VARCHAR(140)` — Unique identifier (e.g., `"1110 - Checking Bank - ACME"`)
* `account_name`: `VARCHAR(140)` — Display title
* `account_number`: `VARCHAR(20)` — Code identifier
* `parent_account`: `VARCHAR(140)` — Foreign Key to `tabAccount.name`
* `is_group`: `TINYINT(1) DEFAULT 0` — `1` for group nodes, `0` for leaf postable accounts
* `root_type`: `ENUM('Asset', 'Liability', 'Equity', 'Income', 'Expense')`
* `account_type`: `VARCHAR(50)` — `Bank`, `Cash`, `Receivable`, `Payable`, `Tax`, etc.
* `account_currency`: `VARCHAR(10)` — Foreign Key to `tabCurrency.name`
* `company`: `VARCHAR(140)` — Foreign Key to `tabCompany.name`
* `lft`: `INT(11)` — Nested Set model left index for high-performance subtree aggregation
* `rgt`: `INT(11)` — Nested Set model right index
* `creation`, `modified`, `modified_by`: Standard Audit Timestamp and User tracking

#### `tabGL Entry` (Immutable Financial Transaction Store)
* `name` **(PK)**: `VARCHAR(140)` — Auto-increment GUID / Transaction Hash
* `posting_date`: `DATE` — Financial value posting date (Indexed)
* `transaction_date`: `DATE` — Physical system timestamp
* `account`: `VARCHAR(140)` — Foreign Key to `tabAccount.name` (Indexed)
* `party_type`: `VARCHAR(50)` — `Customer`, `Supplier`, `Employee`
* `party`: `VARCHAR(140)` — Sub-ledger entity identifier (Indexed)
* `cost_center`: `VARCHAR(140)` — Foreign Key to `tabCost Center.name`
* `debit`: `DECIMAL(18,6) DEFAULT 0.000000` — Debit amount in company base currency
* `credit`: `DECIMAL(18,6) DEFAULT 0.000000` — Credit amount in company base currency
* `account_currency`: `VARCHAR(10)` — ISO Currency code of transaction leg
* `debit_in_account_currency`: `DECIMAL(18,6)`
* `credit_in_account_currency`: `DECIMAL(18,6)`
* `voucher_type`: `VARCHAR(50)` — Originating DocType (e.g., `'Sales Invoice'`, `'Payment Entry'`, `'Journal Entry'`)
* `voucher_no`: `VARCHAR(140)` — Compound Index `(voucher_type, voucher_no)`
* `is_cancelled`: `TINYINT(1) DEFAULT 0` — Cancellation flag
* `fiscal_year`: `VARCHAR(20)` — Active accounting fiscal year identifier
* `company`: `VARCHAR(140)` — Foreign Key to `tabCompany.name`

#### `tabSales Invoice` (AR Document Header)
* `name` **(PK)**: `VARCHAR(140)` — Form sequence (e.g., `"ACC-SINV-2026-00001"`)
* `customer`: `VARCHAR(140)` — Foreign Key to `tabCustomer.name`
* `posting_date`: `DATE` — Accrual posting date
* `due_date`: `DATE` — Invoice payment maturity date
* `company`: `VARCHAR(140)` — Foreign Key to `tabCompany.name`
* `currency`: `VARCHAR(10)` — Billing currency
* `conversion_rate`: `DECIMAL(18,9) DEFAULT 1.0` — FX Rate to base currency
* `net_total`: `DECIMAL(18,6)` — Line items subtotal
* `total_taxes_and_charges`: `DECIMAL(18,6)` — Total calculated output taxes
* `grand_total`: `DECIMAL(18,6)` — Total gross billable amount
* `outstanding_amount`: `DECIMAL(18,6)` — Active unpaid balance
* `docstatus`: `TINYINT(1) DEFAULT 0` — `0 = Draft`, `1 = Submitted / Active`, `2 = Cancelled`
* `status`: `ENUM('Draft', 'Unpaid', 'Paid', 'Partially Paid', 'Overdue', 'Cancelled')`

#### `tabPayment Entry` (Treasury Document)
* `name` **(PK)**: `VARCHAR(140)` — Form sequence (e.g., `"ACC-PAY-2026-00001"`)
* `payment_type`: `ENUM('Receive', 'Pay', 'Internal Transfer')`
* `party_type`: `VARCHAR(50)` — Dynamic party master type
* `party`: `VARCHAR(140)` — Dynamic party master reference
* `paid_from`: `VARCHAR(140)` — Foreign Key to `tabAccount.name`
* `paid_to`: `VARCHAR(140)` — Foreign Key to `tabAccount.name`
* `paid_amount`: `DECIMAL(18,6)` — Source amount
* `received_amount`: `DECIMAL(18,6)` — Destination amount
* `unallocated_amount`: `DECIMAL(18,6)` — Retained unapplied cash balance
* `reference_no`: `VARCHAR(100)` — Cheque / Wire reference ID
* `reference_date`: `DATE` — Clearance / Issue date
* `clearance_date`: `DATE` — Bank statement clearance date
* `docstatus`: `TINYINT(1) DEFAULT 0`

---

## 9. Role-Based Access Control (RBAC) Permission Matrix

### Defined Roles
* **CA:** Chief Accountant / Financial Controller
* **AO:** Accounts Officer / Junior Accountant
* **AP:** Accounts Payable Clerk
* **AR:** Accounts Receivable / Billing Clerk
* **AUD:** External / Internal Auditor

| Form / DocType | CA | AO | AP | AR | AUD | Specific Permission Codes |
|---|:---:|:---:|:---:|:---:|:---:|---|
| **Chart of Accounts** | `CRUD+P` | `R` | `R` | `R` | `R` | `ACC_COA_CREATE`, `ACC_COA_UPDATE`, `ACC_COA_DELETE` |
| **Journal Entry** | `CRUD+S+C` | `CRUD+S` | `R` | `R` | `R` | `ACC_JE_CREATE`, `ACC_JE_SUBMIT`, `ACC_JE_CANCEL` |
| **Sales Invoice** | `CRUD+S+C` | `CRUD+S` | `R` | `CRUD+S` | `R` | `ACC_SINV_WRITE`, `ACC_SINV_SUBMIT`, `ACC_SINV_CANCEL` |
| **Purchase Invoice** | `CRUD+S+C` | `CRUD+S` | `CRUD+S` | `R` | `R` | `ACC_PINV_WRITE`, `ACC_PINV_SUBMIT`, `ACC_PINV_CANCEL` |
| **Payment Entry** | `CRUD+S+C` | `CRUD+S` | `CRUD` | `CRUD` | `R` | `ACC_PAY_CREATE`, `ACC_PAY_SUBMIT`, `ACC_PAY_CANCEL` |
| **Bank Reconciliation**| `CRUD+S` | `CRUD+S` | — | — | `R` | `ACC_BANK_RECON_EXECUTE` |
| **Budget Master** | `CRUD+S+C` | `R` | — | — | `R` | `ACC_BUDGET_ADMIN` |
| **Period Closing** | `CRUD+S+C` | — | — | — | `R` | `ACC_YEAR_END_CLOSE` |
| **Financial Reports** | `View+Exp` | `View+Exp`| `View` | `View` | `View+Exp` | `ACC_REPORT_FINANCIAL_VIEW`, `ACC_REPORT_EXPORT` |

> **Legend:**  
> `C` = Create, `R` = Read, `U` = Update, `D` = Delete, `S` = Submit (Post to General Ledger), `C` = Cancel (Reverse Ledger Postings), `P` = Freeze/Unfreeze Permissions, `Exp` = Export to Excel/PDF.

---

## 10. Financial Reporting Specifications

### 10.1. General Ledger (GL) Report
* **Purpose:** Detailed chronological audit trail of all debit and credit entries affecting selected account heads over a date range.
* **Input Parameters:** `company` (Required), `accounts` (Multi-select), `from_date` (Required), `to_date` (Required), `cost_center`, `party_type`, `party`, `include_cancelled` (Boolean).
* **Core Query Logic:**
```sql
SELECT 
    posting_date, 
    account, 
    party_type, 
    party, 
    voucher_type, 
    voucher_no, 
    debit, 
    credit, 
    (debit - credit) AS net_amount, 
    user_remark 
FROM tabGLEntry
WHERE company = :company 
  AND is_cancelled = 0
  AND account IN (:accounts)
  AND posting_date BETWEEN :from_date AND :to_date
ORDER BY posting_date ASC, creation ASC;
```

* **Balance Computations:**
  * **Opening Balance:**
    $$\text{Opening Balance} = \sum \text{Debit} - \sum \text{Credit} \quad \forall \text{ entries where } \text{posting\_date} < \text{from\_date}$$
  * **Running Balance:**
    $$\text{Running Balance}_i = \text{Balance}_{i-1} + \text{Debit}_i - \text{Credit}_i$$
  * **Closing Balance:**
    $$\text{Closing Balance} = \text{Opening Balance} + \sum \text{Debits} - \sum \text{Credits}$$

---

### 10.2. Trial Balance
* **Purpose:** Verifies global arithmetic equality of all debits and credits across the Chart of Accounts as of a specific date.
* **Layout & Columns:** `Account Number` | `Account Name` | `Opening (Dr/Cr)` | `Period Debit` | `Period Credit` | `Closing (Dr/Cr)`.
* **Hierarchy Rollup:** Group node balance is the recursive sum of all child leaves:
  $$\text{Balance}_{\text{Group}} = \sum_{c \in \text{Children}} \text{Balance}_c$$
* **Balancing Assertion:**
  $$\sum \text{Closing Debits} \equiv \sum \text{Closing Credits}$$

---

### 10.3. Profit and Loss Statement (Income Statement)
* **Purpose:** Measures financial performance and net income/loss over an accounting interval.
* **Structure & Formula:**
  1. **Operating Revenue (Income):** Gross Sales Revenue, Service Revenue, less Sales Returns.
  2. **Cost of Goods Sold (COGS):** Direct Materials, Direct Labor, Production Overheads.
  3. **Gross Profit:**
     $$\text{Gross Profit} = \text{Total Revenue} - \text{Total COGS}$$
  4. **Operating Expenses (OPEX):** Administrative, Selling, Marketing, Depreciation.
  5. **Operating Income (EBIT):**
     $$\text{Operating Income (EBIT)} = \text{Gross Profit} - \text{Total OPEX}$$
  6. **Other Income / Expenses:** Interest Income/Expense, Taxes, Realized/Unrealized Forex Gains/Losses.
  7. **Net Profit / (Loss):**
     $$\text{Net Profit} = \text{EBIT} + \text{Other Income} - \text{Interest} - \text{Taxes}$$

---

### 10.4. Balance Sheet
* **Purpose:** Snapshot of financial position (Assets, Liabilities, and Equity) at a point in time.
* **Fundamental Accounting Equation Assertion:**
  $$\text{Total Assets} \equiv \text{Total Liabilities} + \text{Total Equity} + \text{Provisional Profit (Unclosed P\&L)}$$
* **Asset Breakdown:**
  * **Current Assets:** Bank, Petty Cash, Accounts Receivable, Inventory in Hand.
  * **Non-Current Assets:** Property, Plant & Equipment less Accumulated Depreciation, Intangibles.
* **Liability Breakdown:**
  * **Current Liabilities:** Accounts Payable, Accrued Taxes, Short-term Borrowings.
  * **Long-Term Liabilities:** Long-term Loans, Deferred Tax Liabilities.
* **Equity Breakdown:** Paid-up Capital, Share Premium, Reserves, Retained Earnings.

---

## 11. Final Technical Deliverables & System Architecture

### A. RESTful API Architecture

```http
BASE URL: /api/v1/accounting/

# Chart of Accounts
GET    /accounts?company={id}&tree=true     # Fetch COA hierarchical tree
POST   /accounts                            # Create new Account Head
PUT    /accounts/{id}                       # Update Account Head (name, freeze)
DELETE /accounts/{id}                       # Delete Account (only if no GL entries exist)

# Journal Entries
GET    /journal-entries/{id}                # Fetch full Journal Entry document
POST   /journal-entries                     # Create Draft Journal Entry
PUT    /journal-entries/{id}                # Modify Draft Journal Entry
POST   /journal-entries/{id}/submit         # Post to General Ledger (docstatus=1)
POST   /journal-entries/{id}/cancel         # Reverse GL entries (docstatus=2)

# Invoicing (AR & AP)
POST   /sales-invoices                      # Create Draft Sales Invoice
POST   /sales-invoices/{id}/submit          # Post Sales Invoice to GL
POST   /purchase-invoices                   # Create Draft Purchase Invoice
POST   /purchase-invoices/{id}/submit       # Post Purchase Invoice to GL

# Payments & Clearing
POST   /payment-entries                     # Create Payment Entry
POST   /payment-entries/{id}/submit         # Post Payment to GL and allocate
POST   /payments/reconcile                  # Execute batch reconciliation on open items

# Financial Reports
GET    /reports/general-ledger              # Run General Ledger query
GET    /reports/trial-balance               # Generate Trial Balance
GET    /reports/profit-and-loss             # Generate P&L statement
GET    /reports/balance-sheet               # Generate Balance Sheet
```

---

### B. Frontend Engineering Requirements (UI / UX)

#### 1. Layout Architecture
* **Document Form View:** Two-column metadata header (Party, Dates, Currency, Status Badge) with a full-width bottom section for Line Item and Tax Grids.
* **Real-time Computation:** Grid changes (Quantity, Rate, Tax Template) compute net total, line-level tax splits, and grand totals client-side before sending payloads to `/calculate` endpoints.
* **Status Indicator Ribbon:** Visual state pills for document lifecycles:
  $$\text{Draft (Grey)} \longrightarrow \text{Submitted / Unpaid (Orange)} \longrightarrow \text{Paid (Green)} \longrightarrow \text{Cancelled (Red)}$$

#### 2. Master-Detail Dynamic Grid Specification
* **Inline Lookup Fields:** Typeahead async auto-completion for Item Code, Account Head, and Cost Center.
* **Dynamic Column Visibility:** Selecting `payment_type = 'Internal Transfer'` hides party and receivable fields and switches line prompts to `Source Account` and `Target Account`.
* **Validation Alerts:** If $\sum \text{Debit} \neq \sum \text{Credit}$ in a Journal Entry, render a red badge displaying the exact imbalance:
  $$\Delta = \left| \sum \text{Debit} - \sum \text{Credit} \right|$$
  Disable the **Submit** button until $\Delta = 0.00$.

---

### C. Implementation Prioritization Matrix

```text
[P0: Core Engine] ────────► [P1: Trading & Operations] ─────► [P2: Treasury & Control] ─────► [P3: Advanced & Closing]
• Company & Base Currencies   • Sales Invoices & Credit Notes  • Bank Reconciliation Tool     • Deferred Revenue / Expense
• Chart of Accounts Tree      • Purchase Invoices & Debits     • Payment Reconcile Utility    • Fixed Asset Lifecycle
• Journal Entry Engine        • Payment Entry (Pay / Receive)  • Multi-Currency Forex Gain    • Period Closing Automation
• GL Posting Table & Ledger   • Basic Tax Templates            • Budget Control Engine        • Subscription Billing
```

| Phase | Priority | Modules / Components | Key Dependencies | Functional Deliverables |
|---|:---:|---|---|---|
| **Phase 1** | **P0** *(Core / Mandatory)* | Company Master, COA Tree, Fiscal Year, Journal Entry Engine, GL Ledger Posting Table | Relational DB Engine | Double-entry foundation, debit/credit balancing assertion, GL running balance generation. |
| **Phase 2** | **P1** *(Operational)* | Sales Invoice, Purchase Invoice, Payment Entry, Taxes & Charges Templates | Phase 1 (COA, GL) | Order-to-cash and procure-to-pay posting, customer/vendor sub-ledgers. |
| **Phase 3** | **P2** *(Control & Treasury)* | Bank Reconciliation, Multi-Currency FX Realization, Budgeting, AR/AP Aging | Phase 1 & 2 | Bank statement import, currency translation gains/losses, automated spending caps. |
| **Phase 4** | **P3** *(Advanced / Year-End)* | Fixed Assets & Depreciation, Deferred Accounting, Year-End Period Closing | Phase 1, 2 & 3 | Straight-line asset schedules, monthly accruals, period closing vouchers to Retained Earnings. |

---

### D. Architectural Traceability Summary

```text
                ┌──────────────────────────────────────────────┐
               │   Frappe ERPNext Accounting Reference Core   │
               └──────────────────────┬───────────────────────┘
                                      │
    ┌─────────────────────────────────┼────────────────────────────────┐
    ▼                                 ▼                                ▼
[OBSERVED FUNCTIONALITY]      [INFERRED ARCHITECTURE]        [TECHNICAL SPECIFICATION]
• COA Group & Leaf Structure   • Nested Set Indexing (lft/rgt) • Relational SQL Schema
• Real-time GL Entry Posting   • Dual-Currency Ledger Engine   • RESTful API Endpoints
• Invoice & Payment Life-cycle • Atomic Posting Hooks          • Master-Detail UI Layouts
• Bank Reconciliation Utility  • Strict Imbalance Rejections   • RBAC Matrix & Priority Map
```

* **Observed Functionality:** Chart of Accounts tree models, multi-currency invoice and payment creation, automated tax application, manual/automated journal generation, fixed asset depreciation schedules, and bank statement matching.
* **Inferred Architecture:** Optimized relational schema with foreign key constraints, nested set tree indexing (`lft`, `rgt`) for account rollups, double-entry balancing assertions ($\Delta = 0.00$), and immutable General Ledger transaction tables.
* **Replication Target:** Enables engineering teams to build a full-featured, GAAP/IFRS-compliant ERP Accounting Engine from scratch across the .NET backend and Angular frontend stack.

---

## 12. Implementation Verification Report

> **Verified Against:** FIN module backend (14 services, 20 controllers, 12 entities) + Angular frontend (19 routes, 43 files)
> **Verification Date:** 2026-08-24
> **Module Architecture:** Modular ERP — invoicing in SAL/PUR, inventory in INV, payroll in HRM. FIN receives GL impact through a decoupled outbox posting engine (`FinPostingService`).

---

### 12.1. Form-by-Form Verification Matrix

**Legend:**
- ✅ **Implemented** — form exists and covers the business requirement
- ⚠️ **Partial** — form exists but some doc features are missing or simplified
- 🔄 **Relocated** — business function exists but in a different module (SAL/PUR/SYS/INV)
- ❌ **Not Implemented** — form/feature does not exist anywhere in the codebase
- ➕ **Extra** — implemented but not in the original business doc (logical addition)

#### A. Master & Setup Data

| Doc Form | Status | Implementation | Notes |
|---|:---:|---|---|
| Company & Global Settings | 🔄 | SYS module (`sys_company`) | Correctly owned by SYS, not FIN |
| **Chart of Accounts (COA)** | ✅ | `FIN_1001` (Account Master) + `FIN_1002` (Account Group) | Split into Account Master + Account Group hierarchy |
| Fiscal Year & Periods | 🔄 | SYS module (`sys_fin_year`, `sys_fin_year_dtl`) + `FIN_1401` manages period status | Calendar setup is SYS; period locking/closing is FIN_1401 |
| Cost Center & Dimensions | 🔄 | SYS module. Referenced in voucher lines via `cost_center_no` | FIN enforces `requires_cost_center` flag per account |
| Currency & Exchange | 🔄 | SYS module (`sys1004` Currency form) | FIN references via `currency_no` and `ICurrencyLookup` |
| **Mode of Payment** | ❌ | Not implemented | Handled through voucher types (Payment/Receipt/Contra) instead |
| **Payment Terms Template** | ❌ | Not implemented | SAL module has `due_date` but no dedicated template form |

#### B. General Ledger & Adjustments

| Doc Form | Status | Implementation | Notes |
|---|:---:|---|---|
| **Journal Entry** | ✅ | `FIN_1101` (Voucher Entry) | Covers Journal (1), Payment (2), Receipt (3), Contra (4), Sales (5), Purchase (6), Opening (7), Closing (8) via `base_kind` |
| **Period Closing Voucher** | ✅ | `FIN_1401` | Year-end close zeroes P&L → Retained Earnings. Period status management included |
| **Opening Invoice Tool** | ❌ | Not implemented | `FIN_1004` handles opening *balances* (not invoices) |

#### C. Accounts Receivable (AR)

| Doc Form | Status | Implementation | Notes |
|---|:---:|---|---|
| Sales Invoice | 🔄 | SAL module (`Sal1001Service`) | GL posting via outbox → `FinPostingService` |
| Sales Taxes Template | 🔄 | SAL/SYS modules (VAT tax setup) | Tax calculation in SAL; GL leg mapping via FIN_1006 |
| **Payment Request & Gateway** | ❌ | Not implemented | No online payment link or gateway integration |
| **Dunning / Payment Reminders** | ❌ | Not implemented | No overdue notice or collection tracking |

#### D. Accounts Payable (AP)

| Doc Form | Status | Implementation | Notes |
|---|:---:|---|---|
| Purchase Invoice | 🔄 | PUR module | GL posting via outbox → `FinPostingService` |
| Purchase Taxes Template | 🔄 | PUR/SYS modules | Same pattern as Sales Taxes |

#### E. Payments & Treasury Management

| Doc Form | Status | Implementation | Notes |
|---|:---:|---|---|
| **Payment Entry** | ⚠️ | `FIN_1101` (base_kind 2=Payment, 3=Receipt, 4=Contra) | Merged into Voucher Entry. Missing: invoice allocation table, `unallocated_amount`, dual exchange rates |
| **Payment Reconciliation Tool** | ❌ | Not implemented | FIN_1202/1203 provides sub-ledger *audit* but not *clearing* |
| **Bank Account Master** | ✅ | `FIN_1005` | Full implementation with GL link, 1:1 enforcement, routing/swift |
| **Bank Statement Import** | ❌ | Not implemented | No CSV/OFX/MT940/CAMT.053 parser |
| **Bank Reconciliation Tool** | ✅ | `FIN_1102` | Manual ledger-line matching. Missing: auto-match, statement import |

#### F. Budgeting, Accruals, Fixed Assets, Equity & Subscriptions

| Doc Form | Status | Notes |
|---|:---:|---|
| **Budget Master** | ❌ | No spending limits, Stop/Warn/Ignore matrix, or PO commitment tracking |
| **Deferred Revenue Processing** | ❌ | No ASC 606/IFRS 15 monthly amortization |
| **Deferred Expense Processing** | ❌ | No prepaid expense amortization |
| **Asset Category** | ❌ | No fixed asset module |
| **Asset Master & Depreciation** | ❌ | No SLM/WDV schedules |
| **Asset Depreciation Entry** | ❌ | No automated Dr Depreciation Exp / Cr Accumulated Dep |
| **Asset Value Adjustment** | ❌ | — |
| **Asset Disposal / Scrapping** | ❌ | No gain/loss on sale |
| **Shareholder & Share Transfer** | ❌ | No equity registry |
| **Subscription Billing** | ❌ | No recurring invoice generator |

#### G. Financial Reporting

| Doc Form | Status | Implementation | Notes |
|---|:---:|---|---|
| **General Ledger Report** | ✅ | `FIN_1302` | Opening → Running → Closing balance |
| **Trial Balance** | ✅ | `FIN_1301` | Per-account Dr/Cr sums, balancing assertion |
| **Profit & Loss** | ✅ | `FIN_1304` | Date-windowed. Revenue − Expense = Net Profit |
| **Balance Sheet** | ✅ | `FIN_1305` | Assets = Liabilities + Equity + Current Year Earnings |
| **Cash Flow Statement** | ✅ | `FIN_1306` | Direct method over Bank/Cash control accounts |

#### H. Extra Forms (Implemented but Not in Original Business Doc)

| Form | Purpose | Assessment |
|---|---|---|
| **FIN_1002** Account Group | Separate group hierarchy management | **Logical** — cleaner than flat parent_account tree |
| **FIN_1003** Voucher Type | Configurable voucher categories | **Logical** — replaces hardcoded `voucher_type` enum |
| **FIN_1006** GL Mapping | Auto-posting account resolution rules | **Logical** — essential for decoupled outbox architecture |
| **FIN_1201** Posting Monitor | Event outbox visibility & re-drive | **Logical** — operational necessity for async GL posting |
| **FIN_1202** AP Sub-ledger Recon | GL vs PUR sub-ledger audit | **Logical** — catches posting discrepancies |
| **FIN_1203** AR Sub-ledger Recon | GL vs SAL sub-ledger audit | **Logical** — catches posting discrepancies |
| **FIN_1303** Day Book | Daily chronological audit trail | **Logical** — standard accounting report |
| **FIN_1307** AR/AP Aging | Party-level aging buckets | **Logical** — standard aging analysis |
| **FIN_1308** COA Export | PDF/Excel chart of accounts | **Logical** — audit requirement |

---

### 12.2. Field-Level Verification: Chart of Accounts

#### Doc (§3.1 `tabAccount`) vs Implementation (`fin_account` + `fin_account_group`)

| Doc Field | Doc Type | Impl Field | Impl Entity | Match | Notes |
|---|---|---|---|:---:|---|
| `account_name` | String(140) | `account_name` | `fin_account` | ✅ | Max 200 in impl |
| `account_number` | String(20) | `account_code` | `fin_account` | ✅ | Max 30. Auto-generated via `IDocSequenceGenerator` |
| `parent_account` | Link | `account_group_no` | `fin_account` | ⚠️ | **Design difference**: Doc uses nested tree. Impl uses flat accounts under group hierarchy |
| `is_group` | Boolean | `is_postable` | `fin_account` | ✅ | Inverted: `is_group=1` ↔ `is_postable=0` |
| `root_type` | Select (5) | `root_type` | `fin_account` | ✅ | Doc: string enum. Impl: short 1–5 |
| `account_type` | Select | `control_type` | `fin_account` | ✅ | 1=AR, 2=AP, 3=Bank, 4=Cash, 5=Inventory, 6=Tax, 7=Retained-Earnings |
| `account_currency` | Link | `currency_no` | `fin_account` | ✅ | FK to currency master |
| `freeze_account` | Select | `is_active` | `fin_account` | ✅ | Inverted: `freeze="Yes"` ↔ `is_active=0` |
| `company` | Link | `company_no` | `fin_account` | ✅ | Mandatory BIGINT NOT NULL |
| `lft` / `rgt` | INT | — | — | ❌ | Nested set not used. Group-based joins instead |
| — | — | `normal_balance` | `fin_account` | ➕ | Explicit dr/cr flag derived from `root_type` |
| — | — | `requires_cost_center` | `fin_account` | ➕ | Per-account cost center enforcement |
| — | — | `requires_party` | `fin_account` | ➕ | Per-account party enforcement. Auto-set for AR/AP |
| — | — | `opening_balance` | `fin_account` | ➕ | Migration seed value. Zeroed after FIN_1004 posting |
| — | — | `branch_no` | `fin_account` | ➕ | Multi-branch support |

---

### 12.3. Field-Level Verification: Journal Entry / Voucher Entry

#### Doc (§3.4 `tabJournal Entry`) vs Implementation (`fin_voucher` + `fin_voucher_dtl`)

| Doc Field | Doc Type | Impl Field | Impl Entity | Match |
|---|---|---|---|:---:|
| `voucher_type` | Select | `voucher_type_no` → `base_kind` | `fin_voucher` → `fin_voucher_type` | ✅ |
| `posting_date` | Date | `voucher_date` | `fin_voucher` | ✅ |
| `company` | Link | `company_no` | `fin_voucher` | ✅ |
| `accounts` table | Table | `fin_voucher_dtl` | Child table | ✅ |
| `account` (row) | Link | `account_no` | `fin_voucher_dtl` | ✅ |
| `party_type` (row) | Select | `party_type` | `fin_voucher_dtl` | ✅ |
| `party` (row) | Dynamic | `party_no` | `fin_voucher_dtl` | ✅ |
| `debit_in_account_currency` | Currency | `debit_fc` | `fin_voucher_dtl` | ✅ |
| `credit_in_account_currency` | Currency | `credit_fc` | `fin_voucher_dtl` | ✅ |
| `cost_center` (row) | Link | `cost_center_no` | `fin_voucher_dtl` | ✅ |
| `total_debit` | Currency | `total_debit` | `fin_voucher` | ✅ |
| `total_credit` | Currency | `total_credit` | `fin_voucher` | ✅ |
| `user_remark` | Long Text | `narration` | `fin_voucher` | ✅ |
| — | — | `fin_year_no` | `fin_voucher` | ➕ |
| — | — | `fin_period_no` | `fin_voucher` | ➕ |
| — | — | `reference_no` | `fin_voucher` | ➕ |
| — | — | `source_module` / `source_doc_type` / `source_doc_no` | `fin_voucher` | ➕ |
| — | — | `approval_request_no` | `fin_voucher` | ➕ |
| — | — | `vat_tax_no` | `fin_voucher_dtl` | ➕ |
| — | — | `against_voucher_no` | `fin_voucher_dtl` | ➕ |

---

### 12.4. Field-Level Verification: Payment Entry

#### Doc (§3.3 `tabPayment Entry`) vs Implementation

| Doc Field | Status | Implementation | Gap Detail |
|---|:---:|---|---|
| `payment_type` (Receive/Pay/Transfer) | ✅ | Voucher type `base_kind` 2/3/4 | Different mechanism, same result |
| `party_type` / `party` | ✅ | `fin_voucher_dtl.party_type` / `party_no` | Per-line, not header-level |
| `paid_from` / `paid_to` | ⚠️ | Voucher lines with dr/cr accounts | No dedicated header-level from/to |
| `paid_amount` / `received_amount` | ⚠️ | `total_debit` / `total_credit` | Works but less user-friendly |
| `source_exchange_rate` / `target_exchange_rate` | ⚠️ | Single `fx_rate` on header | Doc has dual-rate for cross-currency |
| `references` (invoice allocation table) | ❌ | **Not implemented** | `against_voucher_no` exists but no allocation UI |
| `allocated_amount` (per invoice) | ❌ | **Not implemented** | No partial invoice clearing |
| `unallocated_amount` | ❌ | **Not implemented** | No advance/unapplied tracking |
| `reference_no` | ✅ | `fin_voucher.reference_no` | |
| `reference_date` | ❌ | **Not implemented** | No separate value/clearance date |

---

### 12.5. Business Logic Verification

#### A. Debit / Credit Integrity (§5.1)

| Rule | Status | Implementation |
|---|:---:|---|
| Zero Imbalance: ΣDr − ΣCr = 0 | ✅ | `Math.Abs(dr - cr) > 0.0001m` → reject. Enforced on save AND post |
| Negative amounts blocked | ✅ | `debit < 0` or `credit < 0` → `ValidationException` |
| Either debit or credit, not both | ✅ | `debit > 0 && credit > 0` → reject |

#### B. Account Hierarchy & Posting (§5.2)

| Rule | Status | Implementation |
|---|:---:|---|
| Leaf-only posting | ✅ | `is_postable != 1` → reject |
| AR/AP account type matching | ✅ | `control_type` enforces `requires_party = 1` |
| Bank/Cash for treasury legs | ✅ | FIN_1005 enforces `control_type IN (3,4)` |

#### C. Multi-Currency & Forex (§5.3)

| Rule | Status | Implementation |
|---|:---:|---|
| Dual-currency tracking | ⚠️ | `debit_fc`/`credit_fc` + `debit`/`credit` exist but FIN_1101 currently **rejects `fx_rate != 1.0`** |
| Forex Gain/Loss realization | ❌ | GL mapping has `FX_GAIN`/`FX_LOSS` leg keys but no service computes the delta |

#### D. Fiscal Year & Period Locking (§5.4)

| Rule | Status | Implementation |
|---|:---:|---|
| Period boundary enforcement | ✅ | `IFinCalendar.FindYearForDateAsync` → reject if no match |
| Period-open guard on posting | ✅ | `period_status != 1` → reject with named period in error |
| Year-end closing lock | ✅ | `FIN_1401` locks all periods to status 3 |
| Locked period irreversibility | ✅ | Status 3 can never be re-opened from UI |

#### E. Budgetary Control (§5.5)

| Rule | Status | Implementation |
|---|:---:|---|
| Budget evaluation hook | ❌ | **Not implemented**. No spending caps or Stop/Warn/Ignore matrix |

---

### 12.6. Accounting Entries Verification

| Entry Pattern | Doc Section | Status | Implementation |
|---|---|:---:|---|
| Dr AR / Cr Revenue / Cr VAT (Sales) | §6.1 | ✅ | SAL → outbox → `FinPostingService` resolves via GL maps |
| Dr COGS / Cr Inventory | §6.1 | ✅ | INV module posts inventory GL events |
| Credit Note (reversed signs) | §6.1 | ✅ | `SalesReturnPosted` event type |
| Dr Expense / Dr Input VAT / Cr AP (Purchase) | §6.2 | ✅ | PUR → outbox → `FinPostingService` |
| Dr Bank / Cr AR (Customer receipt) | §6.3 | ✅ | `CustomerReceiptPosted` event |
| Dr AP / Cr Bank (Supplier payment) | §6.3 | ✅ | `SupplierPaymentPosted` event |
| Contra (Bank ↔ Cash) | §6.3 | ✅ | Manual voucher with base_kind=4 |
| Cash discount + FX gain/loss | §6.3 | ❌ | No auto-calculated discount or forex delta |
| Year-End: Dr Revenue / Cr Expense / Cr RE | §6.4 | ✅ | `FIN_1401.CloseFiscalYearAsync` |
| Dr Depreciation / Cr Accumulated Dep | §6.4 | ❌ | No fixed asset module |
| Deferred Revenue/Expense amortization | §6.4 | ❌ | Not implemented |

---

### 12.7. Database Architecture Comparison

| Aspect | Doc Design | Implementation | Assessment |
|---|---|---|---|
| **Primary Keys** | `VARCHAR(140) name` (ERPNext GUID) | `BIGSERIAL identity` | ✅ Better — numeric PKs for performance |
| **Table Naming** | `tabAccount`, `tabGL Entry` | `fin_account`, `fin_ledger` | ✅ Better — module-prefixed snake_case |
| **Lifecycle** | `docstatus` (0/1/2) | `status` (1=Draft, 2=Posted, 3=Cancelled) | ✅ Equivalent |
| **Hierarchy** | Nested Set (`lft`/`rgt`) | Group-based (`fin_account_group`) | ✅ Simpler — sufficient for SME |
| **Soft Delete** | `docstatus = 2` | `is_deleted` + audit fields | ✅ Separate from status |
| **Immutability** | `is_cancelled` flag | Append-only ledger + `is_reversal=1` rows | ✅ Stronger guarantee |
| **Company Isolation** | `company VARCHAR(140)` | `company_no BIGINT NOT NULL` | ✅ Mandatory on every table |

---

### 12.8. Coverage Summary

| Category | Doc Forms | Implemented | Relocated | Missing | Extra |
|---|:---:|:---:|:---:|:---:|:---:|
| **Master & Setup** | 7 | 2 | 4 | 1 | 3 |
| **GL & Adjustments** | 3 | 2 | — | 1 | — |
| **AR** | 4 | — | 2 | 2 | — |
| **AP** | 2 | — | 2 | — | — |
| **Payments & Treasury** | 5 | 2 | — | 3 | — |
| **Budgeting** | 1 | — | — | 1 | — |
| **Accruals** | 2 | — | — | 2 | — |
| **Fixed Assets** | 5 | — | — | 5 | — |
| **Equity & Subscriptions** | 2 | — | — | 2 | — |
| **Reporting** | 5 | 5 | — | — | 3 |
| **TOTAL** | **36** | **11** | **8** | **17** | **6** |

**Core accounting flow status:** ✅ **COMPLETE**

```text
Setup (Groups → COA → Voucher Types → Bank Accounts → GL Maps)
  → Opening Balances (FIN_1004)
    → Transactions (Vouchers: Journal/Payment/Receipt/Contra)
      → Auto-Posting (Outbox → FinPostingService → GL)
        → Reconciliation (Bank Recon + Sub-ledger Audit)
          → Reports (TB, GL, Day Book, P&L, BS, Cash Flow, Aging)
            → Year-End Close (FIN_1401 → Retained Earnings)
```

**Unimplemented items by priority phase (per §11C):**

| Phase | Items |
|---|---|
| **P2 — Treasury & Control** | Payment Reconciliation Tool, Bank Statement Import, Multi-Currency Forex Gain/Loss, Budget Control |
| **P3 — Advanced & Closing** | Deferred Revenue/Expense, Fixed Asset Lifecycle, Subscription Billing, Mode of Payment, Payment Terms |
| **Unprioritized** | Shareholder/Share Transfer, Dunning/Reminders, Payment Gateway, Opening Invoice Migration Tool |

The implementation fully covers **P0 (Core Engine)** and **P1 (Operational)** priorities as defined in §11C.
