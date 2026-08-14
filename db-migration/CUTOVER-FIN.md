# FIN Module — Production Cutover Runbook

**Date:** 2026-08-06
**Module:** Finance (FIN)
**Target:** sme-dotnet-backend (AidlyErp.slnx)

---

## 1. Pre-Flight Checklist

- [ ] Database backup verified and restorable
- [x] SchemaDriftTests GREEN against restored copy (11 Cause A columns remain — run migrations 1500, 1840, 0925)
- [ ] GL map coverage query returns ZERO rows
- [ ] All 17 FIN menus exist in sys_menu
- [ ] Role permissions seeded and verified

---

## 2. Migration Runbook

### Scripts to run in order against production:

| # | Script | Purpose | Expected Row Change |
|---|--------|---------|---------------------|
| 1 | `2026-06_fin_dr_cr_string_migration.sql` | Converts dr/cr flags from numeric to string | 0 (already applied if .NET code works) |
| 2 | `2026-06_fin_bank_recon_bank_date.sql` | Adds bank_date to fin_bank_recon_line | 0 (already applied if .NET code works) |
| 3 | `2026-06_fin_production_guardrails.sql` | Creates unique indexes for data integrity | 0 (index creation only) |
| 4 | `2026-08-06_1500_fin_voucher_dtl_vat_columns.sql` | Adds vat_tax_no, tax_rate_pct to fin_voucher_dtl; vat_tax_no to fin_ledger | 0 (column additions only) |
| 5 | `2026-08-06_1510_sal_pur_invoice_dtl_vat_tax_no.sql` | Adds vat_tax_no to sal_invoice_dtl and pur_invoice_dtl | 0 (column additions only) |
| 6 | `2026-08-06_1835_fin_opening_balance_single_source.sql` | **DESTRUCTIVE** — zeros opening_balance for accounts with posted FIN_OPENING vouchers | **RUN PREVIEW SELECT FIRST** |
| 7 | `2026-08-06_1840_fin_bank_recon_cleared_amounts.sql` | Adds cleared_debits/cleared_credits to fin_bank_recon | 0 (column additions only) |

### Migration 1835 — Preview Query (run FIRST):

```sql
SELECT a.account_no, a.account_code, a.account_name, a.opening_balance, a.opening_dr_cr
FROM fin_account a
WHERE a.is_deleted = 0
  AND a.opening_balance != 0
  AND EXISTS (SELECT 1 FROM fin_voucher v
              WHERE v.company_no = a.company_no
                AND v.source_doc_type = 'FIN_OPENING'
                AND v.status = 2 AND v.is_deleted = 0);
```

**If this returns rows:** Those accounts were being double-counted in every report. The migration will zero them. Historical reports were wrong.

### Idempotency:

All scripts use `IF NOT EXISTS` or `ADD COLUMN IF NOT EXISTS`. Running the full set twice is safe — the second pass is a no-op.

---

## 3. GL Map Seed

### Emitter Verification (from code extraction):

| Event Type | Source Module | Legs |
|------------|--------------|------|
| `PayrollPosted` | HRM_1202 | EXPENSE(dr), PAYABLE(cr), STATUTORY(cr) |
| `FinalSettlementPosted` | HRM_1207 | EXPENSE(dr), PAYABLE(cr), DEDUCTION(cr) |
| `SalesInvoicePosted` | SAL_1001 | RECEIVABLE(dr), CASH(dr), REVENUE(cr), VAT_OUTPUT(cr), COGS(dr), INVENTORY(cr) |
| `CustomerReceiptPosted` | SAL_1102 | CASH(dr), RECEIVABLE(cr) |
| `SalesReturnPosted` | SAL_1103 | SALES_RETURN(dr), VAT_OUTPUT(dr), RECEIVABLE(cr) |
| `PurchaseInvoicePosted` | PUR_1102 | INVENTORY(dr), VAT_INPUT(dr), PAYABLE(cr) |
| `SupplierPaymentPosted` | PUR_1104 | PAYABLE(dr), CASH(cr) |
| `OpeningStockPosted` | INV_1101 | INVENTORY(dr), OPENING_EQUITY(cr) |
| `StockAdjustmentPosted` | INV_1102 | INVENTORY/ADJUSTMENT (direction varies) |
| `StockAdjustmentReversed` | INV_1102 | INVENTORY/ADJUSTMENT (mirrored) |

### Coverage Query:

