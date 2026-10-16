using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.API.Hubs;

[Authorize]
public class TaskFlowHub : Hub
{
    private readonly IApplicationDbContext _db;

    public TaskFlowHub(IApplicationDbContext db) => _db = db;

    private Guid? UserId => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private bool IsAdmin => Context.User?.IsInRole("Admin") ?? false;

    public async Task JoinProject(string projectId)
    {
        // Only members (or admins) may subscribe to a project's live updates.
        if (!Guid.TryParse(projectId, out var pid) || UserId is not { } uid)
            throw new HubException("Invalid project.");

        var allowed = IsAdmin || await _db.ProjectMembers.AsNoTracking().AnyAsync(m => m.ProjectId == pid && m.UserId == uid);
        if (!allowed) throw new HubException("Access denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"project-{pid}");
        await Clients.Caller.SendAsync("JoinedProject", projectId);
    }

    public Task JoinProjectGroup(string projectId) => JoinProject(projectId);

    public async Task LeaveProject(string projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project-{projectId}");
    }

    public Task LeaveProjectGroup(string projectId) => LeaveProject(projectId);

    public override async Task OnConnectedAsync()
    {
        // Personal channel for notifications and cross-tab sync.
        if (UserId is { } uid)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{uid}");

        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
}
