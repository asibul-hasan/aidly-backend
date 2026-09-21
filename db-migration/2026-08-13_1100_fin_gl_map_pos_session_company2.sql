-- ═══════════════════════════════════════════════════════════════════════════
-- FIN — GL mapping for the POS session close (company_no = 2)
-- Generated: 2026-08-13   |   run in pgAdmin
--
-- WHAT THIS DOES
--   SalPosService emits PosSessionClosePosted when a cashier closes a drawer.
--   Its three legs need fin_gl_map rows or the event PARKS as Failed in
--   FIN_1201 instead of reaching the ledger.
--
--     leg              meaning                                     Dr/Cr
--     ──────────────────────────────────────────────────────────────────────
--     CASH_IN_TRANSIT  cash physically removed from the till       Dr
--     DRAWER_CASH      what the recorded tenders say was taken     Cr
--     CASH_OVER_SHORT  the difference — only when the count differs Dr short / Cr over
--
--   A perfectly counted drawer posts two legs; a short or over one posts three.
--
-- ⚠ TWO ACCOUNTS THIS CHART DOES NOT HAVE
--   The existing chart (seeded 2026-08-11) has 1101 Cash in Hand and
--   1102 Cash at Bank, but nothing for cash in transit and nothing for a cash
--   variance. Mapping the variance onto an unrelated account — 5102 Stock
--   Adjustment was the closest candidate — would mix till discrepancies into
--   stock gain/loss and make both meaningless.
--
--   So section 1 CREATES the two accounts, if absent:
--
--     1106  Cash in Transit    asset    (root_type 1)
--     5103  Cash Over / Short  expense  (root_type 5)
--
--   Change the codes in the parameters block if your numbering differs. If you
--   would rather bank takings straight to 1102 Cash at Bank and skip the
--   in-transit step, set v_transit_code to '1102' and section 1 will reuse it
--   rather than creating 1106.
--
-- ⚠ DRAWER_CASH points at 1101 Cash in Hand
--   That is deliberate and must match SalesInvoicePosted/CASH, which the
--   2026-08-11 seed mapped to 1101 (counter takings). The close moves money
--   OUT of the same account the sales moved it INTO; if those two disagree,
--   1101 drifts by the value of every session ever closed.
--
-- SAFETY
--   Idempotent. Upserts on the same live unique index as the 2026-08-11 seed.
--   Account creation is guarded by NOT EXISTS. Nothing is committed if an
--   account code fails to resolve.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── Parameters ───────────────────────────────────────────────────────────
CREATE TEMP TABLE _params (
    company_no    BIGINT      NOT NULL,
    drawer_code   VARCHAR(30) NOT NULL,   -- where counter takings sit
    transit_code  VARCHAR(30) NOT NULL,   -- where banked cash goes
    variance_code VARCHAR(30) NOT NULL    -- where over/short lands
) ON COMMIT DROP;

INSERT INTO _params VALUES (2, '1101', '1106', '5103');


-- ── 1. Create the two missing accounts, if absent ────────────────────────
-- Placed in the same group as their sibling cash / expense accounts so they
-- inherit the right position in the COA tree.

INSERT INTO fin_account (
    account_code, account_name, account_group_no, root_type, normal_balance,
    is_postable, control_type, requires_cost_center, requires_party,
    opening_balance, opening_dr_cr, company_no, branch_no,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    p.transit_code, 'Cash in Transit', sib.account_group_no, 1, 'dr',
    1, 4, 0, 0,                          -- control_type 4 = Cash
    0, 'dr', p.company_no, NULL,
    1, 0, NULL, now(), 1
FROM _params p
JOIN fin_account sib
  ON sib.account_code = p.drawer_code AND sib.company_no = p.company_no AND sib.is_deleted = 0
WHERE NOT EXISTS (
    SELECT 1 FROM fin_account a
    WHERE a.account_code = p.transit_code AND a.company_no = p.company_no AND a.is_deleted = 0
);

INSERT INTO fin_account (
    account_code, account_name, account_group_no, root_type, normal_balance,
    is_postable, control_type, requires_cost_center, requires_party,
    opening_balance, opening_dr_cr, company_no, branch_no,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    p.variance_code, 'Cash Over / Short', sib.account_group_no, 5, 'dr',
    1, NULL, 0, 0,
    0, 'dr', p.company_no, NULL,
    1, 0, NULL, now(), 1
FROM _params p
JOIN fin_account sib
  ON sib.account_code = '5102' AND sib.company_no = p.company_no AND sib.is_deleted = 0
WHERE NOT EXISTS (
    SELECT 1 FROM fin_account a
    WHERE a.account_code = p.variance_code AND a.company_no = p.company_no AND a.is_deleted = 0
);


-- ── 2. Map the three legs ────────────────────────────────────────────────
CREATE TEMP TABLE _seed (
    event_type   VARCHAR(60) NOT NULL,
    leg_key      VARCHAR(60) NOT NULL,
    sub_key      VARCHAR(60),
    account_code VARCHAR(30) NOT NULL,
    note         TEXT
) ON COMMIT DROP;

INSERT INTO _seed (event_type, leg_key, sub_key, account_code, note)
SELECT 'PosSessionClosePosted', 'CASH_IN_TRANSIT', NULL, p.transit_code,
       'cash lifted from the till, en route to the bank'
FROM _params p
UNION ALL
SELECT 'PosSessionClosePosted', 'DRAWER_CASH', NULL, p.drawer_code,
       'must match SalesInvoicePosted/CASH or this account drifts'
FROM _params p
UNION ALL
SELECT 'PosSessionClosePosted', 'CASH_OVER_SHORT', NULL, p.variance_code,
       'Dr when the drawer is short, Cr when it is over'
FROM _params p;

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


-- ── 3. Guard: every code had to resolve ──────────────────────────────────
DO $$
DECLARE missing INT;
BEGIN
    SELECT count(*) INTO missing
    FROM _seed s
    CROSS JOIN _params p
    WHERE NOT EXISTS (
        SELECT 1 FROM fin_account a
        WHERE a.account_code = s.account_code AND a.company_no = p.company_no AND a.is_deleted = 0
    );
    IF missing > 0 THEN
        RAISE EXCEPTION 'POS GL map seed aborted: % account code(s) did not resolve', missing;
    END IF;
END $$;

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION — run after COMMIT
-- ═══════════════════════════════════════════════════════════════════════════

-- 1. The three legs, and the accounts they resolved to (expect 3 rows)
--
-- SELECT m.event_type, m.leg_key, a.account_code, a.account_name
-- FROM fin_gl_map m
-- JOIN fin_account a ON a.account_no = m.account_no
-- WHERE m.company_no = 2 AND m.event_type = 'PosSessionClosePosted' AND m.is_deleted = 0
-- ORDER BY m.leg_key;

-- 2. Drawer account agrees with the sales cash leg (expect the SAME account_code twice)
--
-- SELECT m.event_type, m.leg_key, a.account_code
-- FROM fin_gl_map m
-- JOIN fin_account a ON a.account_no = m.account_no
-- WHERE m.company_no = 2 AND m.is_deleted = 0
--   AND ((m.event_type = 'SalesInvoicePosted'     AND m.leg_key = 'CASH' AND m.sub_key IS NULL)
--     OR (m.event_type = 'PosSessionClosePosted'  AND m.leg_key = 'DRAWER_CASH'));

-- 3. Nothing parked after the first close
--
-- SELECT event_no, event_type, status, error_message
-- FROM sys_event_outbox
-- WHERE event_type = 'PosSessionClosePosted' AND status = 3;
