namespace AidlyErp.Shared.Core.Abstractions;

/// <summary>
/// File-storage port. The Application layer depends on this abstraction; the adapter that
/// actually touches the filesystem lives in <c>Shared.Infrastructure</c>.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Decodes a <c>data:image/...;base64,...</c> payload to storage and returns the
    /// web-relative path. Returns the input unchanged when it is not a data-image URI, and
    /// <c>null</c> when decoding or the path-traversal guard fails.
    /// </summary>
    string? SaveBase64Image(string? base64Data, string? subDir, string? preferredFileName);
}
