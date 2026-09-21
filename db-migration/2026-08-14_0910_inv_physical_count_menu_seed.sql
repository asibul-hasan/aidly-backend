-- ═══════════════════════════════════════════════════════════════════════════
-- INV — menu entry for INV_2006 Physical Count
-- Generated: 2026-08-14   |   run in pgAdmin, AFTER 2026-08-14_0900
--
--   Kept separate from the SAL POS menu seed on purpose: this menu belongs under
--   the INV module, and putting it in the SAL script would have filed a stock
--   form under Point of Sale.
--
--   A form needs all three of sys_menu + sys_enroll_menu + sys_role_permission
--   (can_view = 1) before it appears. Role grants are copied from INV_1102 Stock
--   Adjustment, since a physical count posts through exactly that document.
--
-- SAFETY
--   Idempotent. Pure DDL/DML.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. Menu, under the same submodule as the other stock operations ──────
INSERT INTO sys_menu (
    submodule_no, form_id, form_name, menu_desc, menu_type, route_path, icon_name,
    order_sl, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    ref.submodule_no, 'INV_2006', 'Physical Count', 'Stocktake and variance posting',
    'form', '/inv/form/physical-count', 'fact_check',
    COALESCE(ref.order_sl, 0) + 1, 1, 0, 1, now(), 1
FROM sys_menu ref
WHERE ref.form_id = 'INV_1102'          -- Stock Adjustment: same submodule, same neighbourhood
  AND ref.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_menu m
      WHERE (m.form_id = 'INV_2006' OR m.route_path = '/inv/form/physical-count') AND m.is_deleted = 0
  )
LIMIT 1;


-- ── 2. Enrol every company already entitled to INV_1102 ──────────────────
INSERT INTO sys_enroll_menu (
    company_no, branch_no, menu_no, is_lifetime, enroll_start_date, enroll_end_date,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.company_no, src.branch_no, target.menu_no,
    MAX(src.is_lifetime), MAX(src.enroll_start_date), MAX(src.enroll_end_date),
    1, 0, 1, now(), 1
FROM sys_enroll_menu src
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'INV_1102' AND ref.is_deleted = 0
JOIN sys_menu target ON target.route_path = '/inv/form/physical-count' AND target.is_deleted = 0
WHERE src.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_enroll_menu em
      WHERE em.branch_no IS NOT DISTINCT FROM src.branch_no
        AND em.menu_no = target.menu_no
  )
GROUP BY src.company_no, src.branch_no, target.menu_no
ON CONFLICT ON CONSTRAINT uq_branch_menu DO NOTHING;


-- ── 3. Grant to the roles that can already adjust stock ──────────────────
-- can_approve matters here: it is what lets a supervisor post a variance.
INSERT INTO sys_role_permission (
    role_no, menu_no, can_view, can_insert, can_update, can_delete,
    can_approve, can_post, can_cancel, can_export,
    perm_scope, record_filter, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.role_no, target.menu_no,
    1, 1, 1, MAX(src.can_delete),
    MAX(src.can_approve), MAX(src.can_post), MAX(src.can_cancel), 1,
    MAX(src.perm_scope), MAX(src.record_filter), 1, 0, 1, now(), 1
FROM sys_role_permission src
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'INV_1102' AND ref.is_deleted = 0
JOIN sys_menu target ON target.route_path = '/inv/form/physical-count' AND target.is_deleted = 0
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
-- WHERE m.route_path = '/inv/form/physical-count' AND m.is_deleted = 0;
--   → module_code must be INV, not SAL.

-- SELECT r.role_name, rp.can_view, rp.can_approve
-- FROM sys_role_permission rp
-- JOIN sys_role r ON r.role_no = rp.role_no
-- JOIN sys_menu m ON m.menu_no = rp.menu_no
-- WHERE m.route_path = '/inv/form/physical-count' AND rp.is_deleted = 0;
