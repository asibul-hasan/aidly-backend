-- ═══════════════════════════════════════════════════════════════════════════
-- SYS — pattern-based document numbering for SYS_1301 (all companies)
-- Generated: 2026-08-14   |   run in pgAdmin
--
-- WHAT THIS ADDS
--   sys_doc_sequence currently supports only prefix + padded counter. SYS_1301
--   lets a company describe the whole number as a pattern, e.g.
--
--     INV-{FY_YY_YY}-{SEQ:6}      ->  INV-26-27-000042
--     {BRANCH}/{YY}{MM}/{SEQ:4}   ->  DHK/2608/0042
--
--   Four columns are needed for that:
--
--     pattern       the template; NULL keeps the old prefix+padding behaviour
--     doc_sub_type  distinguishes series within one doc_type (e.g. cash vs credit)
--     menu_no       which form the series belongs to, so the setup screen can list them
--     starting_no   the number the series began at, kept separate from next_no so a
--                   reset can return to it without guessing
--
--   Existing rows keep working untouched: pattern stays NULL and the generator
--   falls back to prefix + padding exactly as before. This is additive only.
--
-- SAFETY
--   Idempotent. Pure DDL. No data is rewritten.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS pattern      VARCHAR(200);
ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS doc_sub_type VARCHAR(30)  NOT NULL DEFAULT '';
ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS menu_no      BIGINT;
ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS starting_no  BIGINT       NOT NULL DEFAULT 1;

-- One live series per company/branch/doc_type/sub_type/fin-year. Without this two
-- rows can hand out the same number to the same series, which is the one failure a
-- numbering table must not have. fin_year_no is nullable, so it is coalesced.
CREATE UNIQUE INDEX IF NOT EXISTS uq_sys_doc_sequence_series
    ON sys_doc_sequence (company_no, branch_no, doc_type, doc_sub_type,
                         COALESCE(fin_year_no, 0));

CREATE INDEX IF NOT EXISTS idx_sys_doc_sequence_menu
    ON sys_doc_sequence (menu_no);

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════

-- SELECT column_name, data_type, is_nullable, column_default
-- FROM information_schema.columns
-- WHERE table_name = 'sys_doc_sequence'
-- ORDER BY ordinal_position;
--   → pattern, doc_sub_type, menu_no, starting_no must be present.

-- Existing series must be unaffected (pattern NULL = legacy prefix behaviour):
-- SELECT doc_type, prefix, padding, next_no, pattern FROM sys_doc_sequence ORDER BY doc_sequence_no;
