using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Users;
using TaskFlow.Domain.Enums;

namespace TaskFlow.API.Controllers;

/// <summary>System-wide user administration (Admin role only)</summary>
[Authorize(Policy = "AdminOnly")]
public class UsersController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] GetUsersQuery query, CancellationToken ct)
        => HandleResult(await Mediator.Send(query, ct));

    [HttpPut("{userId:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid userId, [FromBody] ChangeRoleRequest request, CancellationToken ct)
        => HandleResult(await Mediator.Send(new ChangeUserRoleCommand(userId, request.Role), ct));

    [HttpPut("{userId:guid}/active")]
    public async Task<IActionResult> SetActive(Guid userId, [FromBody] SetActiveRequest request, CancellationToken ct)
        => HandleResult(await Mediator.Send(new SetUserActiveCommand(userId, request.IsActive), ct));
}

public record ChangeRoleRequest(UserRole Role);
public record SetActiveRequest(bool IsActive);
