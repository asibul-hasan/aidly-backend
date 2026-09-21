-- ═══════════════════════════════════════════════════════════════════════════
-- PUR — menu entry for PUR_1301 Supplier Aging & Spend
-- Generated: 2026-08-14   |   run in pgAdmin
--
--   Placed beside PUR_1104 Supplier Payment: whoever plans a payment run is the
--   person who needs the aging report open next to it.
--
--   Read-only report, so the grant is can_view + can_export only — no insert,
--   update, delete or approve.
--
-- SAFETY
--   Idempotent. Pure DDL/DML.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. Menu ──────────────────────────────────────────────────────────────
INSERT INTO sys_menu (
    submodule_no, form_id, form_name, menu_desc, menu_type, route_path, icon_name,
    order_sl, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    ref.submodule_no, 'PUR_1301', 'Supplier Aging & Spend',
    'What is owed to whom, and where purchase money went',
    'report', '/pur/report/aging-spend', 'query_stats',
    COALESCE(ref.order_sl, 0) + 1, 1, 0, 1, now(), 1
FROM sys_menu ref
WHERE ref.form_id = 'PUR_1104'          -- Supplier Payment: same submodule
  AND ref.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_menu m
      WHERE (m.form_id = 'PUR_1301' OR m.route_path = '/pur/report/aging-spend') AND m.is_deleted = 0
  )
LIMIT 1;


-- ── 2. Enrol every company already entitled to PUR_1104 ──────────────────
INSERT INTO sys_enroll_menu (
    company_no, branch_no, menu_no, is_lifetime, enroll_start_date, enroll_end_date,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.company_no, src.branch_no, target.menu_no,
    MAX(src.is_lifetime), MAX(src.enroll_start_date), MAX(src.enroll_end_date),
    1, 0, 1, now(), 1
FROM sys_enroll_menu src
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'PUR_1104' AND ref.is_deleted = 0
JOIN sys_menu target ON target.route_path = '/pur/report/aging-spend' AND target.is_deleted = 0
WHERE src.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_enroll_menu em
      WHERE em.branch_no IS NOT DISTINCT FROM src.branch_no
        AND em.menu_no = target.menu_no
  )
GROUP BY src.company_no, src.branch_no, target.menu_no
ON CONFLICT ON CONSTRAINT uq_branch_menu DO NOTHING;


-- ── 3. Grant — view and export only; a report writes nothing ─────────────
INSERT INTO sys_role_permission (
    role_no, menu_no, can_view, can_insert, can_update, can_delete,
    can_approve, can_post, can_cancel, can_export,
    perm_scope, record_filter, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.role_no, target.menu_no,
    1, 0, 0, 0,
    0, 0, 0, 1,
    MAX(src.perm_scope), MAX(src.record_filter), 1, 0, 1, now(), 1
FROM sys_role_permission src
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'PUR_1104' AND ref.is_deleted = 0
JOIN sys_menu target ON target.route_path = '/pur/report/aging-spend' AND target.is_deleted = 0
WHERE src.is_deleted = 0
  AND src.can_view = 1
  AND NOT EXISTS (
      SELECT 1 FROM sys_role_permission rp
      WHERE rp.role_no = src.role_no AND rp.menu_no = target.menu_no
  )
GROUP BY src.role_no, target.menu_no;

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════

-- SELECT m.form_id, m.route_path, sm.submodule_name, mo.module_code
-- FROM sys_menu m
-- JOIN sys_submodule sm ON sm.submodule_no = m.submodule_no
-- JOIN sys_module mo ON mo.module_no = sm.module_no
-- WHERE m.route_path = '/pur/report/aging-spend' AND m.is_deleted = 0;
--   → module_code must be PUR.
