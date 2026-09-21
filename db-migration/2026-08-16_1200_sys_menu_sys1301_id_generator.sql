-- ═══════════════════════════════════════════════════════════════════════════
-- SYS — menu entry for SYS_1301 ID Generator
-- Generated: 2026-08-16   |   run in pgAdmin
--
--   The last form of the Java -> .NET migration. It configures the document
--   number series every module draws from, so it belongs beside the other
--   system setup screens.
--
--   Placed next to SYS_1108 Approval Workflow Setup: both are cross-cutting
--   configuration that decides how documents behave everywhere else.
--
--   Full CRUD grant — this is a setup form. Deleting a series that has already
--   issued numbers is refused by the service regardless of permission, because
--   reusing a printed document number is not recoverable.
--
-- SAFETY
--   Idempotent. Pure DML.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. Menu ──────────────────────────────────────────────────────────────
INSERT INTO sys_menu (
    submodule_no, form_id, form_name, menu_desc, menu_type, route_path, icon_name,
    order_sl, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    ref.submodule_no, 'SYS_1301', 'ID Generator',
    'Configure document number patterns for every form',
    'form', '/sys/form/id-generator', 'tag',
    COALESCE(ref.order_sl, 0) + 1, 1, 0, 1, now(), 1
FROM sys_menu ref
WHERE ref.form_id = 'SYS_1108'
  AND ref.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_menu m
      WHERE (m.form_id = 'SYS_1301' OR m.route_path = '/sys/form/id-generator')
        AND m.is_deleted = 0
  )
LIMIT 1;


-- ── 2. Enrol every company already entitled to SYS_1108 ──────────────────
INSERT INTO sys_enroll_menu (
    company_no, branch_no, menu_no, is_lifetime, enroll_start_date, enroll_end_date,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.company_no, src.branch_no, target.menu_no,
    MAX(src.is_lifetime), MAX(src.enroll_start_date), MAX(src.enroll_end_date),
    1, 0, 1, now(), 1
FROM sys_enroll_menu src
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'SYS_1108' AND ref.is_deleted = 0
JOIN sys_menu target ON target.form_id = 'SYS_1301' AND target.is_deleted = 0
WHERE src.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_enroll_menu em
      WHERE em.company_no = src.company_no
        AND em.branch_no IS NOT DISTINCT FROM src.branch_no
        AND em.menu_no = target.menu_no AND em.is_deleted = 0
  )
GROUP BY src.company_no, src.branch_no, target.menu_no;


-- ── 3. Grants — mirror SYS_1108 ──────────────────────────────────────────
INSERT INTO sys_role_permission (
    role_no, menu_no, can_view, can_insert, can_update, can_delete,
    can_approve, can_post, can_cancel, can_export,
    perm_scope, record_filter, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.role_no, target.menu_no,
    MAX(src.can_view), MAX(src.can_insert), MAX(src.can_update), MAX(src.can_delete),
    0, 0, 0, MAX(src.can_export),
    MAX(src.perm_scope), MAX(src.record_filter), 1, 0, 1, now(), 1
FROM sys_role_permission src
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'SYS_1108' AND ref.is_deleted = 0
JOIN sys_menu target ON target.form_id = 'SYS_1301' AND target.is_deleted = 0
WHERE src.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_role_permission rp
      WHERE rp.role_no = src.role_no AND rp.menu_no = target.menu_no AND rp.is_deleted = 0
  )
GROUP BY src.role_no, target.menu_no;

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════
-- SELECT m.form_id, m.route_path,
--        (SELECT count(*) FROM sys_enroll_menu e WHERE e.menu_no=m.menu_no AND e.is_deleted=0) AS enrol,
--        (SELECT count(*) FROM sys_role_permission p WHERE p.menu_no=m.menu_no AND p.is_deleted=0) AS grants
-- FROM sys_menu m WHERE m.form_id='SYS_1301' AND m.is_deleted=0;
--   → one row with enrol and grants > 0. Log in again to pick up the permission.
