-- ═══════════════════════════════════════════════════════════════════════════
-- PUR — invoice cancellations that clawed back the landed cost twice
-- Generated: 2026-08-17   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   Cancelling an invoice debited the AP sub-ledger by inv.GrandTotal. Applying
--   a landed cost adds its amount to GrandTotal and credits the payable under
--   its OWN document, so the cancel took back the freight as well — money the
--   invoice never put there. The sub-ledger ended up short by the landed cost:
--
--       AP sub-ledger  16,000.00
--       GL 2101        16,500.00
--       gap               500.00  = the landed cost on PINV000012
--
--   Pur1102Service now reverses (GrandTotal - LandedCostTotal), so a cancel
--   returns only what that invoice credited. This corrects the row already
--   written by the old path.
--
-- WHY A COMPENSATING ENTRY
--   Same reason as the cash-refund correction: a sub-ledger is a book of
--   record. The over-debit stays and is answered by an equal credit, so the
--   supplier's statement explains itself.
--
-- SAFETY
--   Idempotent — skips any cancellation that already carries its correction.
--   Pure DML. No fin_ledger row and no document is touched.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

INSERT INTO pur_supplier_ledger (
    company_no, branch_no, supplier_no, txn_date, ref_doc_type, ref_doc_no, ref_doc_pk,
    debit, credit, balance_after, remarks,
    fin_year_no, fin_period_no, is_active, is_deleted, created_by, created_at, row_version
)
SELECT sl.company_no, sl.branch_no, sl.supplier_no, sl.txn_date, sl.ref_doc_type,
       sl.ref_doc_no, sl.ref_doc_pk,
       0, i.landed_cost_total,          -- give back only the landed cost slice
       0,
       'Correction: cancellation of ' || sl.ref_doc_no
         || ' reversed the landed cost, which its own document owns',
       sl.fin_year_no, sl.fin_period_no, 1, 0, 1, now(), 1
FROM pur_supplier_ledger sl
JOIN pur_invoice i ON i.invoice_id = sl.ref_doc_no AND i.is_deleted = 0
WHERE sl.is_deleted = 0
  AND sl.remarks LIKE 'Invoice cancelled%'
  AND COALESCE(i.landed_cost_total, 0) > 0
  AND sl.debit = i.grand_total          -- written by the old path (full GrandTotal)
  AND NOT EXISTS (
      SELECT 1 FROM pur_supplier_ledger c
      WHERE c.is_deleted = 0
        AND c.ref_doc_no = sl.ref_doc_no
        AND c.remarks LIKE 'Correction: cancellation of%'
  );

-- Running balance, in ledger order, so the statement reads correctly.
WITH ordered AS (
    SELECT supplier_ledger_no,
           SUM(credit - debit) OVER (PARTITION BY supplier_no ORDER BY supplier_ledger_no
                                     ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running
    FROM pur_supplier_ledger
    WHERE is_deleted = 0
)
UPDATE pur_supplier_ledger sl
SET balance_after = o.running
FROM ordered o
WHERE o.supplier_ledger_no = sl.supplier_ledger_no
  AND sl.balance_after IS DISTINCT FROM o.running;

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════
-- Sub-ledger and control account agree:
--
-- SELECT (SELECT sum(credit) - sum(debit) FROM pur_supplier_ledger WHERE is_deleted = 0) AS ap_subledger,
--        (SELECT sum(l.credit) - sum(l.debit) FROM fin_ledger l
--           JOIN fin_account a ON a.account_no = l.account_no
--          WHERE a.account_code = '2101' AND l.is_deleted = 0) AS gl_2101;
