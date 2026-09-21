-- ═══════════════════════════════════════════════════════════════════════════
-- FIN — GL Account Mapping seed (fin_gl_map) for company_no = 2
-- Generated: 2026-08-11
--
-- WHAT THIS DOES
--   fin_gl_map tells FinPostingService which GL account each leg of an
--   auto-posted event hits. Without a row, the event PARKS as Failed in
--   FIN_1201 instead of reaching the ledger.
--
--   The leg list below was extracted from the emitters in
--   src\Modules\{Hrm,Sal,Pur,Inv}\...\Services\*.cs — not from the blueprint,
--   which is out of date. 25 mappings: 3 corrections + 22 new.
--
-- ⚠ READ BEFORE RUNNING — THREE EXISTING ROWS ARE WRONG AND WILL BE CORRECTED
--   The only mappings currently in the database are for PayrollPosted, and all
--   three point at the wrong account. As it stands, approving a payroll run
--   would post: Dr Bank Charges / Cr Cash in Hand. That is not a payroll entry.
--
--     event / leg              currently              corrected to
--     ─────────────────────────────────────────────────────────────────────
--     PayrollPosted/EXPENSE    5204 Bank Charges      5201 Salary Expense
--     PayrollPosted/PAYABLE    1101 Cash in Hand      2103 Salary Payable
--     PayrollPosted/STATUTORY  2103 Salary Payable    2104 Accrued Expenses
--
--   If those three were deliberate, remove them from section 1 before running.
--
-- ⚠ THREE JUDGEMENT CALLS NEEDING THE ACCOUNTANT'S SIGN-OFF
--   1. CASH — this chart has both 1101 Cash in Hand and 1102 Cash at Bank.
--      Defaulted to 1102 Cash at Bank for receipts and payments (they clear
--      through the bank), and 1101 Cash in Hand for the sales-invoice cash
--      leg (over-the-counter takings). Change if the business works otherwise.
--   2. STATUTORY / DEDUCTION — there is no dedicated statutory-payable account
--      in this chart. Defaulted to 2104 Accrued Expenses. RECOMMENDED: create
--      a "Statutory Deductions Payable" account under root_type 2 and point
--      these two legs at it, so withheld tax is not mixed with accruals.
--   3. AIT-10 (10%) and TDS-3 (3%) are withholding taxes (tax_type 2 and 3),
--      NOT VAT. They are deliberately NOT seeded as VAT_OUTPUT / VAT_INPUT.
--      Withholding needs its own leg keys and emitter support — out of scope.
--
-- SAFETY
--   Idempotent. Upserts on uq_fin_gl_map_company_event_leg_sub_live
--   (company_no, event_type, leg_key, COALESCE(sub_key,'')) WHERE is_deleted=0.
--   Safe to re-run; a second pass is a no-op. Accounts are resolved by
--   account_code so the script is readable and portable across environments.
--   Verification queries at the bottom — run them after COMMIT.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── Parameters ───────────────────────────────────────────────────────────
-- Change v_company to re-use this script for another company. Every account
-- code referenced below must already exist for that company or the INSERT
-- resolves to NULL and the NOT NULL constraint on account_no rejects it —
-- which is the intended failure mode. It fails loudly rather than mapping
-- a leg to nothing.

CREATE TEMP TABLE _seed (
    event_type   VARCHAR(60)  NOT NULL,
    leg_key      VARCHAR(60)  NOT NULL,
    sub_key      VARCHAR(60),
    account_code VARCHAR(30)  NOT NULL,
    note         TEXT
) ON COMMIT DROP;

INSERT INTO _seed (event_type, leg_key, sub_key, account_code, note) VALUES

-- ═══ 1. CORRECTIONS — these three rows already exist and are wrong ═══
('PayrollPosted',           'EXPENSE',        NULL,     '5201', 'CORRECTION was 5204 Bank Charges — gross payroll is salary expense'),
('PayrollPosted',           'PAYABLE',        NULL,     '2103', 'CORRECTION was 1101 Cash in Hand — net pay owed is a liability'),
('PayrollPosted',           'STATUTORY',      NULL,     '2104', 'CORRECTION was 2103 Salary Payable — withheld tax is a separate liability'),

