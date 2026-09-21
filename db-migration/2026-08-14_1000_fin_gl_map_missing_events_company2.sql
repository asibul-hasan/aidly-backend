-- ═══════════════════════════════════════════════════════════════════════════
-- FIN — GL mapping for eight event types that had none (company_no = 2)
-- Generated: 2026-08-14   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   An audit of every event_type the modules emit against the rows actually in
--   fin_gl_map found eight with no mapping at all. The posting engine never
--   guesses an account, so each of these PARKS as Failed in FIN_1201 and the
--   transaction never reaches the ledger:
--
--     GoodsReceiptPosted        PUR_1105  goods arrive, not yet invoiced
--     GoodsReceiptReversed      PUR_1105  receipt cancelled
--     LandedCostApplied         PUR_1106  freight/duty capitalised into stock
--     PurchaseInvoiceReversed   PUR_1102  supplier bill cancelled
--     PurchaseReturnPosted      PUR_1103  goods sent back to supplier
--     PurchaseReturnReversed    PUR_1103  that return cancelled
--     SupplierPaymentReversed   PUR_1104  payment cancelled
--     SalesInvoiceReversed      SAL_1001  credit sale cancelled
--
--   The reversal events matter as much as the postings. Without them a
--   cancellation reverses stock and the sub-ledger but leaves the general
--   ledger holding the original entry forever — the sub-ledger and its control
--   account then disagree permanently, which is exactly what FIN_1202/1203
--   report as an unexplained difference.
--
-- ⚠ ONE ACCOUNT THIS CHART DOES NOT HAVE
--   GoodsReceiptPosted credits GRN_CLEARING — goods received but not yet
--   invoiced. That is a real liability: the stock is on the shelf and the
--   supplier will bill for it, but no bill exists yet. Posting it straight to
--   2101 Accounts Payable would inflate what the AP sub-ledger says is owed
--   against actual supplier invoices, and break the FIN_1202 reconciliation.
--
--   Section 1 creates it if absent:
--
--     2103  Goods Received Not Invoiced   liability (root_type 2)
--
--   The balance clears when PUR_1102 posts the matching bill, which debits
--   INVENTORY — so it should hover near zero and any aged balance means a
--   receipt never got invoiced.
--
-- SAFETY
--   Idempotent. Same upsert target as the 2026-08-11 and 2026-08-13 seeds.
--   Account creation is guarded by NOT EXISTS. A leg whose account_code does
--   not resolve inserts nothing rather than pointing at the wrong account.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── Parameters ───────────────────────────────────────────────────────────
CREATE TEMP TABLE _params (
    company_no BIGINT      NOT NULL,
    grni_code  VARCHAR(30) NOT NULL
) ON COMMIT DROP;

INSERT INTO _params VALUES (2, '2103');


-- ── 1. Create the GRNI account, if absent ────────────────────────────────
-- Placed in the same group as 2101 Accounts Payable so it sits with the other
-- current liabilities in the COA tree.

