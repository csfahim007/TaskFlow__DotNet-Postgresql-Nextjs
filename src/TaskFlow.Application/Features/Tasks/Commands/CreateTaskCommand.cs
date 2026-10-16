using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Events;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Application.Features.Tasks.Queries;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Tasks.Commands;

public record CreateTaskCommand(
    string Title,
    string? Description,
    Guid ProjectId,
    Priority Priority,
    DateTime? DueDate,
    int? StoryPoints,
    Guid? AssigneeId
) : IRequest<Result<TaskDto>>;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(5000);
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.StoryPoints).InclusiveBetween(1, 100).When(x => x.StoryPoints.HasValue);
        RuleFor(x => x.DueDate).GreaterThan(DateTime.UtcNow).When(x => x.DueDate.HasValue);
    }
}

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, Result<TaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IProjectAccessService _access;
    private readonly IEventBus _bus;

    public CreateTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser,
        IAuditService auditService, IProjectAccessService access, IEventBus bus)
    {
        _context = context;
        _currentUser = currentUser;
        _auditService = auditService;
        _access = access;
        _bus = bus;
    }

    public async Task<Result<TaskDto>> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        if (await _access.GetRoleAsync(request.ProjectId, cancellationToken) is null)
            return Result<TaskDto>.NotFound("Project not found.");
        if (!await _access.HasPermissionAsync(request.ProjectId, ProjectPermission.CreateTask, cancellationToken))
            return Result<TaskDto>.Failure("You do not have permission to create tasks in this project.", 403);

        var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (project is null) return Result<TaskDto>.NotFound("Project not found.");
        if (project.Status == ProjectStatus.Archived)
            return Result<TaskDto>.Failure("Project is archived and read-only.", 409);

        User? assignee = null;
        if (request.AssigneeId.HasValue)
        {
            assignee = await TaskGuards.ResolveAssigneeAsync(_context, request.ProjectId, request.AssigneeId.Value, cancellationToken);
            if (assignee is null) return Result<TaskDto>.Failure("Assignee must be a member of the project.", 400);
        }

        var seq = await _context.NextTaskSequenceAsync(request.ProjectId, cancellationToken);
        var taskNumber = $"{project.Key}-{seq:000}";

        var dueDateUtc = TaskGuards.ToUtc(request.DueDate);
        var task = TaskItem.Create(request.Title.Trim(), request.Description, request.ProjectId, request.Priority, taskNumber, dueDateUtc, request.StoryPoints);
        task.SetCreatedBy(_currentUser.Email!);
        if (assignee is not null) task.Assign(assignee.Id);

        _context.TaskItems.Add(task);

        if (assignee is not null && assignee.Id != _currentUser.UserId)
        {
            var actor = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == _currentUser.UserId, cancellationToken);
            _bus.Publish(new TaskAssignedEvent(task.Id, taskNumber, task.Title, project.Id, assignee.Id, actor.Id, actor.FullName));
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("TaskItem", task.Id.ToString(), "Created",
            newValues: $"{{ Title: {task.Title}, Number: {taskNumber} }}", cancellationToken: cancellationToken);

        return Result<TaskDto>.Success(TaskGuards.ToDto(task, project.Key, assignee?.FullName));
    }
}

internal static class TaskGuards
{
    public static DateTime? ToUtc(DateTime? d) => d.HasValue
        ? (d.Value.Kind == DateTimeKind.Utc ? d.Value : d.Value.Kind == DateTimeKind.Local ? d.Value.ToUniversalTime() : DateTime.SpecifyKind(d.Value, DateTimeKind.Utc))
        : null;

    /// <summary>Returns the user only if they are an active member who is allowed to work on the project.</summary>
    public static async Task<User?> ResolveAssigneeAsync(IApplicationDbContext ctx, Guid projectId, Guid userId, CancellationToken ct)
    {
        var isMember = await ctx.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId && m.Role != UserRole.Viewer, ct);
        if (!isMember) return null;
        return await ctx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
    }

    public static TaskDto ToDto(TaskItem t, string projectKey, string? assigneeName) =>
        new(t.Id, t.TaskNumber, t.Title, t.Description, t.Status.ToString(), t.Priority.ToString(), t.DueDate,
            t.StoryPoints, t.ProjectId, projectKey, t.AssigneeId, assigneeName, t.CreatedAt);
}
