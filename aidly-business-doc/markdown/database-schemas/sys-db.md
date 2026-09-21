# SYS DB Script

<!-- Consolidated from the previous aidly-business-doc/backend, frontend, memory, and planning files. -->

This file contains the SYS/schema and catalog SQL that defines platform security, tenant context, menu registry, enrollment, and SYS seeds/migrations.

## Base SYS Schema

```sql
-- =============================================================================
-- AIDLY ERP â€” SYS (System / Platform Foundation) MODULE â€” FULL DDL
-- Target: PostgreSQL 14+   |   Implements: sys-db.md (re-plan)
-- House style: _no BIGSERIAL PK, _id partial-unique per scope, SMALLINT bool/enum
--              + CHECK, soft-delete + row_version, TIMESTAMPTZ.
--
-- HOW TO RUN
--   psql "$DB_URL" -v ON_ERROR_STOP=1 -f sys-db.sql
--
-- NOTES
--   * Wrapped in a single transaction â€” all-or-nothing.
--   * Cross-module FKs (hr_employee, inv_warehouse) are added by conditional DO
--     blocks at the end, so this script runs cleanly whether or not the HR / INV
--     modules exist yet. Re-run after those modules are created to attach them.
--   * Circular FKs (company<->currency/file, branch<->department/cost_center) are
--     added via ALTER after both tables exist.
--   * For a CLEAN re-install on a DB that already has the OLD sys_* tables,
--     uncomment the DROP block below (DESTRUCTIVE â€” drops data).
-- =============================================================================

BEGIN;
SET search_path = public;

-- -----------------------------------------------------------------------------
-- OPTIONAL CLEAN RE-INSTALL (DESTRUCTIVE) â€” uncomment to drop the old SYS module
-- -----------------------------------------------------------------------------
-- DROP TABLE IF EXISTS sys_platform_admin, sys_login_attempt, sys_log, sys_audit_log,
--   sys_session, sys_event_outbox, sys_doc_sequence, sys_setting,
--   sys_approval_request_step, sys_approval_request, sys_approval_workflow,
--   sys_role_permission, sys_user_warehouse, sys_user_branch, sys_user_company,
--   sys_user, sys_role, sys_enroll_menu, sys_menu, sys_submodule, sys_module,
--   sys_fin_year_dtl, sys_fin_year, sys_vat_tax, sys_department, sys_cost_center,
--   sys_branch, sys_exchange_rate, sys_currency, sys_file, sys_company CASCADE;

-- =============================================================================
-- 0. EXTENSIONS
-- =============================================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

-- =============================================================================
-- 1. CORE ORGANIZATION
-- =============================================================================

-- 1.1 Company (legal entity / apex) ------------------------------------------
CREATE TABLE sys_company (
    company_no       BIGSERIAL PRIMARY KEY,
    tenant_no        BIGINT,                                  -- RESERVED for future sys_tenant
    company_id       VARCHAR(15)  NOT NULL,
    company_name     VARCHAR(250) NOT NULL,
    company_name_nls VARCHAR(250),
    company_type     VARCHAR(50),
    trade_license_no VARCHAR(100),
    vat_reg_no       VARCHAR(100),
    tin_no           VARCHAR(100),
    bin_no           VARCHAR(100),
    reg_no           VARCHAR(100),
    company_addr1    VARCHAR(250),
    company_addr2    VARCHAR(250),
    city             VARCHAR(100),
    state_province   VARCHAR(100),
    post_code        VARCHAR(20),
    country_code     VARCHAR(10),
    mobile_no        VARCHAR(20),
    contact_no       VARCHAR(25),
    email            VARCHAR(250),
    website          VARCHAR(250),
    base_currency_no BIGINT,                                  -- FK -> sys_currency (added in Â§9)
    costing_method   SMALLINT     NOT NULL DEFAULT 1,         -- 1=WtdAvg,2=FIFO,3=LIFO,4=Standard
    logo_path        VARCHAR(255),
    logo_file_no     BIGINT,                                  -- FK -> sys_file (added in Â§9)
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_company_costing CHECK (costing_method IN (1,2,3,4)),
    CONSTRAINT chk_sys_company_active  CHECK (is_active  IN (0,1)),
    CONSTRAINT chk_sys_company_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_company_id    ON sys_company(company_id) WHERE is_deleted = 0;
CREATE INDEX        idx_sys_company_tenant ON sys_company(tenant_no);

-- 1.2 File / binary store (logos, photos, signatures, images as bytes) -------
CREATE TABLE sys_file (
    file_no         BIGSERIAL PRIMARY KEY,
    company_no      BIGINT REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    entity_type     SMALLINT NOT NULL,    -- 1=CompanyLogo,2=EmpPhoto,3=EmpSign,4=UserAvatar,5=ProductImage,6=BranchLogo,7=Document,8=Other
    entity_no       BIGINT,
    file_name       VARCHAR(255) NOT NULL,
    content_type    VARCHAR(100) NOT NULL,
    file_extension  VARCHAR(10),
    file_size       BIGINT NOT NULL,
    storage_type    SMALLINT NOT NULL DEFAULT 1,  -- 1=DB(bytea), 2=ExternalURL
    file_bytes      BYTEA,
    external_url    VARCHAR(500),
    checksum_sha256 VARCHAR(64),
    width_px        INTEGER, height_px INTEGER,
    is_primary      SMALLINT NOT NULL DEFAULT 1,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_file_entity  CHECK (entity_type IN (1,2,3,4,5,6,7,8)),
    CONSTRAINT chk_sys_file_storage CHECK (storage_type IN (1,2)),
    CONSTRAINT chk_sys_file_payload CHECK ((storage_type = 1 AND file_bytes IS NOT NULL)
                                        OR (storage_type = 2 AND external_url IS NOT NULL)),
    CONSTRAINT chk_sys_file_active  CHECK (is_active  IN (0,1)),
    CONSTRAINT chk_sys_file_deleted CHECK (is_deleted IN (0,1))
);
CREATE INDEX idx_sys_file_entity   ON sys_file(entity_type, entity_no) WHERE is_deleted = 0;
CREATE INDEX idx_sys_file_company  ON sys_file(company_no);
CREATE INDEX idx_sys_file_checksum ON sys_file(checksum_sha256);

-- 1.3 Currency ---------------------------------------------------------------
CREATE TABLE sys_currency (
    currency_no      BIGSERIAL PRIMARY KEY,
    company_no       BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    currency_code    VARCHAR(3)  NOT NULL,
    currency_name    VARCHAR(100) NOT NULL,
    currency_symbol  VARCHAR(10),
    fraction_name    VARCHAR(50),
    decimal_places   SMALLINT NOT NULL DEFAULT 2,
    number_system    SMALLINT NOT NULL DEFAULT 1,   -- 1=Global(1,000,000), 2=Indian(10,00,000)
    exchange_rate    NUMERIC(15,6) NOT NULL DEFAULT 1.000000,
    is_base_currency SMALLINT NOT NULL DEFAULT 0,
    remarks          TEXT,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_currency_numsys  CHECK (number_system IN (1,2)),
    CONSTRAINT chk_sys_currency_rate    CHECK (exchange_rate > 0),
    CONSTRAINT chk_sys_currency_base    CHECK (is_base_currency IN (0,1)),
    CONSTRAINT chk_sys_currency_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_currency_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_currency_code ON sys_currency(company_no, currency_code) WHERE is_deleted = 0;

-- 1.4 Exchange rate (historical) ---------------------------------------------
CREATE TABLE sys_exchange_rate (
    exchange_rate_no BIGSERIAL PRIMARY KEY,
    company_no  BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    currency_no BIGINT NOT NULL REFERENCES sys_currency(currency_no) ON DELETE RESTRICT,
    rate_date   DATE NOT NULL,
    rate        NUMERIC(15,6) NOT NULL,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_fx_rate CHECK (rate > 0)
);
CREATE UNIQUE INDEX uq_sys_fx ON sys_exchange_rate(company_no, currency_no, rate_date) WHERE is_deleted = 0;

-- 1.5 Branch (operational outlet) --------------------------------------------
CREATE TABLE sys_branch (
    branch_no       BIGSERIAL PRIMARY KEY,
    company_no      BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_id       VARCHAR(15)  NOT NULL,
    branch_name     VARCHAR(250) NOT NULL,
    branch_name_nls VARCHAR(250),
    branch_type     SMALLINT NOT NULL DEFAULT 1,   -- 1=Outlet,2=HeadOffice,3=WarehouseHub,4=Online
    branch_addr1    VARCHAR(250), branch_addr2 VARCHAR(250),
    city VARCHAR(100), post_code VARCHAR(20), mobile_no VARCHAR(20), contact_no VARCHAR(25), email VARCHAR(250),
    manager_employee_no BIGINT,            -- FK -> hr_employee (added in Â§10 if HR exists)
    department_no   BIGINT,                -- FK -> sys_department (added in Â§9)
    cost_center_no  BIGINT,                -- FK -> sys_cost_center (added in Â§9)
    logo_file_no    BIGINT REFERENCES sys_file(file_no) ON DELETE SET NULL,
    is_main_branch  SMALLINT NOT NULL DEFAULT 0,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_branch_type    CHECK (branch_type IN (1,2,3,4)),
    CONSTRAINT chk_sys_branch_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_branch_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_branch_company ON sys_branch(company_no, branch_id) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_sys_branch_main    ON sys_branch(company_no) WHERE is_main_branch = 1 AND is_deleted = 0;
CREATE INDEX        idx_sys_branch_company ON sys_branch(company_no) WHERE is_deleted = 0;

-- 1.6 Cost center (accounting dimension) -------------------------------------
CREATE TABLE sys_cost_center (
    cost_center_no  BIGSERIAL PRIMARY KEY,
    company_no      BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    cost_center_id  VARCHAR(20) NOT NULL,
    cost_center_name VARCHAR(150) NOT NULL,
    branch_no       BIGINT REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    parent_cost_center_no BIGINT REFERENCES sys_cost_center(cost_center_no) ON DELETE RESTRICT,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_cc_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_cc_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_cc_id ON sys_cost_center(company_no, cost_center_id) WHERE is_deleted = 0;

-- 1.7 Department -------------------------------------------------------------
CREATE TABLE sys_department (
    department_no   BIGSERIAL PRIMARY KEY,
    company_no      BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no       BIGINT REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    department_id   VARCHAR(20) NOT NULL,
    department_name VARCHAR(150) NOT NULL,
    parent_department_no BIGINT REFERENCES sys_department(department_no) ON DELETE RESTRICT,
    cost_center_no  BIGINT REFERENCES sys_cost_center(cost_center_no) ON DELETE RESTRICT,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_dept_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_dept_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_dept_id ON sys_department(company_no, department_id) WHERE is_deleted = 0;

-- =============================================================================
-- 2. FISCAL & TAX
-- =============================================================================

-- 2.1 VAT / Tax --------------------------------------------------------------
CREATE TABLE sys_vat_tax (
    vat_tax_no      BIGSERIAL PRIMARY KEY,
    company_no      BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no       BIGINT REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    tax_code        VARCHAR(20)  NOT NULL,
    tax_name        VARCHAR(150) NOT NULL,
    tax_type        SMALLINT NOT NULL,             -- 1=VAT,2=Tax,3=AIT,...
    rate_percentage NUMERIC(5,2) NOT NULL,
    effective_from  DATE NOT NULL,
    effective_to    DATE,
    gl_account_no   BIGINT,                        -- FK -> fin_account (FIN module; no FK here)
    authority_name  VARCHAR(150),
    remarks         TEXT,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_tax_dates   CHECK (effective_to IS NULL OR effective_to >= effective_from),
    CONSTRAINT chk_sys_tax_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_tax_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_vat_tax_code ON sys_vat_tax(company_no, tax_code) WHERE is_deleted = 0;

-- 2.2 Financial year (master) ------------------------------------------------
CREATE TABLE sys_fin_year (
    fin_year_no   BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no     BIGINT REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    fin_year_id   VARCHAR(20)  NOT NULL,           -- auto FY-MMMYY-MMMYY when blank
    fin_year_name VARCHAR(100) NOT NULL,
    start_date    DATE NOT NULL,
    end_date      DATE NOT NULL,
    is_closed     SMALLINT NOT NULL DEFAULT 0,
    remarks       TEXT,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_fy_dates    CHECK (start_date < end_date),
    CONSTRAINT chk_sys_fy_closed   CHECK (is_closed IN (0,1)),
    CONSTRAINT chk_sys_fy_active   CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_fy_deleted  CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_fy_id   ON sys_fin_year(company_no, fin_year_id)   WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_sys_fy_name ON sys_fin_year(company_no, fin_year_name) WHERE is_deleted = 0;
CREATE INDEX idx_sys_fy_branch ON sys_fin_year(branch_no, is_deleted);

-- 2.3 Financial period (detail) ----------------------------------------------
CREATE TABLE sys_fin_year_dtl (
    fin_period_no   BIGSERIAL PRIMARY KEY,
    fin_year_no     BIGINT NOT NULL REFERENCES sys_fin_year(fin_year_no) ON DELETE CASCADE,
    fin_period_id   VARCHAR(20)  NOT NULL,         -- user-entered (M01, Q1, ADJ...)
    fin_period_name VARCHAR(100) NOT NULL,
    start_date      DATE NOT NULL,
    end_date        DATE NOT NULL,
    period_type     SMALLINT NOT NULL DEFAULT 1,   -- 1=Monthly,2=Quarterly,3=Half,4=Yearly,5=Adjustment
    period_status   SMALLINT NOT NULL DEFAULT 1,   -- 1=Open,2=Closed,3=Locked
    remarks         TEXT,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_sys_fin_period   UNIQUE (fin_year_no, fin_period_id),
    CONSTRAINT chk_sys_fp_dates    CHECK (start_date < end_date),
    CONSTRAINT chk_sys_fp_type     CHECK (period_type IN (1,2,3,4,5)),
    CONSTRAINT chk_sys_fp_status   CHECK (period_status IN (1,2,3)),
    CONSTRAINT chk_sys_fp_active   CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_fp_deleted  CHECK (is_deleted IN (0,1))
);
CREATE INDEX idx_sys_fp_year ON sys_fin_year_dtl(fin_year_no, is_deleted);

-- =============================================================================
-- 3. APPLICATION REGISTRY (FIXED global catalog â€” shared by all client companies)
-- =============================================================================

-- 3.1 Module -----------------------------------------------------------------
-- CREATE TABLE sys_module (
--     module_no   SERIAL PRIMARY KEY,
--     module_code VARCHAR(10)  NOT NULL,
--     module_name VARCHAR(250) NOT NULL,
--     module_desc VARCHAR(500),
--     module_icon VARCHAR(100),
--     module_route VARCHAR(50),
--     order_sl    INTEGER NOT NULL DEFAULT 0,
--     is_active   SMALLINT NOT NULL DEFAULT 1,
--     is_deleted  SMALLINT NOT NULL DEFAULT 0,
--     created_by  BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
--     updated_by  BIGINT, updated_at TIMESTAMPTZ,
--     row_version BIGINT NOT NULL DEFAULT 1,
--     CONSTRAINT chk_sys_module_active  CHECK (is_active IN (0,1)),
--     CONSTRAINT chk_sys_module_deleted CHECK (is_deleted IN (0,1))
-- );
-- CREATE UNIQUE INDEX uq_sys_module_code ON sys_module(module_code) WHERE is_deleted = 0;

-- -- 3.2 Submodule --------------------------------------------------------------
-- CREATE TABLE sys_submodule (
--     submodule_no   BIGSERIAL PRIMARY KEY,
--     module_no      BIGINT NOT NULL REFERENCES sys_module(module_no) ON DELETE RESTRICT,
--     submodule_code VARCHAR(20)  NOT NULL,
--     submodule_name VARCHAR(250) NOT NULL,
--     submodule_icon VARCHAR(100),
--     submodule_route VARCHAR(100),
--     order_sl    INTEGER NOT NULL DEFAULT 0,
--     is_active   SMALLINT NOT NULL DEFAULT 1,
--     is_deleted  SMALLINT NOT NULL DEFAULT 0,
--     created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
--     updated_at  TIMESTAMPTZ,
--     row_version BIGINT NOT NULL DEFAULT 1,
--     CONSTRAINT uq_sys_submodule UNIQUE (module_no, submodule_code),
--     CONSTRAINT chk_sys_submodule_active  CHECK (is_active IN (0,1)),
--     CONSTRAINT chk_sys_submodule_deleted CHECK (is_deleted IN (0,1))
-- );

-- -- 3.3 Menu (form registry; RBAC key = form_id) -------------------------------
-- CREATE TABLE sys_menu (
--     menu_no      BIGSERIAL PRIMARY KEY,
--     submodule_no BIGINT NOT NULL REFERENCES sys_submodule(submodule_no) ON DELETE RESTRICT,
--     form_id      VARCHAR(30)  NOT NULL,
--     form_name    VARCHAR(250) NOT NULL,
--     menu_desc    VARCHAR(500),
--     menu_type    VARCHAR(50),
--     route_path   VARCHAR(250),
--     icon_name    VARCHAR(100),
--     order_sl     INTEGER NOT NULL DEFAULT 0,
--     is_visible   SMALLINT NOT NULL DEFAULT 1,
--     is_active    SMALLINT NOT NULL DEFAULT 1,
--     is_deleted   SMALLINT NOT NULL DEFAULT 0,
--     created_by   BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
--     updated_by   BIGINT, updated_at TIMESTAMPTZ,
--     row_version  BIGINT NOT NULL DEFAULT 1,
--     CONSTRAINT chk_sys_menu_active  CHECK (is_active IN (0,1)),
--     CONSTRAINT chk_sys_menu_deleted CHECK (is_deleted IN (0,1))
-- );
-- CREATE UNIQUE INDEX uq_sys_menu_form      ON sys_menu(form_id) WHERE is_deleted = 0;
-- CREATE INDEX        idx_sys_menu_submodule ON sys_menu(submodule_no);

-- -- 3.4 Menu enrollment (per client company = what they purchased) -------------
-- CREATE TABLE sys_enroll_menu (
--     enroll_menu_no BIGSERIAL PRIMARY KEY,
--     company_no  BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
--     branch_no   BIGINT REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
--     menu_no     BIGINT NOT NULL REFERENCES sys_menu(menu_no) ON DELETE RESTRICT,
--     is_lifetime SMALLINT NOT NULL DEFAULT 0,
--     enroll_start_date DATE, enroll_end_date DATE,
--     is_active   SMALLINT NOT NULL DEFAULT 1,
--     is_deleted  SMALLINT NOT NULL DEFAULT 0,
--     created_by  BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
--     row_version BIGINT NOT NULL DEFAULT 1,
--     CONSTRAINT chk_sys_enroll_active  CHECK (is_active IN (0,1)),
--     CONSTRAINT chk_sys_enroll_deleted CHECK (is_deleted IN (0,1))
-- );
-- CREATE UNIQUE INDEX uq_sys_enroll ON sys_enroll_menu(company_no, COALESCE(branch_no,0), menu_no) WHERE is_deleted = 0;

-- =============================================================================
-- 4. IDENTITY & RBAC
-- =============================================================================

-- 4.1 Role (company-scoped template) -----------------------------------------
CREATE TABLE sys_role (
    role_no     BIGSERIAL PRIMARY KEY,
    company_no  BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    role_id     VARCHAR(20)  NOT NULL,
    role_name   VARCHAR(150) NOT NULL,
    role_desc   VARCHAR(500),
    default_access_scope SMALLINT NOT NULL DEFAULT 1,   -- 1=BRANCH,2=COMPANY,3=GLOBAL
    is_system_role SMALLINT NOT NULL DEFAULT 0,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_role_scope   CHECK (default_access_scope IN (1,2,3)),
    CONSTRAINT chk_sys_role_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_role_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_role_company ON sys_role(company_no, role_id) WHERE is_deleted = 0;
CREATE INDEX        idx_sys_role_company ON sys_role(company_no) WHERE is_deleted = 0;

-- 4.2 User (company-scoped; MUST be an employee) -----------------------------
CREATE TABLE sys_user (
    user_no          BIGSERIAL PRIMARY KEY,
    company_no       BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    employee_no      BIGINT NOT NULL,                          -- FK -> hr_employee (added in Â§10)
    user_id          VARCHAR(50)  NOT NULL,                    -- = hr_employee.employee_id
    email            VARCHAR(250),
    password_hash    VARCHAR(255),                             -- NULL until admin sets; login blocked while NULL
    user_name        VARCHAR(150) NOT NULL,
    access_scope     SMALLINT NOT NULL DEFAULT 1,              -- 1=BRANCH,2=COMPANY,3=GLOBAL
    default_branch_no BIGINT REFERENCES sys_branch(branch_no) ON DELETE SET NULL,
    avatar_file_no   BIGINT REFERENCES sys_file(file_no) ON DELETE SET NULL,
    is_mfa_enabled   SMALLINT NOT NULL DEFAULT 0,
    mfa_secret       VARCHAR(255),
    failed_login_count INTEGER NOT NULL DEFAULT 0,
    is_locked        SMALLINT NOT NULL DEFAULT 0,
    locked_until     TIMESTAMPTZ,
    must_change_password SMALLINT NOT NULL DEFAULT 0,
    password_changed_at  TIMESTAMPTZ,
    last_login_at    TIMESTAMPTZ,
    token_version    INTEGER NOT NULL DEFAULT 1,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_user_scope   CHECK (access_scope IN (1,2,3)),
    CONSTRAINT chk_sys_user_locked  CHECK (is_locked IN (0,1)),
    CONSTRAINT chk_sys_user_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_user_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_user_id       ON sys_user(user_id) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_sys_user_email    ON sys_user(email)   WHERE is_deleted = 0 AND email IS NOT NULL;
CREATE UNIQUE INDEX uq_sys_user_employee ON sys_user(employee_no) WHERE is_deleted = 0;  -- one user per employee
CREATE INDEX        idx_sys_user_company  ON sys_user(company_no) WHERE is_deleted = 0;

-- 4.3 User <-> Company (multi-company access) --------------------------------
CREATE TABLE sys_user_company (
    user_company_no BIGSERIAL PRIMARY KEY,
    user_no    BIGINT NOT NULL REFERENCES sys_user(user_no) ON DELETE RESTRICT,
    company_no BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    is_default SMALLINT NOT NULL DEFAULT 0,
    is_owner   SMALLINT NOT NULL DEFAULT 0,
    is_active  SMALLINT NOT NULL DEFAULT 1,
    is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by BIGINT, updated_at TIMESTAMPTZ,
    deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_usercomp_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_usercomp_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_user_company    ON sys_user_company(user_no, company_no) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_sys_usercomp_default ON sys_user_company(user_no) WHERE is_default = 1 AND is_deleted = 0;
CREATE INDEX        idx_sys_usercomp_user    ON sys_user_company(user_no);

-- 4.4 User <-> Branch carrying the ROLE (role-per-branch) --------------------
CREATE TABLE sys_user_branch (
    user_branch_no BIGSERIAL PRIMARY KEY,
    user_no    BIGINT NOT NULL REFERENCES sys_user(user_no) ON DELETE RESTRICT,
    branch_no  BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    role_no    BIGINT NOT NULL REFERENCES sys_role(role_no) ON DELETE RESTRICT,
    is_default SMALLINT NOT NULL DEFAULT 0,
    is_active  SMALLINT NOT NULL DEFAULT 1,
    is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by BIGINT, updated_at TIMESTAMPTZ,
    deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_userbr_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_userbr_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_user_branch    ON sys_user_branch(user_no, branch_no) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_sys_userbr_default ON sys_user_branch(user_no) WHERE is_default = 1 AND is_deleted = 0;
CREATE INDEX        idx_sys_userbr_user    ON sys_user_branch(user_no)   WHERE is_deleted = 0;
CREATE INDEX        idx_sys_userbr_branch  ON sys_user_branch(branch_no) WHERE is_deleted = 0;
CREATE INDEX        idx_sys_userbr_role    ON sys_user_branch(role_no);

-- 4.5 User <-> Warehouse (warehouse staff capabilities) ----------------------
CREATE TABLE sys_user_warehouse (
    user_warehouse_no BIGSERIAL PRIMARY KEY,
    user_no      BIGINT NOT NULL REFERENCES sys_user(user_no) ON DELETE RESTRICT,
    warehouse_no BIGINT NOT NULL,                       -- FK -> inv_warehouse (added in Â§10)
    can_receive  SMALLINT NOT NULL DEFAULT 0,
    can_issue    SMALLINT NOT NULL DEFAULT 0,
    can_adjust   SMALLINT NOT NULL DEFAULT 0,
    can_transfer SMALLINT NOT NULL DEFAULT 0,
    is_active    SMALLINT NOT NULL DEFAULT 1,
    is_deleted   SMALLINT NOT NULL DEFAULT 0,
    created_by   BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by   BIGINT, updated_at TIMESTAMPTZ,
    deleted_by   BIGINT, deleted_at TIMESTAMPTZ,
    row_version  BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_userwh_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_userwh_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_user_warehouse ON sys_user_warehouse(user_no, warehouse_no) WHERE is_deleted = 0;
CREATE INDEX idx_sys_userwh_user ON sys_user_warehouse(user_no) WHERE is_deleted = 0;

-- 4.6 Role permission (form-level + scope + record filter) -------------------
CREATE TABLE sys_role_permission (
    role_permission_no BIGSERIAL PRIMARY KEY,
    role_no    BIGINT NOT NULL REFERENCES sys_role(role_no) ON DELETE RESTRICT,
    menu_no    BIGINT NOT NULL REFERENCES sys_menu(menu_no) ON DELETE RESTRICT,
    can_view   SMALLINT NOT NULL DEFAULT 0,
    can_insert SMALLINT NOT NULL DEFAULT 0,
    can_update SMALLINT NOT NULL DEFAULT 0,
    can_delete SMALLINT NOT NULL DEFAULT 0,
    can_approve SMALLINT NOT NULL DEFAULT 0,
    can_post   SMALLINT NOT NULL DEFAULT 0,
    can_cancel SMALLINT NOT NULL DEFAULT 0,
    can_export SMALLINT NOT NULL DEFAULT 0,
    perm_scope    SMALLINT NOT NULL DEFAULT 2,   -- 1=COMPANY,2=BRANCH
    record_filter SMALLINT NOT NULL DEFAULT 1,   -- 1=ALL,2=OWN
    data_scope    SMALLINT NOT NULL DEFAULT 1,   -- 1=BRANCH,2=DEPARTMENT,3=EMPLOYEE  -- 2026-08-01
    is_active  SMALLINT NOT NULL DEFAULT 1,
    is_deleted SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_rp_scope   CHECK (perm_scope IN (1,2)),
    CONSTRAINT chk_sys_rp_filter  CHECK (record_filter IN (1,2)),
    CONSTRAINT chk_sys_rp_data_scope CHECK (data_scope IN (1,2,3)),   -- 2026-08-01
    CONSTRAINT chk_sys_rp_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_rp_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_rp_role_menu ON sys_role_permission(role_no, menu_no) WHERE is_deleted = 0;
CREATE INDEX        idx_sys_rp_role      ON sys_role_permission(role_no) WHERE is_deleted = 0;

-- =============================================================================
-- 5. APPROVAL WORKFLOW
-- =============================================================================

CREATE TABLE sys_approval_scope (
    scope_no      BIGSERIAL PRIMARY KEY,
    workflow_name VARCHAR(100) NOT NULL,
    company_no    BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no     BIGINT REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    department_no BIGINT REFERENCES sys_department(department_no) ON DELETE RESTRICT,
    menu_no       BIGINT NOT NULL REFERENCES sys_menu(menu_no) ON DELETE RESTRICT,
    is_active     SMALLINT NOT NULL DEFAULT 1,
    is_deleted    SMALLINT NOT NULL DEFAULT 0,
    created_by    BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by    BIGINT, updated_at TIMESTAMPTZ,
    deleted_by    BIGINT, deleted_at TIMESTAMPTZ,
    row_version   BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_apsc_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_apsc_deleted CHECK (is_deleted IN (0,1))
);

CREATE TABLE sys_approval_step (
    step_no      BIGSERIAL PRIMARY KEY,
    scope_no     BIGINT NOT NULL REFERENCES sys_approval_scope(scope_no) ON DELETE RESTRICT,
    step_number  SMALLINT NOT NULL,
    step_name    VARCHAR(150),
    step_type    SMALLINT NOT NULL DEFAULT 1,
    next_step_no SMALLINT,
    sla_hours           INTEGER,                    -- 2026-07-21 SLA per step (UC 4.3)
    escalation_role_no  BIGINT,                     -- 2026-07-21 escalate-to role
    escalate_on_timeout SMALLINT NOT NULL DEFAULT 0,-- 2026-07-21
    is_final     SMALLINT NOT NULL DEFAULT 0,
    is_active    SMALLINT NOT NULL DEFAULT 1,
    is_deleted   SMALLINT NOT NULL DEFAULT 0,
    created_by   BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by   BIGINT, updated_at TIMESTAMPTZ,
    deleted_by   BIGINT, deleted_at TIMESTAMPTZ,
    row_version  BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_apst_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_apst_deleted CHECK (is_deleted IN (0,1))
);

CREATE TABLE sys_approval_step_approver (
    approver_no  BIGSERIAL PRIMARY KEY,
    step_no      BIGINT NOT NULL REFERENCES sys_approval_step(step_no) ON DELETE RESTRICT,
    emp_no       BIGINT NOT NULL,
    is_active    SMALLINT NOT NULL DEFAULT 1,
    is_deleted   SMALLINT NOT NULL DEFAULT 0,
    created_by   BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by   BIGINT, updated_at TIMESTAMPTZ,
    deleted_by   BIGINT, deleted_at TIMESTAMPTZ,
    row_version  BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_apsa_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_apsa_deleted CHECK (is_deleted IN (0,1))
);

CREATE TABLE sys_approval_request (
    approval_request_no BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL,
    branch_no     BIGINT,
    document_type VARCHAR(30) NOT NULL,
    document_no   VARCHAR(40) NOT NULL,
    document_pk   BIGINT,
    scope_no      BIGINT,                           -- 2026-07-22 scope used, so act() can read step_type/next_step_no
    amount        NUMERIC(20,4) NOT NULL DEFAULT 0,
    current_step  SMALLINT NOT NULL DEFAULT 1,
    status        SMALLINT NOT NULL DEFAULT 1,    -- 1=Pending,2=Approved,3=Rejected,4=Cancelled
    requested_by  BIGINT NOT NULL,
    requested_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at  TIMESTAMPTZ,
    due_at        TIMESTAMP,                        -- 2026-07-21 SLA deadline (drives the SLA timer)
    escalated_at  TIMESTAMP,                        -- 2026-07-21
    escalated_to  BIGINT,                           -- 2026-07-21
    row_version   BIGINT NOT NULL DEFAULT 1
);
CREATE INDEX IF NOT EXISTS idx_approval_request_due ON sys_approval_request(status, due_at);

-- ── Delegation of approval (2026-07-21) — "Delegation of Approval History" ──
CREATE TABLE IF NOT EXISTS sys_approval_delegation (
    delegation_no  BIGSERIAL PRIMARY KEY,
    company_no     BIGINT      NOT NULL,
    branch_no      BIGINT,
    from_user_no   BIGINT      NOT NULL,
    to_user_no     BIGINT      NOT NULL,
    document_type  VARCHAR(30),
    valid_from     DATE        NOT NULL,
    valid_to       DATE        NOT NULL,
    reason         TEXT,
    is_active      SMALLINT    NOT NULL DEFAULT 1,
    is_deleted     SMALLINT    NOT NULL DEFAULT 0,
    created_by     BIGINT, created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by     BIGINT, updated_at TIMESTAMP,
    deleted_by     BIGINT, deleted_at TIMESTAMP,
    row_version    BIGINT      NOT NULL DEFAULT 1
);
CREATE INDEX IF NOT EXISTS idx_delegation_from_user
    ON sys_approval_delegation (from_user_no, valid_from, valid_to) WHERE is_deleted = 0;
CREATE INDEX idx_sys_ar_doc     ON sys_approval_request(document_type, document_no);
CREATE INDEX idx_sys_ar_pending ON sys_approval_request(company_no, status) WHERE status = 1;

CREATE TABLE sys_approval_request_step (
    approval_step_no BIGSERIAL PRIMARY KEY,
    approval_request_no BIGINT NOT NULL REFERENCES sys_approval_request(approval_request_no) ON DELETE CASCADE,
    step_number  SMALLINT NOT NULL,
    emp_no       BIGINT NOT NULL,
    action       SMALLINT,                        -- Managed by frontend constants
    action_emp_no BIGINT, 
    action_date  TIMESTAMPTZ, 
    remarks      VARCHAR(250),
    due_at       TIMESTAMP,                         -- 2026-07-21 per-step SLA deadline
    acted_at     TIMESTAMP,                         -- 2026-07-21 turnaround analytics
    row_version  BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT uq_sys_ar_step UNIQUE (approval_request_no, step_number, emp_no)
);

-- =============================================================================
-- 6. CONFIG & SHARED INFRA
-- =============================================================================

-- 6.1 System / company settings ----------------------------------------------
CREATE TABLE sys_setting (
    setting_no    BIGSERIAL PRIMARY KEY,
    company_no    BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no     BIGINT REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    setting_key   VARCHAR(80)  NOT NULL,
    setting_value VARCHAR(500) NOT NULL,
    value_type    SMALLINT NOT NULL DEFAULT 1,    -- 1=String,2=Number,3=Boolean,4=JSON,5=Date
    setting_group VARCHAR(40),
    description   VARCHAR(250),
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    deleted_by  BIGINT, deleted_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_setting_type    CHECK (value_type IN (1,2,3,4,5)),
    CONSTRAINT chk_sys_setting_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_setting_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_setting ON sys_setting(company_no, COALESCE(branch_no,0), setting_key) WHERE is_deleted = 0;

-- 6.2 Document numbering (SYS-owned; used by inv/sal/pur) ---------------------
CREATE TABLE sys_doc_sequence (
    doc_sequence_no BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    branch_no    BIGINT NOT NULL REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    doc_type     VARCHAR(20) NOT NULL,            -- 'PUR_PO','SAL_INV','INV_ADJ',...
    fin_year_no  BIGINT REFERENCES sys_fin_year(fin_year_no) ON DELETE RESTRICT,
    doc_sub_type VARCHAR(20),                      -- 2026-08-14
    menu_no      BIGINT,                            -- 2026-08-14 the form this series belongs to
    pattern      VARCHAR(120),                      -- 2026-08-14 e.g. PO/{FY_YY}-{FY_YY}/{SEQ:5}; NULL = prefix+padding
    prefix       VARCHAR(20) NOT NULL DEFAULT '',
    suffix       VARCHAR(20) NOT NULL DEFAULT '',
    starting_no  BIGINT NOT NULL DEFAULT 1,         -- 2026-08-14
    next_no      BIGINT NOT NULL DEFAULT 1,
    padding      SMALLINT NOT NULL DEFAULT 6,
    reset_policy SMALLINT NOT NULL DEFAULT 2,     -- 1=Never,2=PerFinYear,3=PerMonth
    -- 2026-08-16: the nine audit columns. This table was the exception that carried none of
    -- them, so DocSequence had to Ignore() every one and any is_deleted filter failed at
    -- runtime ("Translation of member 'IsDeleted' ... failed") — SYS_1301's list hit exactly
    -- that. It also meant a series could not be soft-deleted.
    is_active    SMALLINT NOT NULL DEFAULT 1,
    is_deleted   SMALLINT NOT NULL DEFAULT 0,
    created_by   BIGINT,
    created_at   TIMESTAMP(6) NOT NULL DEFAULT now(),
    updated_by   BIGINT,
    updated_at   TIMESTAMP(6),
    deleted_by   BIGINT,
    deleted_at   TIMESTAMP(6),
    row_version  BIGINT NOT NULL DEFAULT 1
);
CREATE INDEX IF NOT EXISTS idx_sys_doc_sequence_live ON sys_doc_sequence (company_no, doc_type, is_deleted);
CREATE UNIQUE INDEX uq_sys_doc_seq ON sys_doc_sequence(company_no, branch_no, doc_type, COALESCE(fin_year_no,0));

-- 6.3 Transactional outbox (SYS-owned event fan-out) -------------------------
CREATE TABLE sys_event_outbox (
    event_no     BIGSERIAL PRIMARY KEY,
    company_no   BIGINT NOT NULL,
    branch_no    BIGINT,
    aggregate_type VARCHAR(40) NOT NULL,
    aggregate_id   VARCHAR(60) NOT NULL,
    event_type   VARCHAR(60) NOT NULL,
    payload      JSONB NOT NULL,
    status       SMALLINT NOT NULL DEFAULT 1,     -- 1=Pending,2=Published,3=Failed
    retry_count  SMALLINT NOT NULL DEFAULT 0,
    available_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    published_at TIMESTAMPTZ,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_sys_outbox_status CHECK (status IN (1,2,3))
);
CREATE INDEX idx_sys_outbox_pending ON sys_event_outbox(status, available_at) WHERE status IN (1,3);

-- =============================================================================
-- 7. SESSION, AUDIT & OBSERVABILITY
-- =============================================================================

-- 7.1 Session / login context ------------------------------------------------
CREATE TABLE sys_session (
    session_no    BIGSERIAL PRIMARY KEY,
    user_no       BIGINT NOT NULL REFERENCES sys_user(user_no) ON DELETE RESTRICT,
    session_uuid  UUID NOT NULL DEFAULT uuid_generate_v4(),
    active_company_no BIGINT REFERENCES sys_company(company_no) ON DELETE RESTRICT,
    active_branch_no  BIGINT REFERENCES sys_branch(branch_no) ON DELETE RESTRICT,
    access_scope  SMALLINT NOT NULL DEFAULT 1,
    access_token_hash  VARCHAR(255),
    refresh_token_hash VARCHAR(255),
    token_version INTEGER NOT NULL DEFAULT 1,
    ip_address    INET, user_agent TEXT, device_uuid VARCHAR(80),
    login_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen_at  TIMESTAMPTZ,
    logout_at     TIMESTAMPTZ, expired_at TIMESTAMPTZ,
    is_revoked    SMALLINT NOT NULL DEFAULT 0, revoked_at TIMESTAMPTZ,
    row_version   BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_session_scope   CHECK (access_scope IN (1,2,3)),
    CONSTRAINT chk_sys_session_revoked CHECK (is_revoked IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_session_access  ON sys_session(access_token_hash)  WHERE access_token_hash IS NOT NULL;
CREATE UNIQUE INDEX uq_sys_session_refresh ON sys_session(refresh_token_hash) WHERE refresh_token_hash IS NOT NULL;
CREATE INDEX idx_sys_session_user   ON sys_session(user_no);
CREATE INDEX idx_sys_session_uuid   ON sys_session(session_uuid);
CREATE INDEX idx_sys_session_active ON sys_session(user_no) WHERE is_revoked = 0;

-- 7.2 Audit log (data changes) â€” no FK by design (high volume) ---------------
CREATE TABLE sys_audit_log (
    audit_log_no BIGSERIAL PRIMARY KEY,
    company_no  BIGINT, branch_no BIGINT, user_no BIGINT, session_no BIGINT,
    table_name  VARCHAR(150) NOT NULL,
    record_pk   VARCHAR(100) NOT NULL,
    action_type VARCHAR(20)  NOT NULL,           -- INSERT/UPDATE/DELETE/SOFT_DELETE/APPROVE/POST/LOGIN/SWITCH
    old_data    JSONB, new_data JSONB,
    ip_address  INET,
    action_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_sys_audit_target ON sys_audit_log(table_name, record_pk);
CREATE INDEX idx_sys_audit_time   ON sys_audit_log(action_at DESC);
CREATE INDEX idx_sys_audit_user   ON sys_audit_log(company_no, user_no, action_at DESC);

-- 7.3 Request log â€” no FK by design ------------------------------------------
CREATE TABLE sys_log (
    log_no       BIGSERIAL PRIMARY KEY,
    company_no   BIGINT, branch_no BIGINT, user_no BIGINT, session_no BIGINT,
    response_status SMALLINT,
    duration_ms  BIGINT,
    ip_address   INET,
    http_method  VARCHAR(10),
    request_uri  TEXT,
    request_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_sys_log_time        ON sys_log(request_at DESC);
CREATE INDEX idx_sys_log_user_branch ON sys_log(user_no, branch_no);

-- 7.4 Login attempts (brute-force) -------------------------------------------
CREATE TABLE sys_login_attempt (
    login_attempt_no BIGSERIAL PRIMARY KEY,
    user_id     VARCHAR(50) NOT NULL,
    ip_address  INET,
    is_success  SMALLINT NOT NULL DEFAULT 0,
    fail_reason VARCHAR(60),
    attempted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_sys_login_attempt CHECK (is_success IN (0,1))
);
CREATE INDEX idx_sys_login_attempt    ON sys_login_attempt(user_id, attempted_at DESC);
CREATE INDEX idx_sys_login_attempt_ip ON sys_login_attempt(ip_address, attempted_at DESC);

-- =============================================================================
-- 8. PLATFORM ADMIN REALM (SaaS vendor â€” SEPARATE from sys_user)
-- =============================================================================
CREATE TABLE sys_platform_admin (
    platform_admin_no BIGSERIAL PRIMARY KEY,
    admin_login   VARCHAR(60)  NOT NULL,
    email         VARCHAR(250) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    admin_name    VARCHAR(150) NOT NULL,
    admin_role    SMALLINT NOT NULL DEFAULT 1,    -- 1=Support,2=Provisioning,3=Billing,4=SuperAdmin
    is_mfa_enabled SMALLINT NOT NULL DEFAULT 1, mfa_secret VARCHAR(255),
    failed_login_count INTEGER NOT NULL DEFAULT 0, is_locked SMALLINT NOT NULL DEFAULT 0,
    last_login_at TIMESTAMPTZ,
    is_active   SMALLINT NOT NULL DEFAULT 1,
    is_deleted  SMALLINT NOT NULL DEFAULT 0,
    created_by  BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by  BIGINT, updated_at TIMESTAMPTZ,
    row_version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_padmin_role    CHECK (admin_role IN (1,2,3,4)),
    CONSTRAINT chk_sys_padmin_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_padmin_deleted CHECK (is_deleted IN (0,1))
);
CREATE UNIQUE INDEX uq_sys_padmin_login ON sys_platform_admin(admin_login) WHERE is_deleted = 0;
CREATE UNIQUE INDEX uq_sys_padmin_email ON sys_platform_admin(email)       WHERE is_deleted = 0;

-- =============================================================================
-- 9. DEFERRED / CIRCULAR FOREIGN KEYS (same module)
-- =============================================================================
ALTER TABLE sys_company  ADD CONSTRAINT fk_sys_company_currency
    FOREIGN KEY (base_currency_no) REFERENCES sys_currency(currency_no) ON DELETE SET NULL;
ALTER TABLE sys_company  ADD CONSTRAINT fk_sys_company_logo
    FOREIGN KEY (logo_file_no) REFERENCES sys_file(file_no) ON DELETE SET NULL;
ALTER TABLE sys_branch   ADD CONSTRAINT fk_sys_branch_department
    FOREIGN KEY (department_no) REFERENCES sys_department(department_no) ON DELETE SET NULL;
ALTER TABLE sys_branch   ADD CONSTRAINT fk_sys_branch_cost_center
    FOREIGN KEY (cost_center_no) REFERENCES sys_cost_center(cost_center_no) ON DELETE SET NULL;

-- =============================================================================
-- 10. CROSS-MODULE FOREIGN KEYS (added only if the target module exists)
--     Re-run this script (or just this block) after HR / INV modules are created.
-- =============================================================================
DO $$
BEGIN
    IF to_regclass('public.hr_employee') IS NOT NULL THEN
        -- user.employee_no -> hr_employee
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_sys_user_employee') THEN
            ALTER TABLE sys_user ADD CONSTRAINT fk_sys_user_employee
                FOREIGN KEY (employee_no) REFERENCES hr_employee(employee_no) ON DELETE RESTRICT;
        END IF;
        -- branch.manager_employee_no -> hr_employee
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_sys_branch_manager') THEN
            ALTER TABLE sys_branch ADD CONSTRAINT fk_sys_branch_manager
                FOREIGN KEY (manager_employee_no) REFERENCES hr_employee(employee_no) ON DELETE SET NULL;
        END IF;
        -- HR employee photo / signature -> sys_file (fulfils "store employee image as bytes")
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_name='hr_employee' AND column_name='photo_file_no') THEN
            ALTER TABLE hr_employee ADD COLUMN photo_file_no BIGINT;
            ALTER TABLE hr_employee ADD CONSTRAINT fk_hr_employee_photo
                FOREIGN KEY (photo_file_no) REFERENCES sys_file(file_no) ON DELETE SET NULL;
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_name='hr_employee' AND column_name='signature_file_no') THEN
            ALTER TABLE hr_employee ADD COLUMN signature_file_no BIGINT;
            ALTER TABLE hr_employee ADD CONSTRAINT fk_hr_employee_signature
                FOREIGN KEY (signature_file_no) REFERENCES sys_file(file_no) ON DELETE SET NULL;
        END IF;
        RAISE NOTICE 'HR cross-module FKs attached.';
    ELSE
        RAISE NOTICE 'hr_employee not found â€” skipped user/branch employee FKs (re-run after HR module).';
    END IF;

    IF to_regclass('public.inv_warehouse') IS NOT NULL THEN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_sys_userwh_warehouse') THEN
            ALTER TABLE sys_user_warehouse ADD CONSTRAINT fk_sys_userwh_warehouse
                FOREIGN KEY (warehouse_no) REFERENCES inv_warehouse(warehouse_no) ON DELETE RESTRICT;
        END IF;
        RAISE NOTICE 'INV cross-module FK attached.';
    ELSE
        RAISE NOTICE 'inv_warehouse not found â€” skipped user_warehouse FK (re-run after INV module).';
    END IF;
END $$;

-- =============================================================================
-- 11. SEED â€” FIXED global SYS catalog (module + submodules + ALL SYS menus)
--     Idempotent (NOT EXISTS guards). Grant per company later via sys_enroll_menu.
-- =============================================================================

-- 11.1 SYS module
INSERT INTO sys_module (module_code, module_name, module_icon, module_route, order_sl)
SELECT 'SYS','System','settings','/sys',1
WHERE NOT EXISTS (SELECT 1 FROM sys_module WHERE module_code='SYS');

-- 11.2 SYS submodules
INSERT INTO sys_submodule (module_no, submodule_code, submodule_name, submodule_icon, order_sl)
SELECT m.module_no, v.code, v.name, v.icon, v.sl
FROM sys_module m
CROSS JOIN (VALUES
    ('SYS-ORG','Organization Setup','corporate_fare',1),
    ('SYS-SEC','User & Security','admin_panel_settings',2),
    ('SYS-CFG','Configuration','tune',3)
) AS v(code,name,icon,sl)
WHERE m.module_code='SYS'
  AND NOT EXISTS (SELECT 1 FROM sys_submodule s WHERE s.submodule_code=v.code);

-- 11.3 SYS menus (complete set: existing + NEW)
INSERT INTO sys_menu (submodule_no, form_id, form_name, menu_type, route_path, icon_name, order_sl,
                      is_visible, is_active, is_deleted, created_at, row_version)
SELECT s.submodule_no, v.form_id, v.form_name, v.menu_type, v.route_path, v.icon, v.sl,
       1, 1, 0, NOW(), 1
FROM sys_submodule s
JOIN (VALUES
    -- Organization Setup
    ('SYS-ORG','SYS_1001','Company Setup',          'FORM',  '/sys/forms/sys1001',         'business',          1),
    ('SYS-ORG','SYS_1002','Branch Setup',           'FORM',  '/sys/forms/sys1002',         'store',             2),
    ('SYS-ORG','SYS_1003','Financial Year Setup',   'FORM',  '/sys/forms/sys1003',         'calendar_month',    3),
    ('SYS-ORG','SYS_1004','Currency Setup',         'FORM',  '/sys/forms/sys1004',         'payments',          4),
    ('SYS-ORG','SYS_1005','VAT / Tax Setup',        'FORM',  '/sys/forms/sys1005',         'receipt_long',      5),
    ('SYS-ORG','SYS_1006','Exchange Rate Setup',    'FORM',  '/sys/forms/sys1006',         'currency_exchange', 6),
    ('SYS-ORG','SYS_1007','Cost Center Setup',      'FORM',  '/sys/forms/sys1007',         'account_tree',      7),
    ('SYS-ORG','SYS_1008','System Settings',        'FORM',  '/sys/forms/sys1008',         'tune',              8),
    -- User & Security
    ('SYS-SEC','SYS_1101','User Management',        'FORM',  '/sys/forms/sys1101',         'manage_accounts',   1),
    ('SYS-SEC','SYS_1102','Role Management',        'FORM',  '/sys/forms/sys1102',         'badge',             2),
    ('SYS-SEC','SYS_1103','Role Permission Matrix', 'FORM',  '/sys/forms/sys1103',         'grid_on',           3),
    ('SYS-SEC','SYS_1104','User Branch Mapping',    'FORM',  '/sys/forms/sys1104',         'hub',               4),
    ('SYS-SEC','SYS_1105','Activity Log Viewer',    'REPORT','/sys/pages/activity-log',    'history',           5),
    ('SYS-SEC','SYS_1106','User Warehouse Mapping', 'FORM',  '/sys/forms/sys1106',         'warehouse',         6),
    ('SYS-SEC','SYS_1107','User Company Mapping',   'FORM',  '/sys/forms/sys1107',         'domain',            7),
    ('SYS-SEC','SYS_1108','Approval Workflow Setup','FORM',  '/sys/forms/sys1108',         'approval',          8),
    ('SYS-SEC','SYS_1109','Session / Login Monitor','REPORT','/sys/pages/session-monitor', 'devices',           9),
    -- Configuration
    ('SYS-CFG','SYS_1201','Dynamic Menu Builder',   'FORM',  '/sys/forms/sys1201',         'account_tree',      1),
    ('SYS-CFG','SYS_1202','Menu Enrollment',        'FORM',  '/sys/forms/sys1202',         'playlist_add_check',2),
    ('SYS-CFG','SYS_1203','File / Image Manager',   'FORM',  '/sys/forms/sys1203',         'perm_media',        3)
) AS v(scode, form_id, form_name, menu_type, route_path, icon, sl) ON v.scode = s.submodule_code
WHERE NOT EXISTS (SELECT 1 FROM sys_menu mm WHERE mm.form_id = v.form_id);

COMMIT;

-- =============================================================================
-- 12. PER-COMPANY ONBOARDING TEMPLATE (data, not schema) â€” run per client.
--     Replace :company_no / :branch_no / :user values, then execute.
-- =============================================================================
-- -- (a) System role templates for the company
-- INSERT INTO sys_role (company_no, role_id, role_name, default_access_scope, is_system_role, created_by)
-- VALUES (:company_no,'OWNER','Owner / Super Admin',3,1,:by),
--        (:company_no,'BR_MGR','Branch Manager',1,1,:by),
--        (:company_no,'CASHIER','Cashier',1,1,:by),
--        (:company_no,'ACCOUNTANT','Accountant',2,1,:by),
--        (:company_no,'AUDITOR','Auditor (read-only)',2,1,:by),
--        (:company_no,'STORE','Warehouse / Store Keeper',2,1,:by);
--
-- -- (b) Enroll the forms the company purchased (example: all SYS-ORG, lifetime)
-- INSERT INTO sys_enroll_menu (company_no, menu_no, is_lifetime, created_by)
-- SELECT :company_no, mn.menu_no, 1, :by
-- FROM sys_menu mn JOIN sys_submodule s ON s.submodule_no = mn.submodule_no
-- WHERE s.submodule_code IN ('SYS-ORG','SYS-SEC','SYS-CFG')
--   AND NOT EXISTS (SELECT 1 FROM sys_enroll_menu e WHERE e.company_no=:company_no AND e.menu_no=mn.menu_no AND e.is_deleted=0);
--
-- -- (c) Grant a role its form permissions (example: OWNER gets full rights on every enrolled menu)
-- INSERT INTO sys_role_permission (role_no, menu_no, can_view, can_insert, can_update, can_delete,
--                                  can_approve, can_post, can_cancel, can_export, perm_scope, record_filter, created_by)
-- SELECT r.role_no, e.menu_no, 1,1,1,1,1,1,1,1,1,1,:by
-- FROM sys_role r JOIN sys_enroll_menu e ON e.company_no = r.company_no
-- WHERE r.company_no=:company_no AND r.role_id='OWNER' AND e.is_deleted=0
--   AND NOT EXISTS (SELECT 1 FROM sys_role_permission rp WHERE rp.role_no=r.role_no AND rp.menu_no=e.menu_no AND rp.is_deleted=0);
--
-- -- (d) Baseline settings consumed by inv/sal/pur
-- INSERT INTO sys_setting (company_no, setting_key, setting_value, value_type, setting_group, created_by) VALUES
--   (:company_no,'INV.COSTING_METHOD','1',2,'Inventory',:by),
--   (:company_no,'INV.ALLOW_NEGATIVE_STOCK','0',3,'Inventory',:by),
--   (:company_no,'PUR.GRN_REQUIRED','0',3,'Purchase',:by),
--   (:company_no,'PUR.PO_REQUIRED','1',3,'Purchase',:by),
--   (:company_no,'PUR.RECEIPT_TOLERANCE_PCT','5',2,'Purchase',:by),
--   (:company_no,'POS.SESSION_TIMEOUT_MIN','480',2,'Sales',:by),
--   (:company_no,'SEC.PASSWORD_MIN_LENGTH','8',2,'Security',:by);
-- =============================================================================
-- NOTIFICATIONS                                                    -- 2026-08-01
-- status: 0=Unread, 1=Read, 2=ActionTaken
-- =============================================================================

CREATE TABLE sys_notification (
    notification_no     BIGSERIAL PRIMARY KEY,
    company_no          BIGINT,
    branch_no           BIGINT,
    target_user_no      BIGINT NOT NULL,
    target_employee_no  BIGINT,
    sender_user_no      BIGINT,
    menu_no             BIGINT,
    form_id             VARCHAR(50),
    document_type       VARCHAR(30),
    document_pk         BIGINT,
    approval_request_no BIGINT,
    title               VARCHAR(150) NOT NULL,
    message             VARCHAR(500) NOT NULL,
    payload_json        TEXT,
    status              SMALLINT NOT NULL DEFAULT 0,
    read_at             TIMESTAMPTZ,
    is_active           SMALLINT NOT NULL DEFAULT 1,
    is_deleted          SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ,
    deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version         BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_notification_status  CHECK (status IN (0,1,2)),
    CONSTRAINT chk_sys_notification_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_notification_deleted CHECK (is_deleted IN (0,1))
);

CREATE INDEX idx_sys_notification_inbox
    ON sys_notification (target_user_no, is_deleted, company_no, notification_no DESC);
CREATE INDEX idx_sys_notification_unread
    ON sys_notification (target_user_no, company_no) WHERE status = 0 AND is_deleted = 0;
CREATE INDEX idx_sys_notification_document ON sys_notification (document_type, document_pk);
CREATE INDEX idx_sys_notification_approval ON sys_notification (approval_request_no);

CREATE TABLE sys_notification_template (
    template_no      BIGSERIAL PRIMARY KEY,
    company_no       BIGINT,
    menu_no          BIGINT,
    form_id          VARCHAR(50),
    trigger_event    VARCHAR(50)  NOT NULL,
    recipient_rule   VARCHAR(50)  NOT NULL,
    title_template   VARCHAR(200) NOT NULL,
    message_template TEXT         NOT NULL,
    is_active        SMALLINT NOT NULL DEFAULT 1,
    is_deleted       SMALLINT NOT NULL DEFAULT 0,
    created_by BIGINT, created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, updated_at TIMESTAMPTZ,
    deleted_by BIGINT, deleted_at TIMESTAMPTZ,
    row_version      BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT chk_sys_notif_tpl_active  CHECK (is_active IN (0,1)),
    CONSTRAINT chk_sys_notif_tpl_deleted CHECK (is_deleted IN (0,1))
);

CREATE INDEX idx_sys_notif_tpl_lookup
    ON sys_notification_template (company_no, trigger_event, is_deleted);

-- =============================================================================
-- END OF SYS MODULE DDL
-- =============================================================================

```

