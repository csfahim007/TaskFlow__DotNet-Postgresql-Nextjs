using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Tasks.Queries;

public record TaskHistoryEntryDto(string Action, string? OldValue, string? NewValue, string? UserEmail, DateTime At);

// ---------- Single task ----------
public record GetTaskQuery(Guid TaskId) : IRequest<Result<TaskDto>>;

public class GetTaskQueryHandler : IRequestHandler<GetTaskQuery, Result<TaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProjectAccessService _access;

    public GetTaskQueryHandler(IApplicationDbContext context, IProjectAccessService access)
    {
        _context = context;
        _access = access;
    }

    public async Task<Result<TaskDto>> Handle(GetTaskQuery r, CancellationToken ct)
    {
        var t = await _context.TaskItems.AsNoTracking().Where(x => x.Id == r.TaskId)
            .Select(x => new TaskDto(x.Id, x.TaskNumber, x.Title, x.Description, x.Status.ToString(), x.Priority.ToString(),
                x.DueDate, x.StoryPoints, x.ProjectId, x.Project != null ? x.Project.Key : "", x.AssigneeId,
                x.Assignee != null ? x.Assignee.FirstName + " " + x.Assignee.LastName : null, x.CreatedAt))
            .FirstOrDefaultAsync(ct);
        if (t is null) return Result<TaskDto>.NotFound("Task not found.");
        if (!await _access.HasPermissionAsync(t.ProjectId, ProjectPermission.View, ct))
            return Result<TaskDto>.NotFound("Task not found."); // hide existence from non-members
        return Result<TaskDto>.Success(t);
    }
}

// ---------- Task history (audit trail) ----------
public record GetTaskHistoryQuery(Guid TaskId) : IRequest<Result<List<TaskHistoryEntryDto>>>;

public class GetTaskHistoryQueryHandler : IRequestHandler<GetTaskHistoryQuery, Result<List<TaskHistoryEntryDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProjectAccessService _access;

    public GetTaskHistoryQueryHandler(IApplicationDbContext context, IProjectAccessService access)
    {
        _context = context;
        _access = access;
    }

    public async Task<Result<List<TaskHistoryEntryDto>>> Handle(GetTaskHistoryQuery r, CancellationToken ct)
    {
        var projectId = await _context.TaskItems.AsNoTracking().Where(t => t.Id == r.TaskId)
            .Select(t => (Guid?)t.ProjectId).FirstOrDefaultAsync(ct);
        if (projectId is null || !await _access.HasPermissionAsync(projectId.Value, ProjectPermission.View, ct))
            return Result<List<TaskHistoryEntryDto>>.NotFound("Task not found.");

        var id = r.TaskId.ToString();
        var entries = await _context.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == "TaskItem" && a.EntityId == id)
            .OrderByDescending(a => a.CreatedAt).Take(200)
            .Select(a => new TaskHistoryEntryDto(a.Action, a.OldValues, a.NewValues, a.UserEmail, a.CreatedAt))
            .ToListAsync(ct);
        return Result<List<TaskHistoryEntryDto>>.Success(entries);
    }
}

// ---------- My tasks (across all projects) ----------
public record GetMyTasksQuery(int Page = 1, int PageSize = 20, TaskItemStatus? Status = null, bool OpenOnly = true)
    : IRequest<Result<PagedResult<TaskDto>>>;

public class GetMyTasksQueryHandler : IRequestHandler<GetMyTasksQuery, Result<PagedResult<TaskDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyTasksQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<TaskDto>>> Handle(GetMyTasksQuery r, CancellationToken ct)
    {
        var page = Math.Max(1, r.Page);
        var size = Math.Clamp(r.PageSize, 1, 100);
        var me = _currentUser.UserId;

        // Only tasks in projects where the user is still a member.
        var q = _context.TaskItems.AsNoTracking()
            .Where(t => t.AssigneeId == me && t.Project!.Members.Any(m => m.UserId == me));
        if (r.Status.HasValue) q = q.Where(t => t.Status == r.Status.Value);
        else if (r.OpenOnly) q = q.Where(t => t.Status != TaskItemStatus.Done && t.Status != TaskItemStatus.Cancelled);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.Priority)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new TaskDto(x.Id, x.TaskNumber, x.Title, x.Description, x.Status.ToString(), x.Priority.ToString(),
                x.DueDate, x.StoryPoints, x.ProjectId, x.Project != null ? x.Project.Key : "", x.AssigneeId,
                x.Assignee != null ? x.Assignee.FirstName + " " + x.Assignee.LastName : null, x.CreatedAt))
            .ToListAsync(ct);
        return Result<PagedResult<TaskDto>>.Success(PagedResult<TaskDto>.Create(items, total, page, size));
    }
}
