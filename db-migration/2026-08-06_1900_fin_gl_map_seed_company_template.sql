-- ═══════════════════════════════════════════════════════════════════════════
-- FIN GL Map Seed — Company-Specific
-- Date: 2026-08-06
-- Purpose: Seed fin_gl_map rows for all emitted event types (28 base legs).
--
-- HOW TO RUN (pgAdmin):
--   1. Edit the two CONFIG blocks below — the company number and the 14
--      account codes. Nothing else in this script needs changing.
--   2. Run the whole script. It is idempotent: existing mappings are skipped.
--   3. Read the verification grid printed by the last statement. Any leg
--      listed as MISSING_ACCOUNT_CODE means that account code does not exist
--      in fin_account for this company — fix the code and re-run.
--
-- NOTE: account codes are used (not raw account_no) so the mapping worksheet
-- the accountant signs off on can be transcribed directly.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

WITH cfg AS (
    -- ── CONFIG 1: company ────────────────────────────────────────────────
    SELECT 1::bigint AS company_no        -- << SET THIS
),
codes (role_key, account_code) AS (
    -- ── CONFIG 2: account codes (must exist in fin_account for company) ──
    VALUES
        ('EXPENSE',      '5100'),         -- << SET THESE
        ('PAYABLE',      '2100'),
        ('STATUTORY',    '2150'),
        ('DEDUCTION',    '2160'),
        ('RECEIVABLE',   '1200'),
        ('CASH',         '1100'),
        ('REVENUE',      '4100'),
        ('SALES_RETURN', '4110'),
        ('VAT_OUTPUT',   '2200'),
        ('VAT_INPUT',    '1250'),
        ('COGS',         '5000'),
        ('INVENTORY',    '1300'),
        ('EQUITY',       '3100'),
        ('ADJUSTMENT',   '5900')
),
legs (event_type, leg_key, role_key) AS (
    -- ── Emitted event legs — do not edit ─────────────────────────────────
    VALUES
        ('PayrollPosted',            'EXPENSE',        'EXPENSE'),
        ('PayrollPosted',            'PAYABLE',        'PAYABLE'),
        ('PayrollPosted',            'STATUTORY',      'STATUTORY'),

        ('FinalSettlementPosted',    'EXPENSE',        'EXPENSE'),
        ('FinalSettlementPosted',    'PAYABLE',        'PAYABLE'),
        ('FinalSettlementPosted',    'DEDUCTION',      'DEDUCTION'),

        ('SalesInvoicePosted',       'RECEIVABLE',     'RECEIVABLE'),
        ('SalesInvoicePosted',       'CASH',           'CASH'),
        ('SalesInvoicePosted',       'REVENUE',        'REVENUE'),
        ('SalesInvoicePosted',       'VAT_OUTPUT',     'VAT_OUTPUT'),
        ('SalesInvoicePosted',       'COGS',           'COGS'),
        ('SalesInvoicePosted',       'INVENTORY',      'INVENTORY'),

        ('CustomerReceiptPosted',    'CASH',           'CASH'),
        ('CustomerReceiptPosted',    'RECEIVABLE',     'RECEIVABLE'),

        ('SalesReturnPosted',        'SALES_RETURN',   'SALES_RETURN'),
        ('SalesReturnPosted',        'VAT_OUTPUT',     'VAT_OUTPUT'),
        ('SalesReturnPosted',        'RECEIVABLE',     'RECEIVABLE'),

        ('PurchaseInvoicePosted',    'INVENTORY',      'INVENTORY'),
        ('PurchaseInvoicePosted',    'VAT_INPUT',      'VAT_INPUT'),
        ('PurchaseInvoicePosted',    'PAYABLE',        'PAYABLE'),

        ('SupplierPaymentPosted',    'PAYABLE',        'PAYABLE'),
        ('SupplierPaymentPosted',    'CASH',           'CASH'),

        ('OpeningStockPosted',       'INVENTORY',      'INVENTORY'),
        ('OpeningStockPosted',       'OPENING_EQUITY', 'EQUITY'),

        ('StockAdjustmentPosted',    'INVENTORY',      'INVENTORY'),
        ('StockAdjustmentPosted',    'ADJUSTMENT',     'ADJUSTMENT'),

        ('StockAdjustmentReversed',  'INVENTORY',      'INVENTORY'),
        ('StockAdjustmentReversed',  'ADJUSTMENT',     'ADJUSTMENT')
),
resolved AS (
    SELECT c.role_key, MIN(a.account_no) AS account_no
    FROM codes c
    CROSS JOIN cfg
    JOIN fin_account a
      ON a.company_no   = cfg.company_no
     AND a.account_code = c.account_code
     AND a.is_deleted   = 0
    GROUP BY c.role_key
)
INSERT INTO fin_gl_map (
    company_no, event_type, leg_key, sub_key, account_no,
    is_active, is_deleted, row_version, created_at
)
SELECT cfg.company_no, l.event_type, l.leg_key, '', r.account_no,
       1, 0, 1, NOW()