## Canonical Menu Seed

```sql
-- ============================================================================
-- menu_seed.sql â€” SINGLE canonical menu seed for ALL modules (SYS/HRM/INV/FIN/PUR/SAL)
-- ----------------------------------------------------------------------------
-- Idempotent + self-healing. Re-runnable any time:
--   â€¢ Module / submodule  â†’ inserted only if its code does not already exist.
--   â€¢ Menu (form)         â†’ inserted if the form_id is new; if the form already
--                           exists but its route_path / name / icon / submodule
--                           CHANGED, the existing row is UPDATED to match (so a
--                           renamed route is corrected without a duplicate).
--   â€¢ Enrollment          â†’ every menu of these modules is enrolled for COMPANY 2
--                           (branch_no NULL = all branches) if not already.
-- created_by is stamped with an existing user (min live sys_user.user_no).
-- This file SUPERSEDES the per-module menu seeds (hrm_inv / fin / pur_sal).
-- ============================================================================
BEGIN;

-- â”€â”€ 1) MODULES â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
INSERT INTO sys_module (module_code, module_name, module_route, module_icon, order_sl, is_active, is_deleted, row_version, created_at, created_by)
SELECT v.code, v.name, v.route, v.icon, v.sl, 1, 0, 1, now(), (SELECT min(user_no) FROM sys_user WHERE is_deleted = 0)
FROM (VALUES
    ('SYS', 'System',                    '/sys', 'settings',        1),
    ('HRM', 'Human Resource Management',  'hrm',  'groups',         20),
    ('INV', 'Inventory',                  'inv',  'inventory_2',    30),
    ('FIN', 'Finance & Accounts',         'fin',  'account_balance',40),
    ('PUR', 'Purchase',                   'pur',  'shopping_cart',  50),
    ('SAL', 'Sales',                      'sal',  'point_of_sale',  60)
) AS v(code, name, route, icon, sl)
WHERE NOT EXISTS (SELECT 1 FROM sys_module m WHERE m.module_code = v.code);

-- â”€â”€ 2) SUBMODULES â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
INSERT INTO sys_submodule (module_no, submodule_code, submodule_name, submodule_icon, order_sl, is_active, is_deleted, row_version, created_at, created_by)
SELECT m.module_no, v.code, v.name, v.icon, v.sl, 1, 0, 1, now(), (SELECT min(user_no) FROM sys_user WHERE is_deleted = 0)
FROM (VALUES
    ('SYS', 'SYS-ORG',   'Organization Setup',   'corporate_fare',       1),
    ('SYS', 'SYS-SEC',   'User & Security',       'admin_panel_settings', 2),
    ('SYS', 'SYS-CFG',   'Configuration',         'tune',                 3),
    ('HRM', 'HRM-SETUP', 'HR Setup',              'settings',             1),
    ('HRM', 'HRM-ATT',   'Attendance & Time',     'schedule',             2),
    ('HRM', 'HRM-PAY',   'Payroll',               'payments',             3),
    ('HRM', 'HRM-LEAVE', 'Leave Management',      'event_available',      4),
    ('HRM', 'HRM-REC',   'Recruitment',           'person_search',        5),
    ('INV', 'INV-PROD',  'Product & Setup',       'inventory_2',          1),
    ('INV', 'INV-STOCK', 'Stock Operations',      'warehouse',            2),
    ('INV', 'INV-WH',    'Warehouse & Transfer',  'move_to_inbox',        3),
    ('FIN', 'FIN-SETUP', 'GL Setup',              'settings',             1),
    ('FIN', 'FIN-TXN',   'Transactions',          'receipt_long',         2),
    ('FIN', 'FIN-REPORT','Financial Reports',     'assessment',           3),
    ('PUR', 'PUR-SETUP', 'Purchase Setup',        'settings',             1),
    ('PUR', 'PUR-TXN',   'Purchase Transactions', 'receipt_long',         2),
    ('SAL', 'SAL-SETUP', 'Sales Setup',           'settings',             1),
    ('SAL', 'SAL-TXN',   'Sales Transactions',    'point_of_sale',        2)
) AS v(mcode, code, name, icon, sl)
JOIN sys_module m ON m.module_code = v.mcode
WHERE NOT EXISTS (SELECT 1 FROM sys_submodule s WHERE s.submodule_code = v.code);

-- â”€â”€ 3) MENUS (forms) â€” staged once, then insert-missing + sync-changed â”€â”€â”€â”€â”€â”€â”€
CREATE TEMP TABLE _menu_seed (
    scode      text, form_id text, form_name text, menu_type text,
    route_path text, icon    text, sl int
) ON COMMIT DROP;

INSERT INTO _menu_seed (scode, form_id, form_name, menu_type, route_path, icon, sl) VALUES
    -- â”€â”€ SYS Â· Organization Setup â”€â”€  (route_path = the Angular FRONTEND route, not the API path)
    ('SYS-ORG', 'SYS_1001', 'Company Setup',           'form',   '/sys/form/company-setup',         'business',          1),
    ('SYS-ORG', 'SYS_1002', 'Branch Setup',            'form',   '/sys/form/branch-setup',          'store',             2),
    ('SYS-ORG', 'SYS_1003', 'Financial Year Setup',    'form',   '/sys/form/financial-year-setup',  'calendar_month',    3),
    ('SYS-ORG', 'SYS_1004', 'Currency Setup',          'form',   '/sys/form/currency-setup',        'payments',          4),
    ('SYS-ORG', 'SYS_1005', 'VAT / Tax Setup',         'form',   '/sys/form/vat-tax-setup',         'receipt_long',      5),
    ('SYS-ORG', 'SYS_1006', 'Exchange Rate Setup',     'form',   '/sys/form/exchange-rate-setup',   'currency_exchange', 6),
    ('SYS-ORG', 'SYS_1007', 'Cost Center Setup',       'form',   '/sys/form/cost-center-setup',     'account_tree',      7),
    ('SYS-ORG', 'SYS_1008', 'System Settings',         'form',   '/sys/form/system-settings',       'tune',              8),
    -- â”€â”€ SYS Â· User & Security â”€â”€
    ('SYS-SEC', 'SYS_1101', 'User Management',         'form',   '/sys/form/user-management',          'manage_accounts',   1),
    ('SYS-SEC', 'SYS_1102', 'Role Management',         'form',   '/sys/form/role-management',          'badge',             2),
    ('SYS-SEC', 'SYS_1103', 'Role Permission Matrix',  'report', '/sys/report/role-permission-matrix', 'grid_on',           3),
    ('SYS-SEC', 'SYS_1104', 'User Branch Mapping',     'form',   '/sys/form/user-branch-mapping',      'hub',               4),
    ('SYS-SEC', 'SYS_1105', 'Activity Log Viewer',     'report', '/sys/report/activity-log-viewer',    'history',           5),
    ('SYS-SEC', 'SYS_1107', 'User Company Mapping',    'form',   '/sys/form/user-company-mapping',     'domain',            7),
    ('SYS-SEC', 'SYS_1108', 'Approval Workflow Setup', 'form',   '/sys/form/approval-workflow-setup',  'approval',          8),
    ('SYS-SEC', 'SYS_1110', 'Approval Inbox',          'form',   '/sys/form/approval-inbox',           'inbox',             9),
    ('SYS-SEC', 'SYS_1109', 'Session / Login Monitor', 'form',   '/sys/form/session-login-monitor',    'devices',           10),
    -- â”€â”€ SYS Â· Configuration â”€â”€
    ('SYS-CFG', 'SYS_1201', 'Dynamic Menu Builder',    'form',   '/sys/form/dynamic-menu-builder',  'account_tree',      1),
    ('SYS-CFG', 'SYS_1202', 'Menu Enrollment',         'form',   '/sys/form/menu-enrollment',       'playlist_add_check',2),
    ('SYS-CFG', 'SYS_1203', 'File / Image Manager',    'form',   '/sys/form/file-image-manager',    'perm_media',        3),

    -- â”€â”€ HRM Â· HR Setup â”€â”€
    ('HRM-SETUP', 'HRM_1001', 'Employee Management',            'form', '/hrm/form/employee-management',            'badge',                  1),
    ('HRM-SETUP', 'HRM_1002', 'Department & Designation Setup', 'form', '/hrm/form/department-designation-setup',   'account_tree',           2),
    ('HRM-SETUP', 'HRM_1003', 'Shift Setup',                    'form', '/hrm/form/shift-setup',                    'schedule',               3),
    ('HRM-SETUP', 'HRM_1004', 'Grade / Pay-Scale Setup',        'form', '/hrm/form/grade-setup',                    'grade',                  4),
    ('HRM-SETUP', 'HRM_1005', 'Leave Type Setup',               'form', '/hrm/form/leave-type-setup',               'event_note',             5),
    ('HRM-SETUP', 'HRM_1006', 'Holiday Calendar',               'form', '/hrm/form/holiday-calendar',               'calendar_month',         6),
    ('HRM-SETUP', 'HRM_1007', 'Salary Component Setup',         'form', '/hrm/form/salary-component-setup',         'tune',                   7),
    ('HRM-SETUP', 'HRM_1008', 'HR Policy / Settings',           'form', '/hrm/form/hr-settings',                    'policy',                 8),
    ('HRM-SETUP', 'HRM_1009', 'Tax Slab Setup',                 'form', '/hrm/form/tax-slab-setup',                 'request_quote',          9),
    -- â”€â”€ HRM Â· Attendance & Time â”€â”€
    ('HRM-ATT', 'HRM_1101', 'Manual Attendance',       'form', '/hrm/form/manual-attendance',       'how_to_reg',     1),
    ('HRM-ATT', 'HRM_1102', 'Device-Sync Attendance',  'form', '/hrm/form/device-sync-attendance',  'fingerprint',    2),
    ('HRM-ATT', 'HRM_1103', 'Attendance Adjustment',   'form', '/hrm/form/attendance-adjustment',   'edit_calendar',  3),
    ('HRM-ATT', 'HRM_1104', 'Shift Roster',            'form', '/hrm/form/shift-roster',            'calendar_month', 4),
    ('HRM-ATT', 'HRM_1105', 'Overtime',                'form', '/hrm/form/overtime',                'more_time',      5),
    ('HRM-ATT', 'HRM_1106', 'Promotion / Transfer',    'form', '/hrm/form/promotion-transfer',      'swap_horiz',     6),
    -- â”€â”€ HRM Â· Payroll â”€â”€
    ('HRM-PAY', 'HRM_1201', 'Salary Structure Setup',  'form', '/hrm/form/salary-structure-setup',  'account_balance_wallet', 1),
    ('HRM-PAY', 'HRM_1202', 'Salary Processing',       'form', '/hrm/form/salary-processing',       'payments',       2),
    ('HRM-PAY', 'HRM_1203', 'Salary Sheet',            'form', '/hrm/form/salary-sheet',            'receipt_long',   3),
    ('HRM-PAY', 'HRM_1204', 'Payslip Generator',       'form', '/hrm/form/payslip-generator',       'receipt',        4),
    ('HRM-PAY', 'HRM_1205', 'Bonus / Festival',        'form', '/hrm/form/bonus-festival',          'celebration',    5),
    ('HRM-PAY', 'HRM_1206', 'Loan / Advance',          'form', '/hrm/form/loan-advance',            'payments',       6),
    ('HRM-PAY', 'HRM_1207', 'Final Settlement',        'form', '/hrm/form/final-settlement',        'price_check',    7),
    -- â”€â”€ HRM Â· Leave Management â”€â”€
    ('HRM-LEAVE', 'HRM_1301', 'Leave Application', 'form', '/hrm/form/leave-application', 'event_available', 1),
    ('HRM-LEAVE', 'HRM_1303', 'Leave Balance',     'form', '/hrm/form/leave-balance',     'event_available', 2),
    -- â”€â”€ HRM Â· Recruitment â”€â”€
    ('HRM-REC', 'HRM_1401', 'Job Requisition',        'form', '/hrm/form/job-requisition',  'work_outline',  1),
    ('HRM-REC', 'HRM_1402', 'Candidate / Application', 'form', '/hrm/form/candidate',        'person_search', 2),
    ('HRM-REC', 'HRM_1404', 'Offer & Onboarding',     'form', '/hrm/form/offer-onboarding', 'description',    3),

    -- â”€â”€ INV Â· Product & Setup â”€â”€
    ('INV-PROD', 'INV_1001', 'Product Master',          'form', '/inv/form/product-master-setup',    'inventory_2', 1),
    ('INV-PROD', 'INV_1002', 'Barcode Generator',       'form', '/inv/form/barcode-generator',       'qr_code',     2),
    ('INV-PROD', 'INV_1003', 'Category Setup',          'form', '/inv/form/category-setup',          'category',    3),
    ('INV-PROD', 'INV_1004', 'Brand Setup',             'form', '/inv/form/brand-setup',             'sell',        4),
    ('INV-PROD', 'INV_1005', 'Unit of Measure Setup',   'form', '/inv/form/unit-of-measure-setup',   'straighten',  5),
    ('INV-PROD', 'INV_1006', 'Product Attribute Setup', 'form', '/inv/form/product-attribute-setup', 'tune',        6),
    -- â”€â”€ INV Â· Stock Operations â”€â”€
    ('INV-STOCK', 'INV_1101', 'Opening Stock',    'form', '/inv/form/opening-stock-entry', 'inventory', 1),
    ('INV-STOCK', 'INV_1102', 'Stock Adjustment', 'form', '/inv/form/stock-adjustment',    'edit_note', 2),
    -- â”€â”€ INV Â· Warehouse & Transfer â”€â”€
    ('INV-WH', 'INV_2001', 'Warehouse Setup',           'form', '/inv/form/warehouse-setup',         'warehouse',                  1),
    ('INV-WH', 'INV_2002', 'Rack/Shelf Setup',          'form', '/inv/form/rack-shelf-setup',        'shelves',                    2),
    ('INV-WH', 'INV_2003', 'Stock Transfer',            'form', '/inv/form/stock-transfer',          'local_shipping',             3),
    ('INV-WH', 'INV_2004', 'Batch / Expiry Management', 'form', '/inv/form/batch-expiry-management',  'event_busy',                 4),
    ('INV-WH', 'INV_2005', 'Reorder Level Setup',       'form', '/inv/form/reorder-level-setup',     'production_quantity_limits',  5),

    -- â”€â”€ FIN Â· GL Setup â”€â”€
    ('FIN-SETUP', 'FIN_1001', 'Chart of Accounts', 'form', '/fin/form/chart-of-accounts', 'account_tree',    1),
    ('FIN-SETUP', 'FIN_1002', 'Account Group',     'form', '/fin/form/account-group',     'workspaces',      2),
    ('FIN-SETUP', 'FIN_1003', 'Voucher Type',      'form', '/fin/form/voucher-type',      'style',           3),
    ('FIN-SETUP', 'FIN_1005', 'Bank Account',      'form', '/fin/form/bank-account',      'account_balance', 4),
    ('FIN-SETUP', 'FIN_1006', 'GL Mapping',        'form', '/fin/form/gl-mapping',        'rule',            5),
    ('FIN-SETUP', 'FIN_1004', 'Opening Balance',   'form', '/fin/form/opening-balance',   'flag',            6),
    -- â”€â”€ FIN Â· Transactions â”€â”€
    ('FIN-TXN', 'FIN_1101', 'Voucher Entry',           'form', '/fin/form/voucher-entry',       'post_add',       1),
    ('FIN-TXN', 'FIN_1102', 'Bank Reconciliation',     'form', '/fin/form/bank-reconciliation', 'fact_check',     2),
    ('FIN-TXN', 'FIN_1201', 'Auto-Posting Monitor',    'form', '/fin/form/posting-monitor',     'sync_problem',   3),
    ('FIN-TXN', 'FIN_1401', 'Period / Year-End Close', 'form', '/fin/form/period-close',        'event_available',4),
    -- â”€â”€ FIN Â· Financial Reports â”€â”€
    ('FIN-REPORT', 'FIN_1301', 'Trial Balance',   'form', '/fin/report/trial-balance',   'balance',          1),
    ('FIN-REPORT', 'FIN_1302', 'Account Ledger',  'form', '/fin/report/account-ledger',  'menu_book',        2),
    ('FIN-REPORT', 'FIN_1303', 'Day Book',        'form', '/fin/report/day-book',        'today',            3),
    ('FIN-REPORT', 'FIN_1304', 'Profit & Loss',   'form', '/fin/report/profit-loss',     'trending_up',      4),
    ('FIN-REPORT', 'FIN_1305', 'Balance Sheet',   'form', '/fin/report/balance-sheet',   'account_balance',  5),
    ('FIN-REPORT', 'FIN_1306', 'Cash Flow',       'form', '/fin/report/cash-flow',       'payments',         6),
    ('FIN-REPORT', 'FIN_1307', 'AR / AP Aging',   'form', '/fin/report/aging',           'hourglass_bottom', 7),

    -- â”€â”€ PUR Â· Purchase Setup â”€â”€
    ('PUR-SETUP', 'PUR_1001', 'Supplier Management', 'form', '/pur/form/supplier-management', 'local_shipping', 1),
    ('PUR-SETUP', 'PUR_1002', 'Supplier Price List', 'form', '/pur/form/supplier-price-list', 'price_change', 2),
    -- â”€â”€ PUR Â· Purchase Transactions â”€â”€
    ('PUR-TXN', 'PUR_1101', 'Purchase Order',    'form', '/pur/form/purchase-order',    'shopping_cart',     0),
    ('PUR-TXN', 'PUR_1102', 'Purchase Invoice',  'form', '/pur/form/purchase-invoice',  'receipt_long',      1),
    ('PUR-TXN', 'PUR_1105', 'Goods Receipt Note','form', '/pur/form/goods-receipt-note','inventory',         2),
    ('PUR-TXN', 'PUR_1106', 'Landed Cost',       'form', '/pur/form/landed-cost',       'price_change',      3),
    ('PUR-TXN', 'PUR_1104', 'Supplier Payment',  'form', '/pur/form/supplier-payment',  'payments',          4),
    ('PUR-TXN', 'PUR_1103', 'Purchase Return',   'form', '/pur/form/purchase-return',   'assignment_return', 5),

    -- â”€â”€ SAL Â· Sales Setup â”€â”€
    ('SAL-SETUP', 'SAL_1101', 'Customer Management', 'form', '/sal/form/customer-management', 'groups', 1),
    -- â”€â”€ SAL Â· Sales Transactions â”€â”€
    ('SAL-TXN', 'SAL_1001', 'Sales Invoice',    'form', '/sal/form/sales-invoice',    'point_of_sale',       1),
    ('SAL-TXN', 'SAL_1102', 'Customer Receipt', 'form', '/sal/form/customer-receipt', 'request_quote',       2),
    ('SAL-TXN', 'SAL_1103', 'Sales Return',     'form', '/sal/form/sales-return',     'assignment_returned', 3);

-- 3a) INSERT menus whose form_id does not yet exist
INSERT INTO sys_menu (submodule_no, form_id, form_name, menu_type, route_path, icon_name, order_sl, is_active, is_deleted, row_version, created_at, created_by)
SELECT s.submodule_no, t.form_id, t.form_name, t.menu_type, t.route_path, t.icon, t.sl, 1, 0, 1, now(), (SELECT min(user_no) FROM sys_user WHERE is_deleted = 0)
FROM _menu_seed t
JOIN sys_submodule s ON s.submodule_code = t.scode
WHERE NOT EXISTS (SELECT 1 FROM sys_menu mn WHERE mn.form_id = t.form_id);

-- 3b) SYNC existing menus whose route_path / name / icon / submodule has changed
UPDATE sys_menu mn
SET route_path   = t.route_path,
    form_name    = t.form_name,
    icon_name    = t.icon,
    menu_type    = t.menu_type,
    order_sl     = t.sl,
    submodule_no = s.submodule_no,
    updated_at   = now()
FROM _menu_seed t
JOIN sys_submodule s ON s.submodule_code = t.scode
WHERE mn.form_id = t.form_id
  AND ( mn.route_path   IS DISTINCT FROM t.route_path
     OR mn.form_name    IS DISTINCT FROM t.form_name
     OR mn.icon_name    IS DISTINCT FROM t.icon
     OR mn.menu_type    IS DISTINCT FROM t.menu_type
     OR mn.submodule_no IS DISTINCT FROM s.submodule_no );

-- â”€â”€ 4) ENROLL every menu of these modules for COMPANY 2 (branch NULL = all) â”€â”€
INSERT INTO sys_enroll_menu (company_no, branch_no, menu_no, is_lifetime, is_active, is_deleted, row_version, created_at, created_by)
SELECT 2, NULL, mn.menu_no, 1, 1, 0, 1, now(), (SELECT min(user_no) FROM sys_user WHERE is_deleted = 0)
FROM sys_menu mn
JOIN sys_submodule s  ON s.submodule_no = mn.submodule_no
JOIN sys_module    mo ON mo.module_no   = s.module_no
WHERE mo.module_code IN ('SYS','HRM','INV','FIN','PUR','SAL')
  AND mn.is_deleted = 0
  AND NOT EXISTS (SELECT 1 FROM sys_enroll_menu e WHERE e.company_no = 2 AND e.menu_no = mn.menu_no AND e.is_deleted = 0);

COMMIT;

```

