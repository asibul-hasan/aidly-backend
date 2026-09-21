-- ═══════════════════════════════════════════════════════════════════════════
-- PUR — correcting entries for cash-refund returns that debited the payable
-- Generated: 2026-08-17   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   Pur1103Service debited the AP sub-ledger for EVERY return, including cash
--   refunds. A cash refund takes money back rather than netting the debt, so
--   the GL correctly debits Cash and leaves the payable alone — but the
--   sub-ledger was reduced anyway. The two therefore disagreed by the refund
--   amount on every cash return:
--
--       AP sub-ledger  24,149.6552
--       GL 2101        26,500.0000
--       gap             2,350.3448  = 2 x 1,175.1724
--
--   The service is fixed. This corrects the rows it already wrote.
--
-- WHY A COMPENSATING ENTRY AND NOT A DELETE
--   A sub-ledger is a book of record. The wrong entries stay and are answered
--   by an equal and opposite entry, so the supplier's statement shows what
--   happened and why. Deleting them would leave a balance nobody can trace.
--
-- SAFETY
--   Idempotent — the guard skips any return that already carries its
--   correction. Pure DML. Touches only pur_supplier_ledger; no fin_ledger row
--   and no document is altered.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

INSERT INTO pur_supplier_ledger (
    company_no, branch_no, supplier_no, txn_date, ref_doc_type, ref_doc_no, ref_doc_pk,
    debit, credit, balance_after, remarks,
    fin_year_no, fin_period_no, is_active, is_deleted, created_by, created_at, row_version
)
SELECT sl.company_no, sl.branch_no, sl.supplier_no, sl.txn_date, sl.ref_doc_type,
       sl.ref_doc_no, sl.ref_doc_pk,
       0, sl.debit,                       -- reverse the debit with an equal credit
       0,                                 -- recomputed below
       'Correction: cash refund ' || sl.ref_doc_no || ' should not reduce the payable',
       sl.fin_year_no, sl.fin_period_no, 1, 0, 1, now(), 1
FROM pur_supplier_ledger sl
JOIN pur_return r ON r.return_id = sl.ref_doc_no AND r.is_deleted = 0
WHERE sl.is_deleted = 0
  AND r.settlement_mode = 2          -- cash refund
  AND sl.debit > 0
  AND NOT EXISTS (
      SELECT 1 FROM pur_supplier_ledger c
      WHERE c.is_deleted = 0
        AND c.ref_doc_no = sl.ref_doc_no
        AND c.remarks LIKE 'Correction: cash refund%'
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
-- Sub-ledger and control account agree (the two figures must match):
--
-- SELECT (SELECT sum(credit) - sum(debit) FROM pur_supplier_ledger WHERE is_deleted = 0) AS ap_subledger,
--        (SELECT sum(l.credit) - sum(l.debit) FROM fin_ledger l
--           JOIN fin_account a ON a.account_no = l.account_no
--          WHERE a.account_code = '2101' AND l.is_deleted = 0) AS gl_2101;
--
-- No cash-refund return leaves a net payable movement:
--
-- SELECT r.return_id, sum(sl.credit - sl.debit) AS net
--   FROM pur_supplier_ledger sl
--   JOIN pur_return r ON r.return_id = sl.ref_doc_no
--  WHERE r.settlement_mode = 2 AND sl.is_deleted = 0
--  GROUP BY r.return_id;   -> net 0 for each
