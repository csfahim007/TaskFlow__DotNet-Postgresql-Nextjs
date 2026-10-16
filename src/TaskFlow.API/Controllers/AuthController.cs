using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Application.Features.Auth.Commands;
using TaskFlow.Application.Features.Users;

namespace TaskFlow.API.Controllers;

public class AuthController : BaseController
{
    /// <summary>Register a new user account</summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken ct)
        => HandleResult(await Mediator.Send(command, ct));

    /// <summary>Login with email and password (rate limited, account lockout after repeated failures)</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
        => HandleResult(await Mediator.Send(command, ct));

    /// <summary>Exchange a refresh token for a new access + refresh token pair (rotation)</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command, CancellationToken ct)
        => HandleResult(await Mediator.Send(command, ct));

    /// <summary>Revoke the refresh token and blacklist the current access token</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand command, CancellationToken ct)
        => HandleResult(await Mediator.Send(command, ct));

    /// <summary>Current user profile</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
        => HandleResult(await Mediator.Send(new GetCurrentUserQuery(), ct));

    /// <summary>Change password; signs the user out of all sessions</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command, CancellationToken ct)
        => HandleResult(await Mediator.Send(command, ct));
}