## SYS Route Fix Migration

```sql
-- ============================================================================
-- fix_sys_menu_routes.sql â€” point SYS menus at the real Angular FRONTEND routes.
-- The original base seed stored the API path (/sys/forms/sys1001) in route_path,
-- which the sidebar binds verbatim â†’ no Angular route matches â†’ dead navigation.
-- After running this you MUST log out and log back in (the menu is cached in
-- sessionStorage from the login response).
-- ============================================================================
BEGIN;

UPDATE sys_menu mn
SET route_path = v.rp,
    menu_type  = v.mt,
    updated_at = now()
FROM (VALUES
    ('SYS_1001', '/sys/form/company-setup',          'form'),
    ('SYS_1002', '/sys/form/branch-setup',           'form'),
    ('SYS_1003', '/sys/form/financial-year-setup',   'form'),
    ('SYS_1004', '/sys/form/currency-setup',         'form'),
    ('SYS_1005', '/sys/form/vat-tax-setup',          'form'),
    ('SYS_1006', '/sys/form/exchange-rate-setup',    'form'),
    ('SYS_1007', '/sys/form/cost-center-setup',      'form'),
    ('SYS_1008', '/sys/form/system-settings',        'form'),
    ('SYS_1101', '/sys/form/user-management',         'form'),
    ('SYS_1102', '/sys/form/role-management',         'form'),
    ('SYS_1103', '/sys/report/role-permission-matrix','report'),
    ('SYS_1104', '/sys/form/user-branch-mapping',     'form'),
    ('SYS_1105', '/sys/report/activity-log-viewer',   'report'),
    ('SYS_1107', '/sys/form/user-company-mapping',    'form'),
    ('SYS_1108', '/sys/form/approval-workflow-setup', 'form'),
    ('SYS_1110', '/sys/form/approval-inbox',          'form'),
    ('SYS_1109', '/sys/form/session-login-monitor',   'form'),
    ('SYS_1201', '/sys/form/dynamic-menu-builder',    'form'),
    ('SYS_1202', '/sys/form/menu-enrollment',         'form'),
    ('SYS_1203', '/sys/form/file-image-manager',      'form')
) AS v(form_id, rp, mt)
WHERE mn.form_id = v.form_id
  AND mn.route_path IS DISTINCT FROM v.rp;

UPDATE sys_menu
SET is_active = 0,
    updated_at = now()
WHERE form_id = 'SYS_1106'
  AND is_deleted = 0
  AND is_active <> 0;

COMMIT;

```