```sql
-- Run this to find unmapped (event_type, leg_key) pairs
WITH emitted AS (
    SELECT 'PayrollPosted' AS event_type, 'EXPENSE' AS leg_key UNION ALL
    SELECT 'PayrollPosted', 'PAYABLE' UNION ALL
    SELECT 'PayrollPosted', 'STATUTORY' UNION ALL
    SELECT 'FinalSettlementPosted', 'EXPENSE' UNION ALL
    SELECT 'FinalSettlementPosted', 'PAYABLE' UNION ALL
    SELECT 'FinalSettlementPosted', 'DEDUCTION' UNION ALL
    SELECT 'SalesInvoicePosted', 'RECEIVABLE' UNION ALL
    SELECT 'SalesInvoicePosted', 'CASH' UNION ALL
    SELECT 'SalesInvoicePosted', 'REVENUE' UNION ALL
    SELECT 'SalesInvoicePosted', 'VAT_OUTPUT' UNION ALL
    SELECT 'SalesInvoicePosted', 'COGS' UNION ALL
    SELECT 'SalesInvoicePosted', 'INVENTORY' UNION ALL
    SELECT 'CustomerReceiptPosted', 'CASH' UNION ALL
    SELECT 'CustomerReceiptPosted', 'RECEIVABLE' UNION ALL
    SELECT 'SalesReturnPosted', 'SALES_RETURN' UNION ALL
    SELECT 'SalesReturnPosted', 'VAT_OUTPUT' UNION ALL
    SELECT 'SalesReturnPosted', 'RECEIVABLE' UNION ALL
    SELECT 'PurchaseInvoicePosted', 'INVENTORY' UNION ALL
    SELECT 'PurchaseInvoicePosted', 'VAT_INPUT' UNION ALL
    SELECT 'PurchaseInvoicePosted', 'PAYABLE' UNION ALL
    SELECT 'SupplierPaymentPosted', 'PAYABLE' UNION ALL
    SELECT 'SupplierPaymentPosted', 'CASH' UNION ALL
    SELECT 'OpeningStockPosted', 'INVENTORY' UNION ALL
    SELECT 'OpeningStockPosted', 'OPENING_EQUITY' UNION ALL
    SELECT 'StockAdjustmentPosted', 'INVENTORY' UNION ALL
    SELECT 'StockAdjustmentPosted', 'ADJUSTMENT' UNION ALL
    SELECT 'StockAdjustmentReversed', 'INVENTORY' UNION ALL
    SELECT 'StockAdjustmentReversed', 'ADJUSTMENT'
)
SELECT e.event_type, e.leg_key
FROM emitted e
LEFT JOIN fin_gl_map m ON m.event_type = e.event_type
    AND m.leg_key = e.leg_key
    AND (m.sub_key = '' OR m.sub_key IS NULL)
    AND m.is_active = 1
    AND m.is_deleted = 0
    AND m.company_no = :company_no  -- PARAMETERIZE
WHERE m.gl_map_no IS NULL;
```

**Result must be ZERO rows before go-live.**

### VAT Sub-Key Rows:

For each active tax code in `sys_vat_tax`, create:
- One `VAT_OUTPUT` row with `sub_key = tax_code`
- One `VAT_INPUT` row with `sub_key = tax_code`

Plus one generic fallback row each (empty sub_key) as safety net.

---

## 4. Menu Enrollment

### Required FIN menus (17 forms):

| form_id | route_path | Description |
|---------|------------|-------------|
| FIN_1001 | /fin/form/chart-of-accounts | Chart of Accounts |
| FIN_1002 | /fin/form/account-group | Account Group |
| FIN_1003 | /fin/form/voucher-type | Voucher Type |
| FIN_1004 | /fin/form/opening-balance | Opening Balance |
| FIN_1005 | /fin/form/bank-account | Bank Account |
| FIN_1006 | /fin/form/gl-mapping | GL Mapping |
| FIN_1101 | /fin/form/voucher-entry | Voucher Entry |
| FIN_1102 | /fin/form/bank-reconciliation | Bank Reconciliation |
| FIN_1201 | /fin/form/posting-monitor | Posting Monitor |
| FIN_1401 | /fin/form/period-close | Period Close |
| FIN_1301 | /fin/report/trial-balance | Trial Balance |
| FIN_1302 | /fin/report/account-ledger | Account Ledger |
| FIN_1303 | /fin/report/day-book | Day Book |
| FIN_1304 | /fin/report/profit-loss | Profit & Loss |
| FIN_1305 | /fin/report/balance-sheet | Balance Sheet |
| FIN_1306 | /fin/report/cash-flow | Cash Flow |
| FIN_1307 | /fin/report/aging | Aging Analysis |

---

## 5. Post-Cutover Verification

```sql
-- 1. Trial balance check (must return balanced)
SELECT SUM(debit) AS total_debit, SUM(credit) AS total_credit,
       ABS(SUM(debit) - SUM(credit)) AS difference
FROM fin_ledger
WHERE is_deleted = 0 AND company_no = :company_no;

-- 2. Zero failed outbox events
SELECT COUNT(*) FROM sys_event_outbox
WHERE status = 3 AND company_no = :company_no;

-- 3. Coverage query (must return 0 rows)
-- (run the coverage query from section 3)
```

---

## 6. Rollback Procedure

### Critical: Migration 1835 (opening_balance single source)

If migration 1835 zeroes opening_balance incorrectly:

```sql
-- Restore from the preview SELECT backup
-- (you DID run the preview SELECT and save the results, right?)
UPDATE fin_account a
SET opening_balance = b.opening_balance,
    opening_dr_cr = b.opening_dr_cr
FROM backup_fin_account_opening b
WHERE a.account_no = b.account_no;
```

### Other migrations:

All other migrations are additive (ADD COLUMN IF NOT EXISTS). Rollback is not needed — the columns can remain unused.

---

## 7. Week-One Watch Items

1. **Documents parking in FIN_1201**: Check every morning. A Failed event means a GL map is missing.
2. **Trial Balance stops balancing**: Check after every month-end close. A mismatch means a posting bug.
3. **Period will not close**: Check at month-end. A locked period cannot be re-opened from the UI.
