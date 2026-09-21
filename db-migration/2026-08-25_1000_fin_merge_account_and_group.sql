-- ============================================================================
-- Migration: 2026-08-25_1000_fin_merge_account_and_group.sql
-- Module: FIN (Financial Accounting / General Ledger)
-- Description: Merges fin_account_group and fin_account into a single unified
--              hierarchical table (fin_account) with is_group, parent_account_no,
--              reporting classifications, and dimensional requirement flags.
-- ============================================================================

BEGIN;

-- 1. Add new unified columns to fin_account if they do not exist
ALTER TABLE fin_account
    ADD COLUMN IF NOT EXISTS parent_account_no bigint,
    ADD COLUMN IF NOT EXISTS is_group smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS is_control smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS order_sl integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS account_category smallint,
    ADD COLUMN IF NOT EXISTS sub_category smallint,
    ADD COLUMN IF NOT EXISTS account_class smallint,
    ADD COLUMN IF NOT EXISTS requires_cost_center smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS requires_party smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS requires_reconciliation smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS requires_branch smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS requires_project smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS requires_department smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS opening_balance numeric(20,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS opening_dr_cr varchar(2) NOT NULL DEFAULT 'dr',
    ADD COLUMN IF NOT EXISTS description text;

-- 2. Make account_group_no nullable if it is currently NOT NULL
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'fin_account' AND column_name = 'account_group_no' AND is_nullable = 'NO'
    ) THEN
        ALTER TABLE fin_account ALTER COLUMN account_group_no DROP NOT NULL;
    END IF;

    -- Ensure default constraints on existing columns
    BEGIN
        ALTER TABLE fin_account ALTER COLUMN opening_balance SET DEFAULT 0;
        ALTER TABLE fin_account ALTER COLUMN opening_dr_cr SET DEFAULT 'dr';
        ALTER TABLE fin_account ALTER COLUMN is_active SET DEFAULT 1;
        ALTER TABLE fin_account ALTER COLUMN is_deleted SET DEFAULT 0;
        ALTER TABLE fin_account ALTER COLUMN row_version SET DEFAULT 0;
    EXCEPTION WHEN OTHERS THEN NULL;
    END;
END $$;

-- 3. If fin_account_group exists, migrate existing groups into fin_account as group nodes (is_group = 1)
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'fin_account_group') THEN
        -- Temporary mapping table to translate old group IDs to new account_no
        CREATE TEMP TABLE temp_group_map (
            old_group_no bigint,
            new_account_no bigint
        ) ON COMMIT DROP;

        -- Insert groups as header accounts
        INSERT INTO fin_account (
            account_code,
            account_name,
            root_type,
            normal_balance,
            is_group,
            is_postable,
            is_control,
            requires_cost_center,
            requires_party,
            requires_reconciliation,
            requires_branch,
            requires_project,
            requires_department,
            opening_balance,
            opening_dr_cr,
            order_sl,
            company_no,
            branch_no,
            is_active,
            is_deleted,
            created_at,
            created_by,
            row_version
        )
        SELECT
            g.account_group_id,
            g.group_name,
            g.root_type,
            COALESCE(g.normal_balance, CASE WHEN g.root_type IN (1, 5) THEN 'dr' ELSE 'cr' END),
            1 AS is_group,
            0 AS is_postable,
            COALESCE(g.is_control, 0),
            0 AS requires_cost_center,
            0 AS requires_party,
            0 AS requires_reconciliation,
            0 AS requires_branch,
            0 AS requires_project,
            0 AS requires_department,
            0.0000 AS opening_balance,
            'dr' AS opening_dr_cr,
            COALESCE(g.order_sl, 0),
            g.company_no,
            g.branch_no,
            COALESCE(g.is_active, 1),
            COALESCE(g.is_deleted, 0),
            COALESCE(g.created_at, CURRENT_TIMESTAMP),
            g.created_by,
            COALESCE(g.row_version, 0)
        FROM fin_account_group g
        WHERE NOT EXISTS (
            SELECT 1 FROM fin_account a
            WHERE a.account_code = g.account_group_id
              AND a.company_no = g.company_no
              AND a.is_deleted = 0
        );

        -- Map old group numbers to new account numbers
        INSERT INTO temp_group_map (old_group_no, new_account_no)
        SELECT g.account_group_no, a.account_no
        FROM fin_account_group g
        JOIN fin_account a ON a.account_code = g.account_group_id AND a.company_no = g.company_no AND a.is_group = 1;

        -- Update parent relationships for groups (parent_group_no -> parent_account_no)
        UPDATE fin_account a
        SET parent_account_no = m_parent.new_account_no
        FROM fin_account_group g
        JOIN temp_group_map m_self ON m_self.old_group_no = g.account_group_no
        JOIN temp_group_map m_parent ON m_parent.old_group_no = g.parent_group_no
        WHERE a.account_no = m_self.new_account_no;

        -- Update parent relationships for leaf accounts (account_group_no -> parent_account_no)
        UPDATE fin_account a
        SET parent_account_no = m.new_account_no
        FROM temp_group_map m
        WHERE a.account_group_no = m.old_group_no AND a.is_group = 0 AND a.parent_account_no IS NULL;

    END IF;
END $$;

-- 4. Create optimized indexes on the unified fin_account table
CREATE INDEX IF NOT EXISTS idx_fin_acc_parent ON fin_account (parent_account_no, is_deleted);
CREATE INDEX IF NOT EXISTS idx_fin_acc_is_group ON fin_account (company_no, is_group, is_deleted);
CREATE INDEX IF NOT EXISTS idx_fin_acc_order ON fin_account (company_no, order_sl, is_deleted);
CREATE UNIQUE INDEX IF NOT EXISTS uq_fin_acc_code_company ON fin_account (company_no, account_code) WHERE is_deleted = 0;

COMMIT;