-- ═══ 2. HRM — final settlement ═══
('FinalSettlementPosted',   'EXPENSE',        NULL,     '5201', 'settlement cost'),
('FinalSettlementPosted',   'PAYABLE',        NULL,     '2103', 'net settlement owed'),
('FinalSettlementPosted',   'DEDUCTION',      NULL,     '2104', 'JUDGEMENT see note 2'),

-- ═══ 3. SAL — sales invoice ═══
('SalesInvoicePosted',      'RECEIVABLE',     NULL,     '1103', 'credit portion'),
('SalesInvoicePosted',      'CASH',           NULL,     '1101', 'JUDGEMENT see note 1 — counter takings'),
('SalesInvoicePosted',      'REVENUE',        NULL,     '4101', NULL),
('SalesInvoicePosted',      'COGS',           NULL,     '5101', NULL),
('SalesInvoicePosted',      'INVENTORY',      NULL,     '1104', 'stock relieved at cost'),
('SalesInvoicePosted',      'VAT_OUTPUT',     NULL,     '2102', 'generic fallback — used when a line carries no tax code'),
('SalesInvoicePosted',      'VAT_OUTPUT',     'VAT-15', '2102', 'per-rate segregation'),
('SalesInvoicePosted',      'VAT_OUTPUT',     'VAT-5',  '2102', 'per-rate segregation'),

-- ═══ 4. SAL — customer receipt ═══
('CustomerReceiptPosted',   'CASH',           NULL,     '1102', 'JUDGEMENT see note 1'),
('CustomerReceiptPosted',   'RECEIVABLE',     NULL,     '1103', NULL),

-- ═══ 5. SAL — sales return ═══
('SalesReturnPosted',       'SALES_RETURN',   NULL,     '4102', NULL),
('SalesReturnPosted',       'RECEIVABLE',     NULL,     '1103', NULL),
('SalesReturnPosted',       'VAT_OUTPUT',     NULL,     '2102', 'VAT reversed on return'),

-- ═══ 6. PUR — purchase invoice ═══
('PurchaseInvoicePosted',   'INVENTORY',      NULL,     '1104', NULL),
('PurchaseInvoicePosted',   'PAYABLE',        NULL,     '2101', NULL),
('PurchaseInvoicePosted',   'VAT_INPUT',      NULL,     '1105', 'generic fallback'),
('PurchaseInvoicePosted',   'VAT_INPUT',      'VAT-15', '1105', 'per-rate segregation'),
('PurchaseInvoicePosted',   'VAT_INPUT',      'VAT-5',  '1105', 'per-rate segregation'),

-- ═══ 7. PUR — supplier payment ═══
('SupplierPaymentPosted',   'PAYABLE',        NULL,     '2101', NULL),
('SupplierPaymentPosted',   'CASH',           NULL,     '1102', 'JUDGEMENT see note 1'),

-- ═══ 8. INV — opening stock ═══
('OpeningStockPosted',      'INVENTORY',      NULL,     '1104', NULL),
('OpeningStockPosted',      'OPENING_EQUITY', NULL,     '3101', NULL),

-- ═══ 9. INV — stock adjustment (and its reversal) ═══
('StockAdjustmentPosted',   'INVENTORY',      NULL,     '1104', NULL),
('StockAdjustmentPosted',   'ADJUSTMENT',     NULL,     '5102', 'gain or loss — direction set by the emitter'),
('StockAdjustmentReversed', 'INVENTORY',      NULL,     '1104', NULL),
('StockAdjustmentReversed', 'ADJUSTMENT',     NULL,     '5102', NULL);