## Role Permission Grant Migration

```sql
-- ============================================================================
-- grant_role_permissions.sql â€” enroll all menus for COMPANY 2 + grant ALL
-- actions to ROLE 1. Idempotent reset (clears role 1's perms first, re-grants).
-- ----------------------------------------------------------------------------
-- IMPORTANT differences from a naive grant:
--   â€¢ sys_role_permission has NO company_no / branch_no columns (it extends
--     AuditEntity; permissions live on the company-scoped role template).
--   â€¢ The menu only shows a form when can_view = 1, so EVERY can_* flag must be
--     set â€” granting only menu_no leaves all forms invisible.
-- After running this, re-login (perms are baked into the JWT at login).
-- ============================================================================
BEGIN;

-- 1) Clear ROLE 1's existing permissions (scoped â€” do NOT wipe all roles)
DELETE FROM sys_role_permission WHERE role_no = 1;

-- 2) Enroll every menu of these modules for COMPANY 2 (branch NULL = all branches)
INSERT INTO sys_enroll_menu (company_no, branch_no, menu_no, is_lifetime, is_active, is_deleted, row_version, created_at, created_by)
SELECT 2, NULL, mn.menu_no, 1, 1, 0, 1, now(), (SELECT min(user_no) FROM sys_user WHERE is_deleted = 0)
FROM sys_menu mn
JOIN sys_submodule s  ON s.submodule_no = mn.submodule_no
JOIN sys_module    mo ON mo.module_no   = s.module_no
WHERE mo.module_code IN ('SYS','HRM','INV','FIN','PUR','SAL')
  AND mn.is_deleted = 0
  AND NOT EXISTS (SELECT 1 FROM sys_enroll_menu e WHERE e.company_no = 2 AND e.menu_no = mn.menu_no AND e.is_deleted = 0);

-- 3) Grant ALL actions on every enrolled menu to ROLE 1
INSERT INTO sys_role_permission
    (role_no, menu_no, can_view, can_insert, can_update, can_delete, can_approve, can_post, can_cancel, can_export,
     is_active, is_deleted, row_version, created_at, created_by)
SELECT
    1, em.menu_no, 1, 1, 1, 1, 1, 1, 1, 1,
    1, 0, 1, now(), (SELECT min(user_no) FROM sys_user WHERE is_deleted = 0)
FROM sys_enroll_menu em
WHERE em.company_no = 2
  AND em.is_deleted = 0;

COMMIT;

```

