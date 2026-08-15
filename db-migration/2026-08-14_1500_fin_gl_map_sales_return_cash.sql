-- ═══════════════════════════════════════════════════════════════════════════
-- FIN — complete the GL mapping for sales returns (company_no = 2)
-- Generated: 2026-08-14   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   Found by posting a real return through the till. SalesReturnPosted parked
--   with "No active GL mapping for ... CASH", and once that was added it parked
--   again on INVENTORY — the mapping was incomplete in several places at once.
--
--   Sal1103Service emits SIX leg keys:
--
--     SALES_RETURN   contra-revenue for the goods coming back
--     VAT_OUTPUT     the tax reversed with them
--     RECEIVABLE     when refund_method = 6 (AgainstDue)
--     CASH           for every other refund_method — the ordinary counter case
--     INVENTORY      stock value restored, restocked lines only
--     COGS           the cost of sale unwound
--
--   Mapped before this script:
--     SalesReturnPosted    SALES_RETURN, VAT_OUTPUT, RECEIVABLE   (no CASH, no INVENTORY, no COGS)
--     SalesReturnReversed  nothing
--
--   So a cash refund could never post, and neither could the stock side of ANY
--   return. Cancelling a return could not post at all.
--
--   Accounts match SalesInvoicePosted exactly — a return has to unwind the sale
--   through the same accounts, or the pair leaves a residue behind in each.
--
-- SAFETY
--   Idempotent. Copies account numbers from the existing SalesInvoicePosted
--   mapping rather than hard-coding them, so it stays correct for a company
--   whose chart differs. SALES_RETURN has no sales-invoice counterpart and is
--   taken from the existing SalesReturnPosted row.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. Legs shared with SalesInvoicePosted, for both return events ────────
INSERT INTO fin_gl_map (
    company_no, branch_no, event_type, leg_key, sub_key, account_no,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.company_no, NULL, v.event_type, src.leg_key, NULL, src.account_no,
    1, 0, 1, now(), 1
FROM fin_gl_map src
CROSS JOIN (VALUES ('SalesReturnPosted'), ('SalesReturnReversed')) AS v(event_type)
WHERE src.company_no = 2
  AND src.is_deleted = 0
  AND src.event_type = 'SalesInvoicePosted'
  AND src.sub_key IS NULL
  AND src.leg_key IN ('CASH', 'RECEIVABLE', 'VAT_OUTPUT', 'INVENTORY', 'COGS')
ON CONFLICT (company_no, event_type, leg_key, COALESCE(sub_key, ''::character varying))
WHERE is_deleted = 0
DO UPDATE SET account_no = EXCLUDED.account_no, is_active = 1, updated_at = now();


-- ── 2. SALES_RETURN — no sales-invoice counterpart, copy the posted row ───
INSERT INTO fin_gl_map (
    company_no, branch_no, event_type, leg_key, sub_key, account_no,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.company_no, NULL, 'SalesReturnReversed', src.leg_key, NULL, src.account_no,
    1, 0, 1, now(), 1
FROM fin_gl_map src
WHERE src.company_no = 2
  AND src.is_deleted = 0
  AND src.event_type = 'SalesReturnPosted'
  AND src.leg_key = 'SALES_RETURN'
  AND src.sub_key IS NULL
ON CONFLICT (company_no, event_type, leg_key, COALESCE(sub_key, ''::character varying))
WHERE is_deleted = 0
DO UPDATE SET account_no = EXCLUDED.account_no, is_active = 1, updated_at = now();

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════

-- Both events must list all six legs:
-- SELECT event_type, string_agg(leg_key, ', ' ORDER BY leg_key) AS legs
-- FROM fin_gl_map WHERE company_no = 2 AND is_deleted = 0
--   AND event_type LIKE 'SalesReturn%'
-- GROUP BY event_type;
--   → CASH, COGS, INVENTORY, RECEIVABLE, SALES_RETURN, VAT_OUTPUT

-- Then re-drive anything parked, from FIN_1201:
-- SELECT event_no, event_type, status FROM sys_event_outbox
-- WHERE company_no = 2 AND status = 3 ORDER BY event_no;


-- ═══════════════════════════════════════════════════════════════════════════
-- ADDENDUM — CustomerReceiptReversed (same class of gap)
--
--   The reversal counterpart of CustomerReceiptPosted was never mapped, so
--   cancelling a customer receipt could not post. Same accounts as the posting;
--   the engine flips the direction, not the account.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

INSERT INTO fin_gl_map (
    company_no, branch_no, event_type, leg_key, sub_key, account_no,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.company_no, NULL, 'CustomerReceiptReversed', src.leg_key, src.sub_key, src.account_no,
    1, 0, 1, now(), 1
FROM fin_gl_map src
WHERE src.company_no = 2
  AND src.is_deleted = 0
  AND src.event_type = 'CustomerReceiptPosted'
ON CONFLICT (company_no, event_type, leg_key, COALESCE(sub_key, ''::character varying))
WHERE is_deleted = 0
DO UPDATE SET account_no = EXCLUDED.account_no, is_active = 1, updated_at = now();

COMMIT;