INSERT INTO fin_account (
    account_code, account_name, account_group_no, root_type, normal_balance,
    is_postable, control_type, requires_cost_center, requires_party,
    opening_balance, opening_dr_cr, company_no, branch_no,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    p.grni_code, 'Goods Received Not Invoiced', sib.account_group_no, 2, 'cr',
    1, NULL, 0, 0,          -- control_type NULL: not an AP control account, deliberately
    0, 'cr', p.company_no, NULL,
    1, 0, NULL, now(), 1
FROM _params p
JOIN fin_account sib
  ON sib.account_code = '2101' AND sib.company_no = p.company_no AND sib.is_deleted = 0
WHERE NOT EXISTS (
    SELECT 1 FROM fin_account a
    WHERE a.account_code = p.grni_code AND a.company_no = p.company_no AND a.is_deleted = 0
);


-- ── 2. The mappings ──────────────────────────────────────────────────────
-- Account codes match the 2026-08-11 seed exactly. A reversal maps to the same
-- accounts as its posting — the engine flips the direction, not the account.

CREATE TEMP TABLE _seed (
    event_type   VARCHAR(60) NOT NULL,
    leg_key      VARCHAR(40) NOT NULL,
    sub_key      VARCHAR(40),
    account_code VARCHAR(30) NOT NULL
) ON COMMIT DROP;

INSERT INTO _seed (event_type, leg_key, sub_key, account_code) VALUES
    -- PUR_1105 Goods Receipt: stock arrives against a liability that is not yet a bill
    ('GoodsReceiptPosted',      'INVENTORY',    NULL,     '1104'),
    ('GoodsReceiptPosted',      'GRN_CLEARING', NULL,     '2103'),
    ('GoodsReceiptReversed',    'INVENTORY',    NULL,     '1104'),
    ('GoodsReceiptReversed',    'GRN_CLEARING', NULL,     '2103'),

    -- PUR_1106 Landed Cost: freight and duty capitalised into inventory value
    ('LandedCostApplied',       'INVENTORY',    NULL,     '1104'),
    ('LandedCostApplied',       'PAYABLE',      NULL,     '2101'),

    -- PUR_1102 cancel — same three legs as PurchaseInvoicePosted, including the
    -- per-tax-code sub-keys, because Pur1102Service emits SubKey on both paths
    ('PurchaseInvoiceReversed', 'INVENTORY',    NULL,     '1104'),
    ('PurchaseInvoiceReversed', 'PAYABLE',      NULL,     '2101'),
    ('PurchaseInvoiceReversed', 'VAT_INPUT',    NULL,     '1105'),
    ('PurchaseInvoiceReversed', 'VAT_INPUT',    'VAT-15', '1105'),
    ('PurchaseInvoiceReversed', 'VAT_INPUT',    'VAT-5',  '1105'),

    -- PUR_1103 Purchase Return and its cancellation
    ('PurchaseReturnPosted',    'INVENTORY',    NULL,     '1104'),
    ('PurchaseReturnPosted',    'PAYABLE',      NULL,     '2101'),
    ('PurchaseReturnPosted',    'VAT_INPUT',    NULL,     '1105'),
    ('PurchaseReturnReversed',  'INVENTORY',    NULL,     '1104'),
    ('PurchaseReturnReversed',  'PAYABLE',      NULL,     '2101'),
    ('PurchaseReturnReversed',  'VAT_INPUT',    NULL,     '1105'),

    -- PUR_1104 payment cancelled. CASH is 1102 Cash at Bank, matching
    -- SupplierPaymentPosted — suppliers are paid from the bank, not the till.
    ('SupplierPaymentReversed', 'PAYABLE',      NULL,     '2101'),
    ('SupplierPaymentReversed', 'CASH',         NULL,     '1102'),

    -- SAL_1001 credit sale cancelled. Mirrors SalesInvoicePosted. VAT carries no
    -- sub-key here: the reversal emits one aggregated VAT_OUTPUT leg.
    ('SalesInvoiceReversed',    'RECEIVABLE',   NULL,     '1103'),
    ('SalesInvoiceReversed',    'CASH',         NULL,     '1101'),
    ('SalesInvoiceReversed',    'REVENUE',      NULL,     '4101'),
    ('SalesInvoiceReversed',    'VAT_OUTPUT',   NULL,     '2102'),
    ('SalesInvoiceReversed',    'COGS',         NULL,     '5101'),
    ('SalesInvoiceReversed',    'INVENTORY',    NULL,     '1104');


INSERT INTO fin_gl_map (
    company_no, branch_no, event_type, leg_key, sub_key, account_no,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    p.company_no,
    NULL,                       -- NULL branch = applies to every branch
    s.event_type,
    s.leg_key,
    s.sub_key,
    a.account_no,
    1, 0, NULL, now(), 1
FROM _seed s
CROSS JOIN _params p
JOIN fin_account a
  ON a.account_code = s.account_code
 AND a.company_no   = p.company_no
 AND a.is_deleted   = 0
ON CONFLICT (company_no, event_type, leg_key, COALESCE(sub_key, ''::character varying))
WHERE is_deleted = 0
DO UPDATE SET
    account_no = EXCLUDED.account_no,
    is_active  = 1,
    updated_at = now();

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════

-- 1. Every leg above resolved to an account (expect 25 rows):
-- SELECT event_type, leg_key, sub_key, a.account_code, a.account_name
-- FROM fin_gl_map m JOIN fin_account a ON a.account_no = m.account_no
-- WHERE m.company_no = 2 AND m.is_deleted = 0
--   AND m.event_type IN ('GoodsReceiptPosted','GoodsReceiptReversed','LandedCostApplied',
--                        'PurchaseInvoiceReversed','PurchaseReturnPosted','PurchaseReturnReversed',
--                        'SupplierPaymentReversed','SalesInvoiceReversed')
-- ORDER BY event_type, leg_key, sub_key;

-- 2. Nothing is still parked for want of a mapping:
-- SELECT event_type, count(*) FROM sys_event_outbox
-- WHERE status = 3 GROUP BY event_type ORDER BY 2 DESC;
--   → re-drive these from FIN_1201 once the rows above exist.