FROM legs l
JOIN resolved r ON r.role_key = l.role_key
CROSS JOIN cfg
WHERE NOT EXISTS (
    SELECT 1 FROM fin_gl_map g
    WHERE g.company_no = cfg.company_no
      AND g.event_type = l.event_type
      AND g.leg_key    = l.leg_key
      AND COALESCE(g.sub_key, '') = ''
      AND g.is_deleted = 0
);

COMMIT;

-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION — the grid below is what pgAdmin shows after the run.
-- Every row must have a status of OK. 28 rows expected.
-- (CUTOVER-FIN.md holds the same coverage query for the go-live checklist.)
-- ═══════════════════════════════════════════════════════════════════════════
WITH cfg AS (
    SELECT 1::bigint AS company_no        -- << SAME COMPANY NUMBER AS ABOVE
),
legs (event_type, leg_key) AS (
    VALUES
        ('PayrollPosted',            'EXPENSE'),
        ('PayrollPosted',            'PAYABLE'),
        ('PayrollPosted',            'STATUTORY'),
        ('FinalSettlementPosted',    'EXPENSE'),
        ('FinalSettlementPosted',    'PAYABLE'),
        ('FinalSettlementPosted',    'DEDUCTION'),
        ('SalesInvoicePosted',       'RECEIVABLE'),
        ('SalesInvoicePosted',       'CASH'),
        ('SalesInvoicePosted',       'REVENUE'),
        ('SalesInvoicePosted',       'VAT_OUTPUT'),
        ('SalesInvoicePosted',       'COGS'),
        ('SalesInvoicePosted',       'INVENTORY'),
        ('CustomerReceiptPosted',    'CASH'),
        ('CustomerReceiptPosted',    'RECEIVABLE'),
        ('SalesReturnPosted',        'SALES_RETURN'),
        ('SalesReturnPosted',        'VAT_OUTPUT'),
        ('SalesReturnPosted',        'RECEIVABLE'),
        ('PurchaseInvoicePosted',    'INVENTORY'),
        ('PurchaseInvoicePosted',    'VAT_INPUT'),
        ('PurchaseInvoicePosted',    'PAYABLE'),
        ('SupplierPaymentPosted',    'PAYABLE'),
        ('SupplierPaymentPosted',    'CASH'),
        ('OpeningStockPosted',       'INVENTORY'),
        ('OpeningStockPosted',       'OPENING_EQUITY'),
        ('StockAdjustmentPosted',    'INVENTORY'),
        ('StockAdjustmentPosted',    'ADJUSTMENT'),
        ('StockAdjustmentReversed',  'INVENTORY'),
        ('StockAdjustmentReversed',  'ADJUSTMENT')
)
SELECT l.event_type,
       l.leg_key,
       a.account_code,
       a.account_name,
       CASE WHEN g.gl_map_no IS NULL THEN 'MISSING_ACCOUNT_CODE' ELSE 'OK' END AS status
FROM legs l
CROSS JOIN cfg
LEFT JOIN fin_gl_map g
       ON g.company_no = cfg.company_no
      AND g.event_type = l.event_type
      AND g.leg_key    = l.leg_key
      AND COALESCE(g.sub_key, '') = ''
      AND g.is_active  = 1
      AND g.is_deleted = 0
LEFT JOIN fin_account a
       ON a.account_no = g.account_no
ORDER BY status DESC, l.event_type, l.leg_key;

-- ── VAT Sub-Key Rows (per tax code) ──────────────────────────────────────
-- Only needed when VAT is split by tax code. Generate one row per code from:
--   SELECT tax_code, vat_tax_no FROM sys_vat_tax WHERE company_no = 1 AND is_deleted = 0;
-- Example for tax_code = 'VAT15' (uncomment, set company_no and account code):
--
-- INSERT INTO fin_gl_map (company_no, event_type, leg_key, sub_key, account_no,
--                         is_active, is_deleted, row_version, created_at)
-- SELECT 1, 'SalesInvoicePosted', 'VAT_OUTPUT', 'VAT15', a.account_no, 1, 0, 1, NOW()
-- FROM fin_account a
-- WHERE a.company_no = 1 AND a.account_code = '2201' AND a.is_deleted = 0
--   AND NOT EXISTS (SELECT 1 FROM fin_gl_map g
--                   WHERE g.company_no = 1 AND g.event_type = 'SalesInvoicePosted'
--                     AND g.leg_key = 'VAT_OUTPUT' AND g.sub_key = 'VAT15'
--                     AND g.is_deleted = 0);
