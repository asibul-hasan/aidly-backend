namespace Aidly.src.Shared.Domain.Wrappers;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }              // ✅ Added
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public object? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> CreateSuccess(
        T? data,
        int statusCode = 200,
        string message = "Success")
    {
        return new ApiResponse<T>
        {
            Success = true,
            StatusCode = statusCode,
            Message = message,
            Data = data,
            Errors = null,
            Timestamp = DateTime.UtcNow
        };
    }

    public static ApiResponse<T> CreateError(
        string message,
        object? errors = null,
        int statusCode = 500)
    {
        return new ApiResponse<T>
        {
            Success = false,
            StatusCode = statusCode,
            Message = message,
            Data = default,
            Errors = errors,
            Timestamp = DateTime.UtcNow
        };
    }
}