-- ── Upsert ───────────────────────────────────────────────────────────────
INSERT INTO fin_gl_map (
    company_no, branch_no, event_type, leg_key, sub_key, account_no,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    2,
    NULL,                       -- NULL branch = applies to every branch
    s.event_type,
    s.leg_key,
    s.sub_key,
    a.account_no,
    1, 0, NULL, now(), 1
FROM _seed s
JOIN fin_account a
  ON a.account_code = s.account_code
 AND a.company_no   = 2
 AND a.is_deleted   = 0
ON CONFLICT (company_no, event_type, leg_key, COALESCE(sub_key, ''::character varying))
WHERE is_deleted = 0
DO UPDATE SET
    account_no = EXCLUDED.account_no,
    is_active  = 1,
    updated_at = now();


-- ── Guard: every seeded code had to resolve to a real account ────────────
-- If this raises, one of the account codes above does not exist for company 2.
-- Nothing is committed — fix the code and re-run.
DO $$
DECLARE missing INT;
BEGIN
    SELECT count(*) INTO missing
    FROM _seed s
    WHERE NOT EXISTS (
        SELECT 1 FROM fin_account a
        WHERE a.account_code = s.account_code AND a.company_no = 2 AND a.is_deleted = 0
    );
    IF missing > 0 THEN
        RAISE EXCEPTION 'GL map seed aborted: % account code(s) do not exist for company 2', missing;
    END IF;
END $$;

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION — run these after COMMIT
-- ═══════════════════════════════════════════════════════════════════════════

-- 1. What is now mapped (expect 31 rows for company 2)
--
-- SELECT m.event_type, m.leg_key, COALESCE(m.sub_key,'(generic)') AS sub_key,
--        a.account_code, a.account_name
-- FROM fin_gl_map m
-- JOIN fin_account a ON a.account_no = m.account_no
-- WHERE m.company_no = 2 AND m.is_deleted = 0
-- ORDER BY m.event_type, m.leg_key, m.sub_key NULLS FIRST;

-- 2. COVERAGE QUERY — must return ZERO rows.
--    This is the check to re-run whenever a new company onboards or a new
--    emitter is added. Keep it in CUTOVER-FIN.md.
--
-- WITH required(event_type, leg_key) AS (VALUES
--     ('PayrollPosted','EXPENSE'),('PayrollPosted','PAYABLE'),('PayrollPosted','STATUTORY'),
--     ('FinalSettlementPosted','EXPENSE'),('FinalSettlementPosted','PAYABLE'),('FinalSettlementPosted','DEDUCTION'),
--     ('SalesInvoicePosted','RECEIVABLE'),('SalesInvoicePosted','CASH'),('SalesInvoicePosted','REVENUE'),
--     ('SalesInvoicePosted','VAT_OUTPUT'),('SalesInvoicePosted','COGS'),('SalesInvoicePosted','INVENTORY'),
--     ('CustomerReceiptPosted','CASH'),('CustomerReceiptPosted','RECEIVABLE'),
--     ('SalesReturnPosted','SALES_RETURN'),('SalesReturnPosted','VAT_OUTPUT'),('SalesReturnPosted','RECEIVABLE'),
--     ('PurchaseInvoicePosted','INVENTORY'),('PurchaseInvoicePosted','VAT_INPUT'),('PurchaseInvoicePosted','PAYABLE'),
--     ('SupplierPaymentPosted','PAYABLE'),('SupplierPaymentPosted','CASH'),
--     ('OpeningStockPosted','INVENTORY'),('OpeningStockPosted','OPENING_EQUITY'),
--     ('StockAdjustmentPosted','INVENTORY'),('StockAdjustmentPosted','ADJUSTMENT'),
--     ('StockAdjustmentReversed','INVENTORY'),('StockAdjustmentReversed','ADJUSTMENT')
-- )
-- SELECT r.event_type, r.leg_key AS unmapped_leg
-- FROM required r
-- WHERE NOT EXISTS (
--     SELECT 1 FROM fin_gl_map m
--     WHERE m.company_no = 2 AND m.event_type = r.event_type
--       AND m.leg_key = r.leg_key AND m.is_active = 1 AND m.is_deleted = 0
-- );

-- 3. Sanity — no leg should point at an account of the wrong nature.
--    Eyeball this once: expense legs on root_type 5, payables on 2, cash on 1.
--
-- SELECT m.event_type, m.leg_key, a.account_code, a.account_name,
--        a.root_type, a.control_type
-- FROM fin_gl_map m
-- JOIN fin_account a ON a.account_no = m.account_no
-- WHERE m.company_no = 2 AND m.is_deleted = 0
-- ORDER BY a.root_type, m.event_type;