## HR to HRM Rename Migration

```sql
-- ============================================================================
-- HR -> HRM module rename â€” DATA / SCHEMA migration (PostgreSQL)
-- Run ONCE, after deploying the renamed backend (ddl-auto: none â†’ not auto-applied).
-- Wrap in a transaction; review the route_path / module_code steps against your data.
-- After this runs, users must re-login (or let the access token refresh) so the JWT
-- `perms` claim is re-issued with the new HRM_ form ids.
-- ============================================================================
BEGIN;

-- 1) RBAC form ids:  HR_1106 -> HRM_1106   (sys_menu.form_id)
UPDATE sys_menu
   SET form_id = 'HRM_' || substring(form_id from 4)
 WHERE form_id ~ '^HR_';

-- 2) Backend route mapping used by RbacAuthorizationInterceptor (sys_menu.route_path).
--    The interceptor matches the request URI against route_path, so it MUST track the new
--    /api/v1/hrm/ base and hrm{NNNN} form slugs. ADJUST the patterns below to your stored
--    format (some installs store '/api/v1/hr/...', others a prefix like '/hr/forms/hr1106').
UPDATE sys_menu
   SET route_path = regexp_replace(
                      regexp_replace(route_path, '/hr/',     '/hrm/',    'g'),
                      'forms/hr([0-9])', 'forms/hrm\1', 'g')
 WHERE route_path LIKE '%/hr/%' OR route_path LIKE '%forms/hr%';

-- 3) Approval workflow document types:  HR_LEAVE -> HRM_LEAVE  (config + live runtime rows)
UPDATE sys_approval_workflow
   SET document_type = 'HRM_' || substring(document_type from 4)
 WHERE document_type ~ '^HR_';
UPDATE sys_approval_request
   SET document_type = 'HRM_' || substring(document_type from 4)
 WHERE document_type ~ '^HR_';

-- 4) OPTIONAL â€” module code, if the HR module is keyed as 'HR' (drives the frontend
--    PermissionConfig grouping). Uncomment and set to your actual values.
-- UPDATE sys_module SET module_code = 'HRM' WHERE module_code = 'HR';

-- 5) Physical table renames  hr_* -> hrm_*  (indexes / FKs / sequences follow automatically)
ALTER TABLE hr_attendance            RENAME TO hrm_attendance;
ALTER TABLE hr_attendance_adjustment RENAME TO hrm_attendance_adjustment;
ALTER TABLE hr_bonus_line            RENAME TO hrm_bonus_line;
ALTER TABLE hr_bonus_run             RENAME TO hrm_bonus_run;
ALTER TABLE hr_candidate             RENAME TO hrm_candidate;
ALTER TABLE hrm_department            RENAME TO hrm_department;
ALTER TABLE hrm_designation           RENAME TO hrm_designation;
ALTER TABLE hrm_employee              RENAME TO hrm_employee;
ALTER TABLE hrm_employee_movement     RENAME TO hrm_employee_movement;
ALTER TABLE hr_final_settlement      RENAME TO hrm_final_settlement;
ALTER TABLE hr_grade                 RENAME TO hrm_grade;
ALTER TABLE hr_holiday               RENAME TO hrm_holiday;
ALTER TABLE hr_job_requisition       RENAME TO hrm_job_requisition;
ALTER TABLE hr_leave_application     RENAME TO hrm_leave_application;
ALTER TABLE hr_leave_balance         RENAME TO hrm_leave_balance;
ALTER TABLE hr_leave_ledger          RENAME TO hrm_leave_ledger;
ALTER TABLE hr_leave_type            RENAME TO hrm_leave_type;
ALTER TABLE hr_loan_advance          RENAME TO hrm_loan_advance;
ALTER TABLE hr_offer                 RENAME TO hrm_offer;
ALTER TABLE hr_overtime              RENAME TO hrm_overtime;
ALTER TABLE hr_payroll_run           RENAME TO hrm_payroll_run;
ALTER TABLE hr_payslip               RENAME TO hrm_payslip;
ALTER TABLE hr_payslip_dtl           RENAME TO hrm_payslip_dtl;
ALTER TABLE hr_salary_component      RENAME TO hrm_salary_component;
ALTER TABLE hr_salary_structure      RENAME TO hrm_salary_structure;
ALTER TABLE hr_salary_structure_dtl  RENAME TO hrm_salary_structure_dtl;
ALTER TABLE hr_shift                 RENAME TO hrm_shift;
ALTER TABLE hr_shift_roster          RENAME TO hrm_shift_roster;
ALTER TABLE hr_shift_roster_line     RENAME TO hrm_shift_roster_line;

COMMIT;

```

