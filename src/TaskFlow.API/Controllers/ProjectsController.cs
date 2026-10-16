using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.API.Hubs;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Projects;
using TaskFlow.Application.Features.Projects.Commands;
using TaskFlow.Application.Features.Projects.Queries;
using TaskFlow.Domain.Enums;

namespace TaskFlow.API.Controllers;

[Authorize]
public class ProjectsController : BaseController
{
    private readonly IHubContext<TaskFlowHub> _hubContext;
    private readonly ICurrentUserService _currentUser;

    public ProjectsController(IHubContext<TaskFlowHub> hubContext, ICurrentUserService currentUser)
    {
        _hubContext = hubContext;
        _currentUser = currentUser;
    }

    /// <summary>Get paginated list of projects visible to the current user</summary>
    [HttpGet]
    public async Task<IActionResult> GetProjects([FromQuery] GetProjectsQuery query, CancellationToken ct)
        => HandleResult(await Mediator.Send(query, ct));

    /// <summary>Project details including caller's role and task counters</summary>
    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetProject(Guid projectId, CancellationToken ct)
        => HandleResult(await Mediator.Send(new GetProjectQuery(projectId), ct));

    /// <summary>Create a new project (creator becomes ProjectManager)</summary>
    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        if (result.Succeeded && _currentUser.UserId is { } uid)
            await _hubContext.Clients.Group($"user-{uid}").SendAsync("ProjectCreated", result.Value, ct);
        return HandleResult(result);
    }

    /// <summary>Edit name/description (ProjectManager)</summary>
    [HttpPut("{projectId:guid}")]
    public async Task<IActionResult> UpdateProject(Guid projectId, [FromBody] UpdateProjectRequest request, CancellationToken ct)
        => HandleResult(await Mediator.Send(new UpdateProjectCommand(projectId, request.Name, request.Description), ct));

    /// <summary>Archive / complete / reactivate a project (ProjectManager)</summary>
    [HttpPut("{projectId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid projectId, [FromBody] SetProjectStatusRequest request, CancellationToken ct)
        => HandleResult(await Mediator.Send(new SetProjectStatusCommand(projectId, request.Status), ct));

    // ----- Members (RBAC) -----

    [HttpGet("{projectId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid projectId, CancellationToken ct)
        => HandleResult(await Mediator.Send(new GetProjectMembersQuery(projectId), ct));

    [HttpPost("{projectId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid projectId, [FromBody] AddMemberRequest request, CancellationToken ct)
        => HandleResult(await Mediator.Send(new AddProjectMemberCommand(projectId, request.Email, request.Role), ct));

    [HttpPut("{projectId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> ChangeMemberRole(Guid projectId, Guid userId, [FromBody] ChangeMemberRoleRequest request, CancellationToken ct)
        => HandleResult(await Mediator.Send(new ChangeProjectMemberRoleCommand(projectId, userId, request.Role), ct));

    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid projectId, Guid userId, CancellationToken ct)
        => HandleResult(await Mediator.Send(new RemoveProjectMemberCommand(projectId, userId), ct));
}

public record UpdateProjectRequest(string Name, string? Description);
public record SetProjectStatusRequest(ProjectStatus Status);
public record AddMemberRequest(string Email, UserRole Role = UserRole.Member);
public record ChangeMemberRoleRequest(UserRole Role);
