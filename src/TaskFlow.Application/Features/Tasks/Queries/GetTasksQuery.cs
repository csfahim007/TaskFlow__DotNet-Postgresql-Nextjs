using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Tasks.Queries;

public record GetTasksQuery(
    Guid ProjectId,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    TaskItemStatus? Status = null,
    Priority? Priority = null,
    Guid? AssigneeId = null,
    bool? Overdue = null
) : IRequest<Result<PagedResult<TaskDto>>>;

public record TaskDto(
    Guid Id,
    string TaskNumber,
    string Title,
    string? Description,
    string Status,
    string Priority,
    DateTime? DueDate,
    int? StoryPoints,
    Guid ProjectId,
    string ProjectKey,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime CreatedAt
);

public class GetTasksQueryHandler : IRequestHandler<GetTasksQuery, Result<PagedResult<TaskDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProjectAccessService _access;

    public GetTasksQueryHandler(IApplicationDbContext context, IProjectAccessService access)
    {
        _context = context;
        _access = access;
    }

    public async Task<Result<PagedResult<TaskDto>>> Handle(GetTasksQuery request, CancellationToken cancellationToken)
    {
        if (!await _access.HasPermissionAsync(request.ProjectId, ProjectPermission.View, cancellationToken))
            return Result<PagedResult<TaskDto>>.Failure("Access denied.", 403);

        request = request with { Page = Math.Max(1, request.Page), PageSize = Math.Clamp(request.PageSize, 1, 200) };

        var query = _context.TaskItems
            .Include(t => t.Assignee)
            .Include(t => t.Project)
            .Where(t => t.ProjectId == request.ProjectId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(t => t.Title.Contains(request.Search) || t.TaskNumber.Contains(request.Search));

        if (request.Status.HasValue) query = query.Where(t => t.Status == request.Status.Value);
        if (request.Priority.HasValue) query = query.Where(t => t.Priority == request.Priority.Value);
        if (request.AssigneeId.HasValue) query = query.Where(t => t.AssigneeId == request.AssigneeId.Value);
        if (request.Overdue == true)
        {
            var now = DateTime.UtcNow;
            query = query.Where(t => t.DueDate < now && t.Status != TaskItemStatus.Done && t.Status != TaskItemStatus.Cancelled);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TaskDto(t.Id, t.TaskNumber, t.Title, t.Description, t.Status.ToString(),
                t.Priority.ToString(), t.DueDate, t.StoryPoints, t.ProjectId, t.Project != null ? t.Project.Key : "",
                t.AssigneeId, t.Assignee != null ? t.Assignee.FullName : null, t.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<TaskDto>>.Success(PagedResult<TaskDto>.Create(items, total, request.Page, request.PageSize));
    }
}
