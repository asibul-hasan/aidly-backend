using System.Net;
using Npgsql;

namespace AidlyErp.Api.Middleware;

/// <summary>
/// Turns a PostgreSQL failure into an HTTP status and a sentence a user can act on.
///
/// <para><b>Keyed on <c>SqlState</c>, never on message text.</b> Matching substrings like
/// "violates foreign key constraint" breaks the moment the server locale or wording changes, and
/// silently misses everything it was not written for. <c>SqlState</c> codes are part of the
/// PostgreSQL protocol and stable forever.</para>
///
/// <para><b>Status codes reflect whose fault it is.</b> A duplicate key or a still-referenced row
/// is the caller's problem (4xx) — returning 500 tells the user "the system broke" when the
/// correct message is "that code is already in use". Only genuine server faults — a missing
/// column, a datatype mismatch — stay 5xx.</para>
///
/// <para>Npgsql already parses the fields, so the constraint, column and table names come from
/// <see cref="PostgresException"/> properties rather than string surgery on the message.</para>
/// </summary>
public static class PostgresErrorTranslator
{
    /// <summary>
    /// Finds a <see cref="PostgresException"/> anywhere in the chain.
    ///
    /// <para>EF wraps provider failures — a connection-pool exhaustion arrives as
    /// <c>InvalidOperationException("An exception has been raised that is likely due to a transient
    /// failure")</c> with the real <c>PostgresException</c> underneath. Matching only on the
    /// outermost type is why those surfaced as a bare "Internal server error" with the actual
    /// cause discarded.</para>
    /// </summary>
    public static PostgresException? Find(Exception? exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is PostgresException pg) return pg;
        }

        return null;
    }

    /// <summary>Maps one PostgreSQL error to an HTTP status and a user-facing message.</summary>
    public static (int StatusCode, string Message) Translate(PostgresException pg) => pg.SqlState switch
    {
        // ── Caller's data: 4xx ────────────────────────────────────────────────
        PostgresErrorCodes.UniqueViolation =>
            ((int)HttpStatusCode.Conflict,
             $"This {Subject(pg)} already exists. Enter a different value."),

        PostgresErrorCodes.ForeignKeyViolation =>
            ((int)HttpStatusCode.Conflict,
             $"This record is linked to other data{In(pg.TableName)} and cannot be changed or deleted "
             + "while those references exist."),

        PostgresErrorCodes.NotNullViolation =>
            ((int)HttpStatusCode.BadRequest,
             $"{Field(pg.ColumnName)} is required and cannot be left blank."),

        PostgresErrorCodes.CheckViolation =>
            ((int)HttpStatusCode.BadRequest,
             $"The value entered is not allowed{For(pg.ConstraintName)}."),

        PostgresErrorCodes.StringDataRightTruncation =>
            ((int)HttpStatusCode.BadRequest,
             $"{Field(pg.ColumnName)} is too long for the field. Shorten it and try again."),

        PostgresErrorCodes.NumericValueOutOfRange =>
            ((int)HttpStatusCode.BadRequest,
             $"{Field(pg.ColumnName)} is outside the range this field accepts."),

        PostgresErrorCodes.InvalidTextRepresentation or PostgresErrorCodes.InvalidDatetimeFormat =>
            ((int)HttpStatusCode.BadRequest,
             "A value is in the wrong format. Check dates and numbers and try again."),

        PostgresErrorCodes.DivisionByZero =>
            ((int)HttpStatusCode.BadRequest, "A calculation divided by zero. Check the values entered."),

        // ── Contention: retryable, so the UI can offer "try again" ────────────
        PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected =>
            ((int)HttpStatusCode.Conflict,
             "Another user changed this data at the same time. Please try again."),

        PostgresErrorCodes.LockNotAvailable =>
            ((int)HttpStatusCode.Conflict, "This record is being edited by someone else. Please try again shortly."),

        // ── Capacity / availability: 503, explicitly not the user's fault ─────
        PostgresErrorCodes.TooManyConnections or PostgresErrorCodes.CannotConnectNow =>
            ((int)HttpStatusCode.ServiceUnavailable,
             "The database is at its connection limit. Please retry in a moment; if it persists, "
             + "an administrator needs to free database connections."),

        PostgresErrorCodes.AdminShutdown or PostgresErrorCodes.CrashShutdown =>
            ((int)HttpStatusCode.ServiceUnavailable, "The database is restarting. Please try again shortly."),

        PostgresErrorCodes.QueryCanceled =>
            ((int)HttpStatusCode.GatewayTimeout, "The request took too long and was cancelled. Try narrowing the date range or filters."),

        PostgresErrorCodes.InsufficientPrivilege =>
            ((int)HttpStatusCode.Forbidden, "The application does not have permission to perform this database operation."),

        // ── Genuine server faults: schema drift between code and database ─────
        PostgresErrorCodes.UndefinedColumn =>
            ((int)HttpStatusCode.InternalServerError,
             $"Schema mismatch — the database is missing a column this feature needs{In(pg.TableName)}. "
             + $"({pg.MessageText}) A database migration has not been applied."),

        PostgresErrorCodes.UndefinedTable =>
            ((int)HttpStatusCode.InternalServerError,
             $"Schema mismatch — a required table is missing. ({pg.MessageText}) "
             + "A database migration has not been applied."),

        PostgresErrorCodes.DatatypeMismatch or PostgresErrorCodes.CannotCoerce =>
            ((int)HttpStatusCode.InternalServerError,
             $"Schema mismatch — a column's type does not match what the application expects. ({pg.MessageText})"),

        PostgresErrorCodes.UndefinedFunction =>
            ((int)HttpStatusCode.InternalServerError,
             $"The database is missing a function this feature needs. ({pg.MessageText})"),

        PostgresErrorCodes.SyntaxError =>
            ((int)HttpStatusCode.InternalServerError, $"The application generated invalid SQL. ({pg.MessageText})"),

        // Unknown code: still show what the database said rather than hiding it.
        _ => ((int)HttpStatusCode.InternalServerError,
              $"Database error ({pg.SqlState}): {pg.MessageText}"),
    };

    /// <summary>
    /// What the duplicate was, preferring the column, then the constraint, then the table — so the
    /// message reads "This branch id already exists" rather than naming an index nobody recognises.
    /// </summary>
    private static string Subject(PostgresException pg)
    {
        if (!string.IsNullOrWhiteSpace(pg.ColumnName)) return Humanise(pg.ColumnName);

        if (!string.IsNullOrWhiteSpace(pg.ConstraintName))
        {
            // A primary-key clash is a duplicate identifier, not a duplicate "branch pkey" —
            // Postgres names the index sys_branch_pkey, which means nothing to a user.
            if (pg.ConstraintName.EndsWith("_pkey", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(pg.TableName)
                    ? "record"
                    : Humanise(StripPrefix(pg.TableName)) + " record";
            }

            return Humanise(StripAffixes(pg.ConstraintName));
        }

        if (!string.IsNullOrWhiteSpace(pg.TableName)) return Humanise(StripPrefix(pg.TableName)) + " record";
        return "record";
    }

    /// <summary>Strips both the naming prefix and PostgreSQL's generated index suffixes.</summary>
    private static string StripAffixes(string name)
    {
        var trimmed = StripPrefix(name);

        foreach (var suffix in new[] { "_pkey", "_unique", "_key", "_idx" })
        {
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^suffix.Length];
                break;
            }
        }

        return trimmed;
    }

    private static string Field(string? column) =>
        string.IsNullOrWhiteSpace(column) ? "A required field" : Humanise(column);

    private static string In(string? table) =>
        string.IsNullOrWhiteSpace(table) ? string.Empty : $" ({Humanise(StripPrefix(table))})";

    private static string For(string? constraint) =>
        string.IsNullOrWhiteSpace(constraint) ? string.Empty : $" for {Humanise(StripAffixes(constraint))}";

    /// <summary>Drops the module/table prefixes and index noise that mean nothing to a user.</summary>
    private static string StripPrefix(string name)
    {
        var trimmed = name;

        foreach (var prefix in new[] { "uq_", "idx_", "chk_", "fk_", "pk_" })
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[prefix.Length..];
                break;
            }
        }

        foreach (var module in new[] { "sys_", "hrm_", "fin_", "inv_", "pur_", "sal_" })
        {
            if (trimmed.StartsWith(module, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[module.Length..];
                break;
            }
        }

        return trimmed;
    }

    /// <summary><c>branch_id</c> → <c>Branch id</c>. Trailing <c>_no</c> is an internal key suffix.</summary>
    private static string Humanise(string name)
    {
        var cleaned = name.Replace('_', ' ').Trim();
        if (cleaned.EndsWith(" no", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[..^3];

        return cleaned.Length == 0
            ? name
            : char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
    }
}
