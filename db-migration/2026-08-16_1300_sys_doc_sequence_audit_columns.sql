-- ═══════════════════════════════════════════════════════════════════════════
-- SYS — audit columns on sys_doc_sequence
-- Generated: 2026-08-16   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   sys_doc_sequence has none of the nine audit columns, so SysDbContext had to
--   Ignore() every one of them on the DocSequence entity. Any query filtering
--   is_deleted then failed at runtime with "Translation of member 'IsDeleted'
--   ... failed. This commonly occurs when the specified member is unmapped."
--   SYS_1301's config list hit exactly that.
--
--   The project rule is that an entity extending AuditEntity carries all nine
--   columns; this table was the exception. It also means a series cannot be
--   soft-deleted, and hard-deleting one that has issued numbers would let a
--   later document reuse a number already printed on a customer's invoice.
--
--   Existing rows default to live (is_deleted = 0, is_active = 1), so nothing
--   currently in use changes behaviour.
--
-- SAFETY
--   Idempotent. Pure DDL. Additive only.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS is_active  SMALLINT     NOT NULL DEFAULT 1;
ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS is_deleted SMALLINT     NOT NULL DEFAULT 0;
ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS created_by BIGINT;
ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS created_at TIMESTAMP(6) NOT NULL DEFAULT now();
ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS updated_by BIGINT;
ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP(6);
ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS deleted_by BIGINT;
ALTER TABLE sys_doc_sequence ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMP(6);

-- Series lookups always filter on the live flag.
CREATE INDEX IF NOT EXISTS idx_sys_doc_sequence_live
    ON sys_doc_sequence (company_no, doc_type, is_deleted);

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════
-- SELECT column_name FROM information_schema.columns
-- WHERE table_name = 'sys_doc_sequence'
--   AND column_name IN ('is_active','is_deleted','created_by','created_at',
--                       'updated_by','updated_at','deleted_by','deleted_at')
-- ORDER BY column_name;
--   → all eight present (row_version already existed).