## HRM Employee User Provisioning Migration

```sql
-- ============================================================================
-- HRM employee â†’ user provisioning: add the is_create_user flag (PostgreSQL).
-- hrm_employee already exists, so this is an additive column. Safe to re-run.
-- ============================================================================
ALTER TABLE hrm_employee
    ADD COLUMN IF NOT EXISTS is_create_user smallint NOT NULL DEFAULT 0;

-- Backfill: mark employees who already have a (non-deleted) login user.
UPDATE hrm_employee e
   SET is_create_user = 1
 WHERE e.is_create_user <> 1
   AND EXISTS (SELECT 1 FROM sys_user u
                WHERE u.employee_no = e.employee_no
                  AND u.is_deleted = 0);

```

---

## SYS Applied Migration Log

Newest first. The schema blocks above are kept in sync with these, so this file
always reflects the CURRENT state of the SYS tables.

| Applied | Migration file | Scope |
|---|---|---|
| 2026-08-16 | `2026-08-16_1500_sys_menu_pur1002_supplier_price_list.sql` | **PUR_1002 Supplier Price List had no menu row.** The only PUR form without one: the Angular route, the component and `Pur1002Controller` all existed, but with no `sys_menu` row there was no enrolment and no grant, so `RbacAuthorizationInterceptor` rejected every call with 403 — verified live. Menu (`/pur/form/supplier-price-list`), enrolment for every company entitled to PUR_1001, and grants mirroring it minus the document actions. |
| 2026-08-16 | `2026-08-16_1300_sys_doc_sequence_audit_columns.sql` | **The nine audit columns on `sys_doc_sequence`.** The table carried none of them, so the `DocSequence` entity had to `Ignore()` all nine and any query filtering `is_deleted` failed at runtime with a LINQ translation error — SYS_1301's config list hit exactly that. Also means a series can now be soft-deleted rather than dropped, which matters because hard-deleting one that has issued numbers would let a later document reuse a number already printed on a supplier's invoice. Existing rows default to live. Adds `idx_sys_doc_sequence_live`. |
| 2026-08-16 | `2026-08-16_1200_sys_menu_sys1301_id_generator.sql` | Menu entry for **SYS_1301 ID Generator** (`/sys/form/id-generator`), beside SYS_1108 Approval Workflow Setup — both are cross-cutting configuration that decides how documents behave everywhere else. Full CRUD grant; deleting a series that has issued numbers is refused by the service regardless of permission. |
| 2026-08-16 | `2026-08-16_1100_sys_menu_pur_grn_landed_cost.sql` | Menu entries for **PUR_1105 Goods Receipt Note** and **PUR_1106 Landed Cost**. |
| 2026-08-16 | `2026-08-16_1000_sys_enrol_pur_inv_company2.sql` | Enrolment for the PUR and INV forms on company 2. |
| 2026-08-14 | `2026-08-14_1400_sys_doc_sequence_pattern.sql` | **SYS_1301 pattern-based document numbering.** `sys_doc_sequence` gains `pattern` (template such as `INV-{FY_YY_YY}-{SEQ:6}`; NULL keeps the old prefix+padding behaviour), `doc_sub_type`, `menu_no`, `starting_no`. Adds a unique index on (company, branch, doc_type, doc_sub_type, COALESCE(fin_year_no,0)) — two live series for one document type would hand out the same number twice. Additive only; existing series are untouched. Completes the last Java form not covered by the .NET port. |
| 2026-08-14 | `2026-08-14_1300_sys_remove_duplicate_fin_year.sql` | **Retired the duplicate overlapping financial year (company 2).** Years 9 (FY-JUN26-DEC26) and 10 (FY-JUL26-DEC26) both covered today and were both open, so the single-row year lookup every posting path uses returned whichever the planner offered first — the same transaction could land in either year. Year 9 holds all the data (6 vouchers, 15 ledger legs, opening balances, the POS sale); year 10 held only its own 6 period rows. Year 10 and its periods soft-deleted, guarded by NOT EXISTS against fin_ledger / fin_voucher / sal_invoice / inv_stock_ledger. One open year now covers today, and zero overlapping pairs remain across all companies. |
| 2026-08-14 | `2026-08-14_1200_sys_enrol_sal_inv_company2.sql` | Enrolled INV_1101/1102 and SAL_1001/1002/1101/1102/1103 for company 2. All were granted at role level but had no `sys_enroll_menu` row, so the login query never put them in the JWT `perms` claim and every request 403d — including the POS sale endpoint. |
| 2026-08-14 | `2026-08-14_1100_sys_ip_address_to_varchar.sql` | **`sys_log.ip_address` and `sys_login_attempt.ip_address` converted `inet` → `varchar(45)`.** Both were `inet` while every entity, DTO and raw-SQL projection treats an IP as a string, so Npgsql failed the insert with `42804`. Live effect: nothing was written to `sys_log` for any POST/PUT/PATCH/DELETE (every POS sale included), and `sys_login_attempt` — which the brute-force limiter reads — failed on every login outcome. Brings these two in line with `sys_audit_log`, `sys_session` and `hrm_leave_audit_log`, which were already `varchar(45)`. |
| 2026-08-01 | `2026-08-01_1400_sys_notification.sql` | Notification + template tables (back-filled; were undocumented) |
| 2026-08-01 | `2026-08-01_1200_sys_role_permission_data_scope.sql` | Row-level data scope on role permissions |
| 2026-07-22 | `2026-07-22_1009_sys_approval_request_scope_routing.sql` | Approval workflow step routing |
| 2026-07-21 | `2026-07_hrm_leave_management_alignment_pure_ddl.sql` | SLA/escalation columns + `sys_approval_delegation` (driven by the HRM leave work) |

