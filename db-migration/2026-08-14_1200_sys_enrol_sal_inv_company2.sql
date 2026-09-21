-- ═══════════════════════════════════════════════════════════════════════════
-- SYS — enrol the SAL / INV transaction forms for company 2
-- Generated: 2026-08-14   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   Company 2 is granted these forms at the ROLE level but was never ENROLLED
--   for them. Both are required: the login query that builds the JWT `perms`
--   claim joins sys_menu -> sys_enroll_menu -> sys_role_permission, so a form
--   missing an enrolment row is simply absent from the claim and every request
--   to it returns 403.
--
--   Found while trying to run a POS sale end to end: SAL_1001 (the POS sale
--   endpoint itself) was granted but not enrolled, so the till could not sell.
--
--     form      menu_no   was enrolled   was granted
--     ─────────────────────────────────────────────────
--     INV_1101     15         no             yes
--     INV_1102     16         no             yes
--     SAL_1001     27         no             yes     <- POS sale
--     SAL_1002     28         no             yes
--     SAL_1101     30         no             yes     <- customers
--     SAL_1102     31         no             yes
--     SAL_1103    105         no             yes
--
--   SAL_1003 / SAL_1005 / SAL_1104 / SAL_1301 were already enrolled by the POS
--   menu seeds, which is why only the newer forms worked.
--
-- SAFETY
--   Idempotent. Pure DML. Enrolment only — it grants no new permission, it just
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
  AND m.form_id IN ('INV_1101','INV_1102','SAL_1001','SAL_1002','SAL_1101','SAL_1102','SAL_1103')
  AND NOT EXISTS (
      SELECT 1 FROM sys_enroll_menu e
      WHERE e.menu_no = m.menu_no AND e.company_no = 2 AND e.is_deleted = 0
  );

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION — every row should show enrolled = 1
-- ═══════════════════════════════════════════════════════════════════════════
-- SELECT m.form_id,
--        (SELECT count(*) FROM sys_enroll_menu e
--          WHERE e.menu_no = m.menu_no AND e.company_no = 2 AND e.is_deleted = 0) AS enrolled
-- FROM sys_menu m
-- WHERE m.is_deleted = 0 AND (m.form_id LIKE 'SAL%' OR m.form_id LIKE 'INV_11%')
-- ORDER BY m.form_id;
--
-- The user must log in again afterwards — `perms` is baked into the access token
-- at login, so an existing token keeps the old (empty) permission set.
