using Api.Application.Commands;
using Api.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Api.WebApi.Controllers;

/// <summary>
/// Handles user authentication: registration, login, and token refresh.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly RegisterUserHandler _registerHandler;
    private readonly LoginHandler _loginHandler;
    private readonly RefreshTokenHandler _refreshHandler;

    public AuthController(
        RegisterUserHandler registerHandler,
        LoginHandler loginHandler,
        RefreshTokenHandler refreshHandler)
    {
        _registerHandler = registerHandler;
        _loginHandler = loginHandler;
        _refreshHandler = refreshHandler;
    }

    /// <summary>
    /// Registers a new user in the system.
    /// </summary>
    /// <param name="request">Registration details including email, password, role, and tenant.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Access token and refresh token for the newly created user.</returns>
    /// <response code="201">User registered successfully.</response>
    /// <response code="400">Email already in use or registration failed.</response>
    /// <response code="422">Request body failed validation.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken ct)
    {
        var result = await _registerHandler.HandleAsync(request, ct);

        if (result.IsFailure)
            return BadRequest(ApiResponse<AuthResponse>.Fail(result.Error.Code, result.Error.Message));

        return StatusCode(201, ApiResponse<AuthResponse>.Ok(result.Value));
    }

    /// <summary>
    /// Authenticates a user and issues access and refresh tokens.
    /// </summary>
    /// <param name="request">Login credentials (email and password).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Access token and refresh token.</returns>
    /// <response code="200">Login successful.</response>
    /// <response code="401">Invalid credentials.</response>
    /// <response code="422">Request body failed validation.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _loginHandler.HandleAsync(request, ct);

        if (result.IsFailure)
            return Unauthorized(ApiResponse<AuthResponse>.Fail(result.Error.Code, result.Error.Message));

        return Ok(ApiResponse<AuthResponse>.Ok(result.Value));
    }

    /// <summary>
    /// Issues a new access token using a valid refresh token.
    /// </summary>
    /// <param name="request">The refresh token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A new access token and rotated refresh token.</returns>
    /// <response code="200">Token refreshed successfully.</response>
    /// <response code="401">Refresh token is invalid or expired.</response>
    /// <response code="422">Request body failed validation.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken ct)
    {
        var result = await _refreshHandler.HandleAsync(request, ct);

        if (result.IsFailure)
            return Unauthorized(ApiResponse<AuthResponse>.Fail(result.Error.Code, result.Error.Message));

        return Ok(ApiResponse<AuthResponse>.Ok(result.Value));
    }
}
