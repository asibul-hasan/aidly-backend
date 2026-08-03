using System.Net;
using System.Text.Json;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Response;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AidlyErp.Api.Middleware;

/// <summary>
/// Central error translator — the .NET counterpart of the Java
/// <c>@RestControllerAdvice GlobalExceptionHandler</c>. Every branch below maps 1:1 to an
/// <c>@ExceptionHandler</c> method there, preserving the HTTP status, the log level, and the
/// exact user-facing message text.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Exception thrown after the response had already started; cannot rewrite it");
                throw;
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, payload) = Translate(exception);

        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions));
    }

    private (int StatusCode, object Payload) Translate(Exception exception)
    {
        // EF wraps anything thrown while evaluating a LINQ query parameter in an
        // InvalidOperationException. A guard clause inside a query — "No active branch in
        // context" — therefore reached the client as a 500 instead of the 400 it is. Unwrap so the
        // status reflects the original fault rather than where it happened to surface.
        if (exception is InvalidOperationException
            && exception.InnerException is DomainException inner)
        {
            exception = inner;
        }

        switch (exception)
        {
            // --- NotFoundException -> 404 ---
            case NotFoundException ex:
                _logger.LogWarning("Resource not found: {Message}", ex.Message);
                return ((int)HttpStatusCode.NotFound,
                    ApiResponse<object>.Error((int)HttpStatusCode.NotFound, ex.Message));

            // --- ValidationException -> 400 ---
            case ValidationException ex:
                _logger.LogWarning("Validation error: {Message}", ex.Message);
                return ((int)HttpStatusCode.BadRequest,
                    ApiResponse<object>.Error((int)HttpStatusCode.BadRequest, ex.Message));

            // --- DomainException -> 500 (must come after its subclasses) ---
            case DomainException ex:
                _logger.LogError(ex, "Domain error: {Message}", ex.Message);
                return ((int)HttpStatusCode.InternalServerError,
                    ApiResponse<object>.Error((int)HttpStatusCode.InternalServerError, ex.Message));

            // --- Malformed / unreadable JSON body -> 400 with a field-error map ---
            case JsonException ex:
            {
                var errors = new Dictionary<string, string>();
                var fieldPath = ex.Path?.TrimStart('$', '.');
                errors[string.IsNullOrWhiteSpace(fieldPath) ? "body" : fieldPath] =
                    ex.Message.Length > 0 ? ex.Message : "Malformed or unreadable JSON request body";

                _logger.LogWarning("Unreadable request body: {@Errors}", errors);
                return ((int)HttpStatusCode.BadRequest,
                    ApiResponse<Dictionary<string, string>>.Error(
                        (int)HttpStatusCode.BadRequest, "Invalid request body", errors));
            }

            // --- Route/query value could not be bound to the parameter type -> 400 ---
            case FormatException or OverflowException:
                _logger.LogWarning("Type mismatch: {Message}", exception.Message);
                return ((int)HttpStatusCode.BadRequest,
                    ApiResponse<object>.Error((int)HttpStatusCode.BadRequest,
                        $"Invalid parameter value — {exception.Message}"));

            // --- Optimistic-lock conflict -> 409 so the UI can prompt a reload ---
            case DbUpdateConcurrencyException ex:
                _logger.LogWarning("Optimistic lock conflict: {Message}", ex.Message);
                return ((int)HttpStatusCode.Conflict,
                    ApiResponse<object>.Error((int)HttpStatusCode.Conflict,
                        "This record was modified by another user. Please reload and try again."));

            // --- Database / persistence failures -> 500 with a diagnosable message ---
            case DbUpdateException ex:
                return TranslateDataAccess(ex);

            case NpgsqlException ex:
                return TranslateDataAccess(ex);

            // --- Unauthenticated -> 401 ---
            case UnauthorizedAccessException ex:
                _logger.LogWarning("Unauthorized: {Message}", ex.Message);
                return ((int)HttpStatusCode.Unauthorized,
                    ApiResponse<object>.Error((int)HttpStatusCode.Unauthorized, ex.Message));

            // --- Anything else ---
            default:
            {
                // A PostgreSQL failure is very often NOT the outermost exception: EF wraps provider
                // errors (a pool exhaustion arrives as InvalidOperationException "…likely due to a
                // transient failure"). Checking the whole chain here is what stops a real, specific
                // database error being reported as a bare "Internal server error".
                var pg = PostgresErrorTranslator.Find(exception);
                if (pg != null) return TranslatePostgres(pg, exception);

                _logger.LogError(exception, "Unexpected [{Type}]: {Message}",
                    exception.GetType().Name, exception.Message);

                // The message is included rather than withheld. "Check server logs" is useless to
                // whoever is looking at the screen, and this is an internal business application —
                // the operator seeing the real reason is worth more than the marginal disclosure.
                return ((int)HttpStatusCode.InternalServerError,
                    ApiResponse<object>.Error((int)HttpStatusCode.InternalServerError,
                        Readable(exception)));
            }
        }
    }

    /// <summary>
    /// Renders an unexpected exception as one readable line, without leaking a stack trace or
    /// anything multi-line into the response body.
    /// </summary>
    private static string Readable(Exception exception)
    {
        var message = exception.Message?.Trim();

        if (string.IsNullOrWhiteSpace(message))
            return $"Unexpected error ({exception.GetType().Name}). Please contact support.";

        // Collapse to the first line so a multi-line framework message cannot spill into the UI.
        var firstLine = message.Split('\n', '\r')[0].Trim();

        return firstLine.Length > 300 ? firstLine[..300] + "…" : firstLine;
    }

    /// <summary>
    /// Translates a database failure.
    ///
    /// <para>Previously this matched on English substrings of the message ("violates foreign key
    /// constraint", "does not exist"), which covered four cases, broke under a different server
    /// locale, and returned 500 for everything — including duplicate keys, which are the caller's
    /// mistake, not a server fault. <see cref="PostgresErrorTranslator"/> keys on
    /// <c>SqlState</c> instead and picks the status to match.</para>
    /// </summary>
    private (int StatusCode, object Payload) TranslateDataAccess(Exception ex)
    {
        var pg = PostgresErrorTranslator.Find(ex);

        if (pg != null) return TranslatePostgres(pg, ex);

        // Not a PostgreSQL error (a provider/transport fault, say) — report what it said.
        var cause = MostSpecificCause(ex);
        _logger.LogError(ex, "Database error [{CauseType}]: {Detail}", cause.GetType().Name, cause.Message);

        return ((int)HttpStatusCode.InternalServerError,
            ApiResponse<object>.Error((int)HttpStatusCode.InternalServerError,
                $"Database error: {Readable(cause)}"));
    }

    private (int StatusCode, object Payload) TranslatePostgres(PostgresException pg, Exception original)
    {
        var (status, message) = PostgresErrorTranslator.Translate(pg);

        // 4xx is the caller's data, not a fault — log it as a warning so real faults stay findable.
        if (status < 500)
        {
            _logger.LogWarning(
                "Database rejected the request [{SqlState}] on {Table}.{Column} ({Constraint}): {Detail}",
                pg.SqlState, pg.TableName, pg.ColumnName, pg.ConstraintName, pg.MessageText);
        }
        else
        {
            _logger.LogError(original,
                "Database error [{SqlState}] on {Table}.{Column} ({Constraint}): {Detail}",
                pg.SqlState, pg.TableName, pg.ColumnName, pg.ConstraintName, pg.MessageText);
        }

        return (status, ApiResponse<object>.Error(status, message));
    }

    /// <summary>Equivalent of Spring's <c>DataAccessException.getMostSpecificCause()</c>.</summary>
    private static Exception MostSpecificCause(Exception ex)
    {
        var current = ex;
        while (current.InnerException != null)
        {
            current = current.InnerException;
        }
        return current;
    }
}
