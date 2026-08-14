-- ═══════════════════════════════════════════════════════════════════════════
-- FIN — GL map rows for the two SAL reversal event types (company_no = 2)
-- Generated: 2026-08-11
--
-- WHY
--   Sal1102Service.CancelAsync and Sal1103Service.CancelAsync were added to close
--   the Java→.NET gap. Each emits a REVERSING GL event under a DISTINCT event_type:
--
--       CustomerReceiptReversed   (mirror of CustomerReceiptPosted)
--       SalesReturnReversed       (mirror of SalesReturnPosted)
--
--   The distinct name is deliberate: FinPostingService dedupes on
--   (source_doc_type, source_doc_no), so reusing the original event_type would make
--   the engine treat the reversal as an already-processed duplicate and silently
--   drop it. The INV module uses the same StockAdjustmentPosted / …Reversed pair.
--
--   Without these rows the reversal PARKS as Failed in FIN_1201 and the cancellation
--   never reaches the ledger — the document shows Cancelled while the GL still holds
--   the original entry.
--
-- LEG MAPPING — identical accounts to the forward events, by design.
--   The reversal swaps DIRECTION (dr↔cr) in the payload, not the account. So each
--   leg points at the same account as its forward counterpart:
--       CustomerReceiptReversed / CASH        → 1102 Cash at Bank      (as CustomerReceiptPosted)
--       CustomerReceiptReversed / RECEIVABLE  → 1103 Accounts Receivable
--       SalesReturnReversed / SALES_RETURN    → 4102 Sales Return
--       SalesReturnReversed / VAT_OUTPUT      → 2102 Output VAT Payable
--       SalesReturnReversed / RECEIVABLE      → 1103 Accounts Receivable
--       SalesReturnReversed / CASH            → 1101 Cash in Hand      (cash-refund path)
--       SalesReturnReversed / INVENTORY       → 1104 Inventory
--       SalesReturnReversed / COGS            → 5101 Cost of Goods Sold
--
--   NOTE Sal1103 emits either RECEIVABLE (refund settled against the customer's due,
--   refund_method = 2) or CASH (cash refund). Both are seeded so either path resolves.
--
-- SAFETY
--   Idempotent — upserts on uq_fin_gl_map_company_event_leg_sub_live. Re-running is a
--   no-op. Aborts if any account code does not exist for company 2.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

CREATE TEMP TABLE _seed_rev (
    event_type   VARCHAR(60) NOT NULL,
    leg_key      VARCHAR(60) NOT NULL,
    account_code VARCHAR(30) NOT NULL
) ON COMMIT DROP;

INSERT INTO _seed_rev (event_type, leg_key, account_code) VALUES
('CustomerReceiptReversed', 'CASH',         '1102'),
('CustomerReceiptReversed', 'RECEIVABLE',   '1103'),
('SalesReturnReversed',     'SALES_RETURN', '4102'),
('SalesReturnReversed',     'VAT_OUTPUT',   '2102'),
('SalesReturnReversed',     'RECEIVABLE',   '1103'),
('SalesReturnReversed',     'CASH',         '1101'),
('SalesReturnReversed',     'INVENTORY',    '1104'),
('SalesReturnReversed',     'COGS',         '5101');

-- Guard — fail loudly rather than mapping a leg to nothing.
DO $$
DECLARE missing INT;
BEGIN
    SELECT count(*) INTO missing
    FROM _seed_rev s
    WHERE NOT EXISTS (
        SELECT 1 FROM fin_account a
        WHERE a.account_code = s.account_code AND a.company_no = 2 AND a.is_deleted = 0
    );
    IF missing > 0 THEN
        RAISE EXCEPTION 'Reversal GL map seed aborted: % account code(s) missing for company 2', missing;
    END IF;
END $$;

INSERT INTO fin_gl_map (
    company_no, branch_no, event_type, leg_key, sub_key, account_no,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT 2, NULL, s.event_type, s.leg_key, NULL, a.account_no, 1, 0, NULL, now(), 1
FROM _seed_rev s
JOIN fin_account a
  ON a.account_code = s.account_code AND a.company_no = 2 AND a.is_deleted = 0
ON CONFLICT (company_no, event_type, leg_key, COALESCE(sub_key, ''::character varying))
WHERE is_deleted = 0
DO UPDATE SET account_no = EXCLUDED.account_no, is_active = 1, updated_at = now();

COMMIT;

-- ── VERIFICATION ─────────────────────────────────────────────────────────
-- Expect 8 rows.
--
-- SELECT m.event_type, m.leg_key, a.account_code, a.account_name
-- FROM fin_gl_map m JOIN fin_account a ON a.account_no = m.account_no
-- WHERE m.company_no = 2 AND m.is_deleted = 0
--   AND m.event_type IN ('CustomerReceiptReversed','SalesReturnReversed')
-- ORDER BY m.event_type, m.leg_key;
