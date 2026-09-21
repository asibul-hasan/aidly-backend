-- ═══════════════════════════════════════════════════════════════════════════
-- SAL — POS menu seed + company-2 enrolment
-- Generated: 2026-08-13   |   run in pgAdmin
--
-- WHAT THIS DOES
--   Registers the three POS screens so they appear in the menu and RBAC can
--   resolve them. A form is only visible when ALL THREE of these line up:
--
--     1. sys_menu             the screen exists, under a submodule of a module
--     2. sys_enroll_menu      this company is entitled to it
--     3. sys_role_permission  some role can_view it
--
--   Missing any one and the form is invisible — enrolment alone is not enough,
--   which is the usual reason a freshly built form never shows up.
--
--     form      screen                    route
--     ──────────────────────────────────────────────────────────────────────
--     SAL_1001  POS Sales (the till)      /sal/pos
--     SAL_1003  POS Closing / Z-report    /sal/form/pos-closing
--     SAL_1005  POS Terminal Setup        /sal/form/pos-terminal
--
-- ⚠ SAL_1001 DELIBERATELY GETS A SECOND MENU ROW
--   SAL_1001 already exists pointing at /sal/form/sales-invoice. The till is the
--   same form id by design — one sal_invoice document, two ways of ringing it up
--   (sal-business §1.1). So this adds a SECOND row with the same form_id and a
--   different route, rather than a new form id.
--
--   The consequence, stated plainly: permissions are per form_id, so a role that
--   can see the till can also see the credit-invoice form. If you need cashiers
--   to have one without the other, the till needs its own form id — say SAL_1002,
--   which the hold-drawer decision freed up — and the POS controller's route
--   prefix must move with it. Left as-is because the blueprint models them as one
--   form.
--
-- ⚠ ROLE GRANTS
--   Section 4 grants the three forms to every role that already has SAL_1101
--   (Customer Management), on the reasoning that whoever manages customers is a
--   sales user. Review that list before running — it decides who can open a till.
--
-- SAFETY
--   Idempotent throughout (NOT EXISTS guards). Re-running is a no-op.
--   Pure DDL/DML. Verification queries at the bottom.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

-- ── 1. Submodule: "POS" under the existing SAL module ────────────────────
INSERT INTO sys_submodule (
    module_no, submodule_code, submodule_name, submodule_icon, submodule_route,
    order_sl, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    mo.module_no, 'SAL-POS', 'Point of Sale', 'point_of_sale', '/sal',
    5, 1, 0, 1, now(), 1
FROM sys_module mo
WHERE mo.module_code = 'SAL'
  AND mo.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_submodule s
      WHERE s.submodule_code = 'SAL-POS' AND s.is_deleted = 0
  );


-- ── 2. Menus ─────────────────────────────────────────────────────────────
CREATE TEMP TABLE _menus (
    form_id   VARCHAR(30)  NOT NULL,
    form_name VARCHAR(150) NOT NULL,
    route     VARCHAR(200) NOT NULL,
    icon      VARCHAR(60),
    order_sl  INT          NOT NULL
) ON COMMIT DROP;

INSERT INTO _menus VALUES
    ('SAL_1001', 'POS Sales',         '/sal/pos',                 'point_of_sale', 1),
    ('SAL_1003', 'POS Closing',       '/sal/form/pos-closing',    'receipt_long',  2),
    ('SAL_1005', 'POS Terminal Setup','/sal/form/pos-terminal',   'devices',       3),
    ('SAL_1104', 'Promotions',        '/sal/form/promotions',     'sell',          4),
    ('SAL_1301', 'Sales Reports',     '/sal/report/sales',        'query_stats',   5);

