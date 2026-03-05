using Api.Application.Commands;
using Api.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Api.WebApi.Controllers;

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

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken ct)
    {
        var result = await _registerHandler.HandleAsync(request, ct);

        if (result.IsFailure)
            return BadRequest(ApiResponse<AuthResponse>.Fail(result.Error.Code, result.Error.Message));

        return StatusCode(201, ApiResponse<AuthResponse>.Ok(result.Value));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _loginHandler.HandleAsync(request, ct);

        if (result.IsFailure)
            return Unauthorized(ApiResponse<AuthResponse>.Fail(result.Error.Code, result.Error.Message));

        return Ok(ApiResponse<AuthResponse>.Ok(result.Value));
    }

    [HttpPost("refresh")]
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
