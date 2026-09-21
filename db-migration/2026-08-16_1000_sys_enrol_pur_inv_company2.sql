-- ═══════════════════════════════════════════════════════════════════════════
-- SYS — enrol the remaining PUR and INV forms for company 2
-- Generated: 2026-08-16   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   Company 2 had NO purchase forms enrolled at all — PUR_1001 through PUR_1301
--   were every one of them absent from sys_enroll_menu, so the whole module
--   returned 403 and no purchase could be entered. INV had only INV_1101 and
--   INV_1102; product master, warehouses, transfers, batches and stocktake were
--   equally unreachable.
--
--   As with the SAL enrolment fixed on 2026-08-14, the role grants already
--   existed. The login query that builds the JWT `perms` claim joins
--   sys_menu -> sys_enroll_menu -> sys_role_permission, so a form with no
--   enrolment row is simply absent from the claim regardless of its grant.
--
--   Found while trying to run a purchase order end to end.
--
-- SAFETY
--   Idempotent. Pure DML. Enrolment only — it confers no new permission, it
--   makes the existing role grants reachable for this company.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

INSERT INTO sys_enroll_menu (
    company_no, branch_no, menu_no, is_lifetime, enroll_start_date, enroll_end_date,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    2, NULL, m.menu_no, 1, NULL, NULL,
    1, 0, 1, now(), 1
FROM sys_menu m
WHERE m.is_deleted = 0
  AND (m.form_id LIKE 'PUR\_%' OR m.form_id LIKE 'INV\_%')
  AND NOT EXISTS (
      SELECT 1 FROM sys_enroll_menu e
      WHERE e.menu_no = m.menu_no AND e.company_no = 2 AND e.is_deleted = 0
  );

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION — every PUR and INV form should show enrolled = 1
-- ═══════════════════════════════════════════════════════════════════════════
-- SELECT m.form_id,
--        (SELECT count(*) FROM sys_enroll_menu e
--          WHERE e.menu_no = m.menu_no AND e.company_no = 2 AND e.is_deleted = 0) AS enrolled,
--        (SELECT count(*) FROM sys_role_permission p
--          WHERE p.menu_no = m.menu_no AND p.is_deleted = 0) AS grants
-- FROM sys_menu m
-- WHERE m.is_deleted = 0 AND (m.form_id LIKE 'PUR\_%' OR m.form_id LIKE 'INV\_%')
-- ORDER BY m.form_id;
--
-- The user must log in again afterwards — `perms` is baked into the access
-- token at login, so an existing token keeps the old permission set.
