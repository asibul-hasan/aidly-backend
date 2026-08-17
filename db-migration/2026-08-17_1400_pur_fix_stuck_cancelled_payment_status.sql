-- ═══════════════════════════════════════════════════════════════════════════
-- PUR — payments cancelled in the ledger but left showing Posted
-- Generated: 2026-08-17   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   Two bugs in Pur1104Service.CancelInternalAsync left cancelled payments in
--   a half-cancelled state:
--
--   a) RequireAsync loaded the payment with AsNoTracking(), and the cancel
--      mutates what it returns. Status = Cancelled (and the cancellation stamp)
--      went to a detached copy that SaveChanges ignored, so the API replied
--      "Payment cancelled" while the row stayed Posted — and could be cancelled
--      again, reversing the payable once more each time.
--
--   b) The cancel unwound each invoice's paid/due amounts but never retired the
--      pur_payment_alloc rows, so a cancelled payment still looked applied to
--      its bills.
--
--   Both are fixed in the service. This repairs the rows already stranded.
--
-- HOW A STRANDED ROW IS IDENTIFIED
--   Its reversal is already in the AP sub-ledger ("Payment cancelled <id>")
--   while the payment still claims Posted. That reversal is written inside the
--   same cancel, so its presence is proof the cancel ran. The cancellation
--   stamp cannot be used — bug (a) meant it was never persisted either.
--
--   The AP sub-ledger, the GL and the invoice dues were all reversed correctly
--   by the original cancel, so this migration does NOT touch them. Re-running
--   the cancel would double-reverse the payable; only the payment's own status
--   and its orphaned allocations are corrected here.
--
-- SAFETY
--   Idempotent. Pure DML.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. Retire allocations belonging to a provably cancelled payment ──────
UPDATE pur_payment_alloc a
SET is_deleted = 1,
    deleted_by = 1,
    deleted_at = now()
FROM pur_payment p
WHERE a.payment_no = p.payment_no
  AND a.is_deleted = 0
  AND p.is_deleted = 0
  AND p.status = 2
  AND EXISTS (
      SELECT 1 FROM pur_supplier_ledger sl
      WHERE sl.ref_doc_no = p.payment_id
        AND sl.is_deleted = 0
        AND sl.remarks LIKE 'Payment cancelled%'
  );

-- ── 2. Mark the payment itself Cancelled ─────────────────────────────────
UPDATE pur_payment p
SET status = 3,
    cancelled_at = COALESCE(p.cancelled_at, now())
WHERE p.is_deleted = 0
  AND p.status = 2
  AND EXISTS (
      SELECT 1 FROM pur_supplier_ledger sl
      WHERE sl.ref_doc_no = p.payment_id
        AND sl.is_deleted = 0
        AND sl.remarks LIKE 'Payment cancelled%'
  );

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════
-- No payment claims Posted while its reversal sits in the sub-ledger (expect 0):
--
-- SELECT count(*) FROM pur_payment p
--  WHERE p.is_deleted = 0 AND p.status = 2
--    AND EXISTS (SELECT 1 FROM pur_supplier_ledger sl
--                 WHERE sl.ref_doc_no = p.payment_id AND sl.is_deleted = 0
--                   AND sl.remarks LIKE 'Payment cancelled%');
--
-- No cancelled payment still holds a live allocation (expect 0):
--
-- SELECT count(*) FROM pur_payment_alloc a
--   JOIN pur_payment p ON p.payment_no = a.payment_no
--  WHERE a.is_deleted = 0 AND p.status = 3;
