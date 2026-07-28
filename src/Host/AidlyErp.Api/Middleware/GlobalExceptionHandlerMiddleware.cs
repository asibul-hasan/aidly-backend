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

            // --- Anything else -> 500, exposing only the exception type name ---
            default:
                _logger.LogError(exception, "Unexpected [{Type}]: {Message}",
                    exception.GetType().Name, exception.Message);
                return ((int)HttpStatusCode.InternalServerError,
                    ApiResponse<object>.Error((int)HttpStatusCode.InternalServerError,
                        $"Internal server error [{exception.GetType().Name}]. Check server logs for details."));
        }
    }

    /// <summary>
    /// Mirrors the Java <c>handleDatabaseException</c>: unwraps to the most specific cause and
    /// rewrites well-known PostgreSQL failures into actionable messages.
    /// </summary>
    private (int StatusCode, object Payload) TranslateDataAccess(Exception ex)
    {
        var cause = MostSpecificCause(ex);
        var detailMessage = cause.Message ?? "(no details)";
        var causeType = cause.GetType().Name;
        var userFriendlyMessage = "Database Error: " + detailMessage;

        if (detailMessage.Contains("column", StringComparison.OrdinalIgnoreCase)
            && detailMessage.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            userFriendlyMessage = "Schema Mismatch: A required column is missing in the database table. Details: " + detailMessage;
        }
        else if (detailMessage.Contains("relation", StringComparison.OrdinalIgnoreCase)
                 && detailMessage.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            userFriendlyMessage = "Schema Mismatch: A required table is missing. Details: " + detailMessage;
        }
        else if (detailMessage.Contains("violates foreign key constraint", StringComparison.OrdinalIgnoreCase))
        {
            userFriendlyMessage = "Data Integrity Error: This record is linked to other data and cannot be modified/deleted. Details: " + detailMessage;
        }
        else if (detailMessage.Contains("syntax error", StringComparison.OrdinalIgnoreCase))
        {
            userFriendlyMessage = "Query Syntax Error: " + detailMessage;
        }

        _logger.LogError(ex, "Database error [{CauseType}]: {Detail}", causeType, detailMessage);
        return ((int)HttpStatusCode.InternalServerError,
            ApiResponse<object>.Error((int)HttpStatusCode.InternalServerError, userFriendlyMessage));
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
