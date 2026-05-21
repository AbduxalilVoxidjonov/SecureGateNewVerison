namespace SecureGate.Api.Models
{
    /// <summary>
    /// API javoblari uchun yagona o'rom (envelope).
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public IReadOnlyDictionary<string, string[]>? Errors { get; set; }

        public static ApiResponse<T> Ok(T? data, string? message = null) => new()
        {
            Success = true,
            Data = data,
            Message = message
        };

        public static ApiResponse<T> Fail(string message, IReadOnlyDictionary<string, string[]>? errors = null) => new()
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }

    public class ApiResponse : ApiResponse<object>
    {
        public static ApiResponse Ok(string? message = null) => new()
        {
            Success = true,
            Message = message
        };

        public static new ApiResponse Fail(string message, IReadOnlyDictionary<string, string[]>? errors = null) => new()
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}
