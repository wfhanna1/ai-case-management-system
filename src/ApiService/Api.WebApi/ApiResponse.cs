namespace Api.WebApi;

/// <summary>
/// Uniform response envelope for all API actions.
/// Successful responses populate <see cref="Data"/>; failures populate <see cref="Error"/>.
/// </summary>
public sealed class ApiResponse<T>
{
    public T? Data { get; init; }
    public ApiError? Error { get; init; }

    public static ApiResponse<T> Ok(T data) =>
        new() { Data = data };

    public static ApiResponse<T> Fail(string code, string message) =>
        new() { Error = new ApiError(code, message) };

    public static ApiResponse<T> Fail(string code, string message, Dictionary<string, string[]> details) =>
        new() { Error = new ApiError(code, message) { Details = details } };
}

public sealed record ApiError(string Code, string Message)
{
    public Dictionary<string, string[]>? Details { get; init; }
}

/// <summary>
/// Empty data payload used as the generic type argument for action-only endpoints
/// that return no meaningful data on success.
/// </summary>
public sealed record EmptyResponse;
