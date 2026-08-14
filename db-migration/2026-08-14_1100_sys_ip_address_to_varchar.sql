-- ═══════════════════════════════════════════════════════════════════════════
-- SYS — align ip_address column types with the code (sys_log, sys_login_attempt)
-- Generated: 2026-08-14   |   run in pgAdmin
--
-- WHAT THIS FIXES
--   Both columns are `inet`, but every entity, DTO and raw-SQL projection in the
--   codebase treats an IP address as a string. Npgsql therefore fails the insert:
--
--     42804: column "ip_address" is of type inet but expression is of type
--            character varying
--
--   Consequences observed against the live database:
--     · sys_log        — AuditLogMiddleware logs every POST/PUT/PATCH/DELETE.
--                        Every mutating request threw, so nothing was audited.
--                        That includes every POS sale.
--     · sys_login_attempt — LoginAttemptService writes a row for every login
--                        outcome, which is also what the brute-force limiter
--                        reads. A failing insert here affects sign-in itself.
--
--   Neither failure is visible in a build or a unit test: the column exists, so
--   the schema-drift check passes — it compares column NAMES, not types.
--
-- WHY varchar RATHER THAN CHANGING THE CODE
--   `inet` is the better type in the abstract — it supports subnet queries. But
--   nothing in this system queries by subnet, and three of the five ip_address
--   columns (sys_audit_log, sys_session, hrm_leave_audit_log) are ALREADY
--   varchar(45). Making these two match means all five agree and every existing
--   read path keeps working. Converting the code instead would mean a value
--   converter on two entities plus casts in the Sys1109 raw SQL, to preserve a
--   capability no caller uses.
--
--   If you would rather keep `inet`, do NOT run this — the alternative fix is a
--   string<->IPAddress value conversion on both entities and `host(ip_address)`
--   casts in Sys1109Service's raw queries.
--
-- SAFETY
--   Idempotent — guarded on the current data type, so a second run is a no-op.
--   `host()` renders the address without a netmask ('203.0.113.7'), which is
--   what a client IP should be. Existing rows are preserved, not truncated:
--   an IPv6 address is at most 45 characters.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

DO $$
BEGIN
    -- Guarded conversions. A DO block is used ONLY because PostgreSQL has no
    -- conditional ALTER; no business logic lives here.
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'sys_log' AND column_name = 'ip_address' AND data_type = 'inet'
    ) THEN
        ALTER TABLE sys_log
            ALTER COLUMN ip_address TYPE varchar(45) USING host(ip_address);
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'sys_login_attempt' AND column_name = 'ip_address' AND data_type = 'inet'
    ) THEN
        ALTER TABLE sys_login_attempt
            ALTER COLUMN ip_address TYPE varchar(45) USING host(ip_address);
    END IF;
END $$;

COMMIT;


-- ═══════════════════════════════════════════════════════════════════════════
-- VERIFICATION
-- ═══════════════════════════════════════════════════════════════════════════

-- All five should now read 'character varying':
-- SELECT table_name, data_type FROM information_schema.columns
-- WHERE column_name = 'ip_address' ORDER BY table_name;

-- Then issue any POST against the API and confirm a row lands:
-- SELECT request_uri, http_method, response_status, ip_address, request_at
-- FROM sys_log ORDER BY log_no DESC LIMIT 5;
