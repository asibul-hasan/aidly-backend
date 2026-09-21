using System.Text.Json.Serialization;

namespace AidlyErp.Api.Controllers;

/// <summary>
/// Body of a cancel/reject call. The Angular forms post <c>{ "reason": "…" }</c>, matching the Java
/// controllers' <c>@RequestBody Map&lt;String,String&gt;</c>; binding the reason from the query
/// string instead drops it silently, leaving cancelled documents with no recorded cause.
/// </summary>
/// <remarks>
/// Bind with <c>[FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]</c> — the body is optional,
/// exactly like the Java <c>required = false</c>.
/// </remarks>
public sealed class ReasonRequest
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
