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
}

public sealed record ApiError(string Code, string Message);
