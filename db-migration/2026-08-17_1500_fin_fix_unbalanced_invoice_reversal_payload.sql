-- ═══════════════════════════════════════════════════════════════════════════
-- FIN — repair an unbalanced PurchaseInvoiceReversed payload
-- Generated: 2026-08-17   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   Pur1102Service billed the PAYABLE leg from inv.GrandTotal. Applying a
--   landed cost adds its amount to GrandTotal and credits the payable under
--   its OWN document, so the invoice's legs credited that freight a second
--   time. On reversal the voucher would not balance:
--
--       Out of balance: debit 10,500.00 != credit 10,000.00
--
--   and the event parked as Failed, leaving GRN clearing at -1,000.00 — a
--   debit balance an accrual account can never work off.
--
--   The service now bills its own goods value plus its own tax, so newly
--   emitted events are balanced. An event ALREADY in the outbox carries the
--   payload serialised at emit time, so re-driving it just replays the bad
--   figure; this rewrites that one payload to what the corrected code emits.
--
-- WHY THIS IS SAFE
--   sys_event_outbox is a QUEUE, not a ledger. This row has never posted — it
--   is status 3 (Failed) precisely because it could not. Correcting an
--   undelivered message and letting it post is what a re-drive is for. No
--   fin_ledger, fin_voucher or document row is touched.
--
-- SAFETY
--   Idempotent — only rewrites legs that are still unbalanced, and only for
--   events that have not been published.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

WITH unbalanced AS (
    SELECT e.event_no,
           SUM(CASE WHEN leg->>'drCr' = 'dr' THEN (leg->>'amount')::numeric ELSE 0 END) AS dr,
           SUM(CASE WHEN leg->>'drCr' = 'cr' THEN (leg->>'amount')::numeric ELSE 0 END) AS cr
    FROM sys_event_outbox e
    CROSS JOIN LATERAL jsonb_array_elements(e.payload->'legs') AS leg
    WHERE e.status <> 2
      AND e.event_type IN ('PurchaseInvoicePosted', 'PurchaseInvoiceReversed')
    GROUP BY e.event_no
    HAVING SUM(CASE WHEN leg->>'drCr' = 'dr' THEN (leg->>'amount')::numeric ELSE 0 END)
        <> SUM(CASE WHEN leg->>'drCr' = 'cr' THEN (leg->>'amount')::numeric ELSE 0 END)
)
UPDATE sys_event_outbox e
SET payload = jsonb_set(
        e.payload,
        '{legs}',
        (
            SELECT jsonb_agg(
                CASE WHEN leg->>'legKey' = 'PAYABLE'
                     -- restate the payable as the other legs' total, which is this invoice's
                     -- own goods value plus its own tax
                     THEN jsonb_set(leg, '{amount}',
                          to_jsonb((SELECT SUM((l2->>'amount')::numeric)
                                      FROM jsonb_array_elements(e.payload->'legs') l2
                                     WHERE l2->>'legKey' <> 'PAYABLE')))
                     ELSE leg END)
            FROM jsonb_array_elements(e.payload->'legs') AS leg
        )
    ),
    status = 1,          -- back to Pending so the drain picks it up
    retry_count = 0
FROM unbalanced u
WHERE u.event_no = e.event_no;

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════
-- No queued invoice event is unbalanced (expect 0 rows):
--
-- SELECT e.event_no,
--        SUM(CASE WHEN leg->>'drCr'='dr' THEN (leg->>'amount')::numeric ELSE 0 END) AS dr,
--        SUM(CASE WHEN leg->>'drCr'='cr' THEN (leg->>'amount')::numeric ELSE 0 END) AS cr
--   FROM sys_event_outbox e
--   CROSS JOIN LATERAL jsonb_array_elements(e.payload->'legs') AS leg
--  WHERE e.status <> 2
--  GROUP BY e.event_no HAVING SUM(...) <> SUM(...);
--
-- After the drain, GRN clearing should be a CREDIT balance equal to goods
-- received but not yet invoiced:
--
-- SELECT sum(l.credit) - sum(l.debit) FROM fin_ledger l
--   JOIN fin_account a ON a.account_no = l.account_no
--  WHERE a.account_code = '2105' AND l.is_deleted = 0;