INSERT INTO sys_menu (
    submodule_no, form_id, form_name, menu_desc, menu_type, route_path, icon_name,
    order_sl, is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    sm.submodule_no, m.form_id, m.form_name, m.form_name, 'form', m.route, m.icon,
    m.order_sl, 1, 0, 1, now(), 1
FROM _menus m
JOIN sys_submodule sm ON sm.submodule_code = 'SAL-POS' AND sm.is_deleted = 0
WHERE NOT EXISTS (
    -- Keyed on route, not form_id: SAL_1001 legitimately has two rows.
    SELECT 1 FROM sys_menu x WHERE x.route_path = m.route AND x.is_deleted = 0
);


-- ── 3. Enrol company 2 (NULL branch = every branch) ──────────────────────
INSERT INTO sys_enroll_menu (
    company_no, branch_no, menu_no, is_lifetime, enroll_start_date, enroll_end_date,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    2, NULL, mn.menu_no, 1, NULL, NULL,
    1, 0, 1, now(), 1
FROM sys_menu mn
JOIN _menus m ON m.route = mn.route_path
WHERE mn.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_enroll_menu em
      WHERE em.branch_no IS NULL
        AND em.menu_no = mn.menu_no
  )
ON CONFLICT ON CONSTRAINT uq_branch_menu DO NOTHING;


-- ── 4. Grant to the roles that already handle sales ──────────────────────
-- Modelled on SAL_1101 Customer Management. can_approve carries the till's
-- override authority: discount over cap, below-min price, and the drawer
-- variance sign-off all check it.
INSERT INTO sys_role_permission (
    role_no, menu_no, can_view, can_insert, can_update, can_delete,
    can_approve, can_post, can_cancel, can_export,
    perm_scope, record_filter, is_active, is_deleted, created_by, created_at, row_version
)
SELECT DISTINCT
    src.role_no, mn.menu_no, 1, 1, 1, 0,
    src.can_approve, 1, src.can_cancel, 1,
    src.perm_scope, src.record_filter, 1, 0, 1, now(), 1
FROM sys_role_permission src
JOIN sys_menu ref ON ref.menu_no = src.menu_no
                 AND ref.form_id = 'SAL_1101'
                 AND ref.is_deleted = 0
CROSS JOIN sys_menu mn
JOIN _menus m ON m.route = mn.route_path
WHERE src.is_deleted = 0
  AND src.can_view = 1
  AND mn.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_role_permission rp
      WHERE rp.role_no = src.role_no AND rp.menu_no = mn.menu_no AND rp.is_deleted = 0
  );

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION — run after COMMIT
-- ═══════════════════════════════════════════════════════════════════════════

-- 1. The three menus exist and are enrolled for company 2 (expect 3 rows)
--
-- SELECT m.form_id, m.form_name, m.route_path, sm.submodule_name,
--        (em.enroll_menu_no IS NOT NULL) AS enrolled
-- FROM sys_menu m
-- JOIN sys_submodule sm ON sm.submodule_no = m.submodule_no
-- LEFT JOIN sys_enroll_menu em ON em.menu_no = m.menu_no AND em.company_no = 2 AND em.is_deleted = 0
-- WHERE m.route_path IN ('/sal/pos','/sal/form/pos-closing','/sal/form/pos-terminal')
--   AND m.is_deleted = 0;

-- 2. Who can open a till (review this — it is the access list)
--
-- SELECT r.role_name, m.form_id, m.route_path, rp.can_view, rp.can_approve
-- FROM sys_role_permission rp
-- JOIN sys_role r ON r.role_no = rp.role_no
-- JOIN sys_menu m ON m.menu_no = rp.menu_no
-- WHERE m.route_path IN ('/sal/pos','/sal/form/pos-closing','/sal/form/pos-terminal')
--   AND rp.is_deleted = 0
-- ORDER BY r.role_name, m.form_id;

-- 3. Full resolution check — mimics what the login menu query returns.
--    Empty means one of the three joins above is missing for that role.
--
-- SELECT DISTINCT m.form_id, m.route_path
-- FROM sys_role_permission rp
-- JOIN sys_menu m ON m.menu_no = rp.menu_no AND m.is_active = 1 AND m.is_deleted = 0
-- JOIN sys_submodule sm ON sm.submodule_no = m.submodule_no AND sm.is_active = 1 AND sm.is_deleted = 0
-- JOIN sys_module mo ON mo.module_no = sm.module_no AND mo.is_active = 1 AND mo.is_deleted = 0
-- JOIN sys_enroll_menu em ON em.menu_no = m.menu_no AND em.company_no = 2
--                        AND em.is_active = 1 AND em.is_deleted = 0
-- WHERE rp.can_view = 1 AND rp.is_deleted = 0
--   AND m.route_path LIKE '/sal/%pos%';
