-- ═══════════════════════════════════════════════════════════════════════════
-- SYS — menu entry for PUR_1002 Supplier Price List
-- Generated: 2026-08-16   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   PUR_1002 is the only PUR form with no sys_menu row. The Angular route
--   (/pur/form/supplier-price-list), the component and Pur1002Controller all
--   exist, but with no menu there is no enrolment and no role grant, so
--   RbacAuthorizationInterceptor rejects every call with 403 — verified live
--   against the running API. The form is unreachable from the menu and dead
--   even when navigated to directly.
--
--   Modelled on PUR_1001 Supplier Management: the price list is supplier master
--   data, belongs in the same submodule, and should be granted to whoever
--   maintains suppliers.
--
-- SAFETY
--   Idempotent. Pure DML.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. Menu, placed straight after Supplier Management ───────────────────
INSERT INTO sys_menu (
    submodule_no, form_id, form_name, menu_desc, menu_type, route_path, icon_name,
    order_sl, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    ref.submodule_no, 'PUR_1002', 'Supplier Price List',
    'Per-supplier product prices and lead times',
    'form', '/pur/form/supplier-price-list', 'sell',
    COALESCE(ref.order_sl, 0) + 1, 1, 0, 1, now(), 1
FROM sys_menu ref
WHERE ref.form_id = 'PUR_1001'
  AND ref.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_menu m
      WHERE (m.form_id = 'PUR_1002' OR m.route_path = '/pur/form/supplier-price-list')
        AND m.is_deleted = 0
  )
LIMIT 1;


-- ── 2. Enrol everyone already entitled to PUR_1001 ───────────────────────
INSERT INTO sys_enroll_menu (
    company_no, branch_no, menu_no, is_lifetime, enroll_start_date, enroll_end_date,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.company_no, src.branch_no, target.menu_no,
    MAX(src.is_lifetime), MAX(src.enroll_start_date), MAX(src.enroll_end_date),
    1, 0, 1, now(), 1
FROM sys_enroll_menu src
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'PUR_1001' AND ref.is_deleted = 0
JOIN sys_menu target ON target.form_id = 'PUR_1002' AND target.is_deleted = 0
WHERE src.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_enroll_menu em
      WHERE em.company_no IS NOT DISTINCT FROM src.company_no
        AND em.branch_no IS NOT DISTINCT FROM src.branch_no
        AND em.menu_no = target.menu_no AND em.is_deleted = 0
  )
GROUP BY src.company_no, src.branch_no, target.menu_no;


-- ── 3. Grants — mirror PUR_1001, minus the document actions ──────────────
--    A price list is edited, never approved/posted/cancelled.
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
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'PUR_1001' AND ref.is_deleted = 0
JOIN sys_menu target ON target.form_id = 'PUR_1002' AND target.is_deleted = 0
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
-- FROM sys_menu m WHERE m.form_id='PUR_1002' AND m.is_deleted=0;
--   → one row, enrol and grants > 0. Log in again to pick up the permission
--     (perms are baked into the JWT at login).
