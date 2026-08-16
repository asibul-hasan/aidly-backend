-- ═══════════════════════════════════════════════════════════════════════════
-- SYS — menu entries for PUR_1105 Goods Receipt and PUR_1106 Landed Cost
-- Generated: 2026-08-16   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   Both forms are fully implemented — service, controller, frontend component
--   and routes all exist — but neither has a row in sys_menu. Permissions are
--   resolved per form id from the menu, so with no menu row there is no grant,
--   the form never reaches the JWT `perms` claim, and every call returns 403.
--   They were unreachable from the UI and from the API alike.
--
--   Found while running a purchase end to end: the goods receipt step could not
--   be performed at all, which breaks the chain PO -> GRN -> Invoice -> Payment
--   and means stock can only arrive through a purchase invoice.
--
--   Placed beside PUR_1102 Purchase Invoice, which is the same submodule and
--   the step immediately after receiving.
--
-- SAFETY
--   Idempotent. Pure DML. Grants mirror PUR_1102's existing role permissions,
--   so no role gains access it did not already have to the purchase workflow.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. Menu rows ─────────────────────────────────────────────────────────
INSERT INTO sys_menu (
    submodule_no, form_id, form_name, menu_desc, menu_type, route_path, icon_name,
    order_sl, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    ref.submodule_no, v.form_id, v.form_name, v.menu_desc,
    'form', v.route_path, v.icon_name,
    COALESCE(ref.order_sl, 0) + v.offset_sl, 1, 0, 1, now(), 1
FROM sys_menu ref
CROSS JOIN (VALUES
    ('PUR_1105', 'Goods Receipt Note',
     'Receive goods against a purchase order', '/pur/form/goods-receipt-note', 'local_shipping', 1),
    ('PUR_1106', 'Landed Cost',
     'Allocate freight and duty into inventory cost', '/pur/form/landed-cost', 'payments', 2)
) AS v(form_id, form_name, menu_desc, route_path, icon_name, offset_sl)
WHERE ref.form_id = 'PUR_1102'
  AND ref.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_menu m
      WHERE (m.form_id = v.form_id OR m.route_path = v.route_path) AND m.is_deleted = 0
  );


-- ── 2. Enrol every company already entitled to PUR_1102 ──────────────────
INSERT INTO sys_enroll_menu (
    company_no, branch_no, menu_no, is_lifetime, enroll_start_date, enroll_end_date,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.company_no, src.branch_no, target.menu_no,
    MAX(src.is_lifetime), MAX(src.enroll_start_date), MAX(src.enroll_end_date),
    1, 0, 1, now(), 1
FROM sys_enroll_menu src
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'PUR_1102' AND ref.is_deleted = 0
JOIN sys_menu target ON target.form_id IN ('PUR_1105', 'PUR_1106') AND target.is_deleted = 0
WHERE src.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_enroll_menu em
      WHERE em.company_no = src.company_no
        AND em.branch_no IS NOT DISTINCT FROM src.branch_no
        AND em.menu_no = target.menu_no AND em.is_deleted = 0
  )
GROUP BY src.company_no, src.branch_no, target.menu_no;


-- ── 3. Grants — mirror PUR_1102 exactly ──────────────────────────────────
INSERT INTO sys_role_permission (
    role_no, menu_no, can_view, can_insert, can_update, can_delete,
    can_approve, can_post, can_cancel, can_export,
    perm_scope, record_filter, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.role_no, target.menu_no,
    MAX(src.can_view), MAX(src.can_insert), MAX(src.can_update), MAX(src.can_delete),
    MAX(src.can_approve), MAX(src.can_post), MAX(src.can_cancel), MAX(src.can_export),
    MAX(src.perm_scope), MAX(src.record_filter), 1, 0, 1, now(), 1
FROM sys_role_permission src
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'PUR_1102' AND ref.is_deleted = 0
JOIN sys_menu target ON target.form_id IN ('PUR_1105', 'PUR_1106') AND target.is_deleted = 0
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
--        (SELECT count(*) FROM sys_enroll_menu e
--          WHERE e.menu_no=m.menu_no AND e.is_deleted=0) AS enrolments,
--        (SELECT count(*) FROM sys_role_permission p
--          WHERE p.menu_no=m.menu_no AND p.is_deleted=0) AS grants
-- FROM sys_menu m WHERE m.form_id IN ('PUR_1105','PUR_1106') AND m.is_deleted=0;
--   → two rows, both with enrolments and grants > 0. Log in again afterwards.
