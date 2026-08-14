-- ═══════════════════════════════════════════════════════════════════════════
-- FIN — menu entries for FIN_1202 Accounts Payable / FIN_1203 Accounts Receivable
-- Generated: 2026-08-14   |   run in pgAdmin
--
--   Both forms reconcile a sub-ledger (owned by PUR / SAL) against its GL
--   control account. They are placed beside FIN_1201 Posting Monitor: the
--   person watching postings land is the person who notices when they stop
--   agreeing.
--
--   Read-only, so the grant is can_view + can_export only. Two separate menu
--   rows and two separate form ids even though one screen serves both, so a
--   company can grant AP without granting AR — the two are often different
--   people.
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
    ref.submodule_no, v.form_id, v.form_name, v.menu_desc,
    'form', v.route_path, 'account_balance',
    COALESCE(ref.order_sl, 0) + v.offset_sl, 1, 0, 1, now(), 1
FROM sys_menu ref
CROSS JOIN (VALUES
    ('FIN_1202', 'Accounts Payable',
     'Supplier sub-ledger against the AP control account', '/fin/form/accounts-payable', 1),
    ('FIN_1203', 'Accounts Receivable',
     'Customer sub-ledger against the AR control account', '/fin/form/accounts-receivable', 2)
) AS v(form_id, form_name, menu_desc, route_path, offset_sl)
WHERE ref.form_id = 'FIN_1201'          -- Posting Monitor: same submodule
  AND ref.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_menu m
      WHERE (m.form_id = v.form_id OR m.route_path = v.route_path) AND m.is_deleted = 0
  );


-- ── 2. Enrol every company already entitled to FIN_1201 ──────────────────
INSERT INTO sys_enroll_menu (
    company_no, branch_no, menu_no, is_lifetime, enroll_start_date, enroll_end_date,
    is_active, is_deleted, created_by, created_at, row_version
)
SELECT
    src.company_no, src.branch_no, target.menu_no,
    MAX(src.is_lifetime), MAX(src.enroll_start_date), MAX(src.enroll_end_date),
    1, 0, 1, now(), 1
FROM sys_enroll_menu src
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'FIN_1201' AND ref.is_deleted = 0
JOIN sys_menu target ON target.form_id IN ('FIN_1202', 'FIN_1203') AND target.is_deleted = 0
WHERE src.is_deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM sys_enroll_menu em
      WHERE em.branch_no IS NOT DISTINCT FROM src.branch_no
        AND em.menu_no = target.menu_no
  )
GROUP BY src.company_no, src.branch_no, target.menu_no
ON CONFLICT ON CONSTRAINT uq_branch_menu DO NOTHING;


-- ── 3. Grant — view and export only; a reconciliation writes nothing ─────
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
JOIN sys_menu ref    ON ref.menu_no = src.menu_no AND ref.form_id = 'FIN_1201' AND ref.is_deleted = 0
JOIN sys_menu target ON target.form_id IN ('FIN_1202', 'FIN_1203') AND target.is_deleted = 0
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
-- WHERE m.form_id IN ('FIN_1202', 'FIN_1203') AND m.is_deleted = 0;
--   → two rows, module_code must be FIN.

-- These forms need a control account to compare against. If this returns nothing,
-- the reconciliation will report "no control account" rather than a false break:
-- SELECT company_no, account_code, account_name, control_type
-- FROM fin_account
-- WHERE control_type IN (1, 2) AND is_deleted = 0
-- ORDER BY company_no, control_type;
--   → control_type 1 = AR control, 2 = AP control.
