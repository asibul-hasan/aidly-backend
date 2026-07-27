using System.Text.Json.Serialization;

namespace AidlyErp.Application.Common.Response;

/// <summary>
/// Standard response envelope. Field names match the Java <c>ApiResponse</c> exactly
/// (<c>status_code</c>, <c>data</c>, <c>timestamp</c>, <c>message</c>) so existing API
/// consumers are unaffected by the migration.
/// </summary>
public class ApiResponse<T>
{
    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>ISO-8601 with offset — the serialised form of Java's <c>OffsetDateTime.now()</c>.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    public static ApiResponse<T> Success(T data) => Success(data, "Operation completed successfully");

    public static ApiResponse<T> Success(T data, string? message) => new()
    {
        StatusCode = 200,
        Data = data,
        Message = message,
        Timestamp = DateTimeOffset.Now
    };

    public static ApiResponse<T> Error(int statusCode, string message) => new()
    {
        StatusCode = statusCode,
        Data = default,
        Message = message,
        Timestamp = DateTimeOffset.Now
    };

    /// <summary>Error envelope defaulting to HTTP 500, matching Java's <c>error(String)</c>.</summary>
    public static ApiResponse<T> Error(string message) => Error(500, message);

    /// <summary>
    /// Error envelope with a payload (typically a field-error map).
    /// Use this when the validation result itself needs to ride in <c>data</c>.
    /// </summary>
    public static ApiResponse<T> Error(int statusCode, string message, T data) => new()
    {
        StatusCode = statusCode,
        Data = data,
        Message = message,
        Timestamp = DateTimeOffset.Now
    };
}
