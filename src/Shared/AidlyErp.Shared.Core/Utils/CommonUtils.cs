using System.Globalization;
using System.Text.RegularExpressions;

namespace AidlyErp.Shared.Core.Utils;

/// <summary>Mirrors <c>core.shared.util.DateUtils</c> — ISO-8601 offset formatting.</summary>
public static class DateUtils
{
    /// <summary>Round-trip ISO-8601 with offset (Java <c>DateTimeFormatter.ISO_OFFSET_DATE_TIME</c>).</summary>
    private const string DefaultFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz";

    public static string? Format(DateTimeOffset? dateTime) =>
        dateTime?.ToString(DefaultFormat, CultureInfo.InvariantCulture);

    public static DateTimeOffset? Parse(string? dateStr) =>
        string.IsNullOrWhiteSpace(dateStr)
            ? null
            : DateTimeOffset.Parse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    // --- Convenience helpers retained from the existing .NET code ---

    public static string FormatIso(DateTime dateTime) => dateTime.ToString("o", CultureInfo.InvariantCulture);

    public static string FormatDateOnly(DateTime dateTime) => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateTime? ParseIso(string? dateStr) =>
        DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
}

/// <summary>Mirrors <c>core.shared.util.StringUtils</c>.</summary>
public static class StringUtils
{
    /// <summary>Null, empty, or whitespace-only (Java <c>isEmpty</c> trims before testing).</summary>
    public static bool IsEmpty(string? str) => string.IsNullOrWhiteSpace(str);

    public static bool IsNotEmpty(string? str) => !IsEmpty(str);

    // --- Aliases retained from the existing .NET code ---

    public static bool IsBlank(string? str) => IsEmpty(str);

    public static string DefaultIfEmpty(string? str, string defaultVal) =>
        IsEmpty(str) ? defaultVal : str!;
}

