using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Application.Common.Utils;

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

/// <summary>
/// Mirrors <c>core.shared.util.FileStorageUtil</c>. Registered in DI (the Java original is a
/// Spring <c>@Component</c>) because it logs and owns the configured upload root.
/// </summary>
public class FileStorageUtil
{
    private const string UploadDir = "uploads";

    private static readonly Regex UnsafeChars = new("[^a-zA-Z0-9_\\-]", RegexOptions.Compiled);
    private static readonly Regex RepeatedUnderscore = new("_{2,}", RegexOptions.Compiled);

    private readonly ILogger<FileStorageUtil> _logger;

    public FileStorageUtil(ILogger<FileStorageUtil> logger) => _logger = logger;

    /// <summary>
    /// Decodes a <c>data:image/...;base64,...</c> payload to disk under
    /// <c>uploads/{subDir}</c> and returns the web-relative path.
    ///
    /// <para>Returns the input unchanged when it is not a data-image URI (so callers can pass
    /// through already-stored paths), and <c>null</c> when decoding or the path-traversal guard
    /// fails — matching the Java behaviour exactly.</para>
    /// </summary>
    public string? SaveBase64Image(string? base64Data, string? subDir, string? preferredFileName)
    {
        if (base64Data == null || !base64Data.StartsWith("data:image", StringComparison.Ordinal))
        {
            return base64Data;
        }

        try
        {
            // Split data:image/png;base64,XXXXX
            var parts = base64Data.Split(',');
            var metadata = parts[0];
            var base64Content = parts[1];

            // Determine extension
            var extension = "jpg";
            if (metadata.Contains("png", StringComparison.Ordinal)) extension = "png";
            else if (metadata.Contains("gif", StringComparison.Ordinal)) extension = "gif";
            else if (metadata.Contains("webp", StringComparison.Ordinal)) extension = "webp";

            var imageBytes = Convert.FromBase64String(base64Content);

            // Sanitize subDir to prevent path traversal
            var safeSubDir = SanitizePathSegment(subDir);
            var safeFileName = SanitizePathSegment(preferredFileName);

            // Create directory if not exists
            var uploadRoot = Path.GetFullPath(UploadDir);
            var directory = Path.GetFullPath(Path.Combine(UploadDir, safeSubDir));

            // Verify the resolved path is still under the upload directory
            if (!directory.StartsWith(uploadRoot, StringComparison.Ordinal))
            {
                _logger.LogWarning("Path traversal attempt detected: subDir={SubDir}", subDir);
                return null;
            }

            Directory.CreateDirectory(directory);

            // Generate file name with a GUID fragment to prevent collisions
            var fileName = $"{safeFileName}_{Guid.NewGuid().ToString("N")[..8]}.{extension}";
            var filePath = Path.GetFullPath(Path.Combine(directory, fileName));

            // Double-check the final file path is within the upload directory
            if (!filePath.StartsWith(directory, StringComparison.Ordinal))
            {
                _logger.LogWarning("Path traversal attempt detected in file path: {FilePath}", filePath);
                return null;
            }

            File.WriteAllBytes(filePath, imageBytes);

            _logger.LogInformation("Image saved to: {FilePath}", filePath);

            // Return the relative path for web access
            return $"/{UploadDir}/{safeSubDir}/{fileName}";
        }
        catch (Exception e) when (e is IOException or FormatException or ArgumentException or IndexOutOfRangeException)
        {
            _logger.LogError(e, "Failed to save base64 image");
            return null;
        }
    }

    /// <summary>
    /// Sanitizes a path segment by removing any directory traversal characters and keeping
    /// only alphanumerics, hyphens, and underscores.
    /// </summary>
    private static string SanitizePathSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "default";
        }

        // Remove path separators and traversal sequences
        return RepeatedUnderscore.Replace(UnsafeChars.Replace(segment, "_"), "_").Trim();
    }

    /// <summary>Generic stream save retained from the existing .NET code.</summary>
    public static async Task<string> SaveFileAsync(Stream stream, string destinationDir, string fileName)
    {
        Directory.CreateDirectory(destinationDir);
        var fullPath = Path.Combine(destinationDir, fileName);
        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream);
        return fullPath;
    }
}