### 2026-08-01 — Notification tables (back-filled)

**Added:** `sys_notification`, `sys_notification_template`, four indexes on the
former and one on the latter.

**Why this entry exists at all:** both tables were created by hand directly
against the development database. They were mapped by EF entities and read by a
live controller, but no migration script created them and they appeared nowhere
in this file — so the feature could not have been deployed to a fresh
environment. The script reconstructs them from the mapped entities; it is
idempotent, so running it against the dev database that already has them is a
no-op.

**Indexes:** `idx_sys_notification_inbox` matches the inbox query exactly
(`target_user_no, is_deleted, company_no` then `notification_no DESC` for the
sort), and `idx_sys_notification_unread` is a partial index over unread rows only
— the badge counts them on every poll, and they are a small fraction of the table.

### 2026-08-01 — Role permission data scope

**Added:** `sys_role_permission.data_scope SMALLINT NOT NULL DEFAULT 1` +
`CONSTRAINT chk_sys_rp_data_scope CHECK (data_scope IN (1,2,3))`.

**Why:** row-level security. `perm_scope` and `record_filter` could only express
"company vs branch" and "all vs the rows I created". Neither can say *"this role
sees its own department"* or *"this role sees only its own employee record"* —
the two narrowings an HRM module actually needs for leave, attendance and
payslips. `data_scope` adds that dimension, configured per form in **SYS_1103
Role Permission Matrix** and enforced by
`DataScopeExtensions.ApplyDataScope`.

**Values:** `1 = BRANCH` (no narrowing beyond the tenant filter), `2 =
DEPARTMENT`, `3 = EMPLOYEE`.

**Default is BRANCH, deliberately.** Every role in every existing install was
configured before this column existed. Defaulting to EMPLOYEE would silently cut
every user down to their own records the moment enforcement went live; admins
tighten the scope explicitly instead. Across multiple roles the **widest**
(lowest) scope wins — `MIN(COALESCE(rp.data_scope, 1))`, matching how the other
scope dimensions aggregate.

**Note:** a superseded draft of this migration created the column with
`DEFAULT 3`. The shipped script carries a clearly-marked one-time `UPDATE` for
installs that ran it. See the script's footer.

### 2026-07-22 — Approval workflow step routing

**Added:** `sys_approval_request.scope_no` (nullable) + indexes
`idx_approval_request_scope`, `idx_approval_request_type_status`,
`idx_approval_request_step_emp`.

**Why:** the engine could not honour the SYS_1108 configuration when an approver
acted. Storing the scope lets `ApprovalService.act()` read the current step's
`step_type` (1=Forward, 2=Backward, 3=Reject) and `next_step_no` instead of
blindly advancing to the next-highest step number. Nullable so requests raised
before this migration still complete (they fall back to sequential advancement).

**Engine behaviour that now depends on this column:**

- **Scope resolution is most-specific-wins** — a scope naming branch **and**
  department beats branch-only, which beats company-wide. A scope naming a
  branch/department the document does not belong to is never eligible. No
  eligible scope ⇒ auto-approve (an unconfigured company is never blocked).
- **Step routing** — `next_step_no` is followed when set; otherwise Forward goes
  to the next higher step and Backward to the previous lower one. A Backward
  route re-opens the target step so its approvers can act again. `is_final = 1`
  or no remaining target ⇒ the document is fully approved.
- **Approver identity** — steps store `emp_no` (`sys_user.employee_no`), which is
  NOT the login `user_no`. The engine resolves user → employee before
  authorising, and the approval inbox fails closed (empty list) when the logged-in
  user has no employee record.
