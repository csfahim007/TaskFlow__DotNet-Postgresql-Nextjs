using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Events;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Application.Features.Tasks.Queries;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Tasks.Commands;

// ---------- Update ----------
public record UpdateTaskCommand(Guid TaskId, string Title, string? Description, Priority Priority,
    DateTime? DueDate, int? StoryPoints) : IRequest<Result<TaskDto>>;

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(5000);
        RuleFor(x => x.StoryPoints).InclusiveBetween(1, 100).When(x => x.StoryPoints.HasValue);
    }
}

public class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand, Result<TaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectAccessService _access;
    private readonly IAuditService _audit;

    public UpdateTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IProjectAccessService access, IAuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
    }

    public async Task<Result<TaskDto>> Handle(UpdateTaskCommand r, CancellationToken ct)
    {
        var task = await _context.TaskItems.Include(t => t.Project).Include(t => t.Assignee).FirstOrDefaultAsync(t => t.Id == r.TaskId, ct);
        if (task is null) return Result<TaskDto>.NotFound("Task not found.");
        if (!await _access.HasPermissionAsync(task.ProjectId, ProjectPermission.EditTask, ct))
            return Result<TaskDto>.Failure("Access denied.", 403);
        if (task.Project?.Status == ProjectStatus.Archived)
            return Result<TaskDto>.Failure("Project is archived and read-only.", 409);

        var before = $"{task.Title}|{task.Priority}|{task.DueDate:O}|{task.StoryPoints}";
        task.Update(r.Title.Trim(), r.Description, r.Priority, TaskGuards.ToUtc(r.DueDate), r.StoryPoints);
        task.SetUpdatedBy(_currentUser.Email!);

        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Result<TaskDto>.Failure("The task was modified by someone else. Refresh and try again.", 409);
        }

        await _audit.LogAsync("TaskItem", task.Id.ToString(), "Updated", before,
            $"{task.Title}|{task.Priority}|{task.DueDate:O}|{task.StoryPoints}", ct);
        return Result<TaskDto>.Success(TaskGuards.ToDto(task, task.Project?.Key ?? "", task.Assignee?.FullName));
    }
}

// ---------- Assign ----------
public record AssignTaskCommand(Guid TaskId, Guid? AssigneeId) : IRequest<Result<TaskDto>>;

public class AssignTaskCommandHandler : IRequestHandler<AssignTaskCommand, Result<TaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectAccessService _access;
    private readonly IAuditService _audit;
    private readonly IEventBus _bus;

    public AssignTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser,
        IProjectAccessService access, IAuditService audit, IEventBus bus)
    {
        _context = context;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
        _bus = bus;
    }

    public async Task<Result<TaskDto>> Handle(AssignTaskCommand r, CancellationToken ct)
    {
        var task = await _context.TaskItems.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == r.TaskId, ct);
        if (task is null) return Result<TaskDto>.NotFound("Task not found.");
        if (!await _access.HasPermissionAsync(task.ProjectId, ProjectPermission.EditTask, ct))
            return Result<TaskDto>.Failure("Access denied.", 403);
        if (task.Project?.Status == ProjectStatus.Archived)
            return Result<TaskDto>.Failure("Project is archived and read-only.", 409);

        Domain.Entities.User? assignee = null;
        if (r.AssigneeId.HasValue)
        {
            assignee = await TaskGuards.ResolveAssigneeAsync(_context, task.ProjectId, r.AssigneeId.Value, ct);
            if (assignee is null) return Result<TaskDto>.Failure("Assignee must be a member of the project.", 400);
        }

        var old = task.AssigneeId?.ToString();
        task.Assign(assignee?.Id);
        task.SetUpdatedBy(_currentUser.Email!);

        if (assignee is not null && assignee.Id != _currentUser.UserId && assignee.Id.ToString() != old)
        {
            var actor = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == _currentUser.UserId, ct);
            _bus.Publish(new TaskAssignedEvent(task.Id, task.TaskNumber, task.Title, task.ProjectId, assignee.Id, actor.Id, actor.FullName));
        }

        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Result<TaskDto>.Failure("The task was modified by someone else. Refresh and try again.", 409);
        }

        await _audit.LogAsync("TaskItem", task.Id.ToString(), "Assigned", old, assignee?.Id.ToString(), ct);
        return Result<TaskDto>.Success(TaskGuards.ToDto(task, task.Project?.Key ?? "", assignee?.FullName));
    }
}

// ---------- Delete ----------
public record DeleteTaskCommand(Guid TaskId) : IRequest<Result>;

public class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectAccessService _access;
    private readonly IAuditService _audit;

    public DeleteTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IProjectAccessService access, IAuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _access = access;
        _audit = audit;
    }

    public async Task<Result> Handle(DeleteTaskCommand r, CancellationToken ct)
    {
        var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == r.TaskId, ct);
        if (task is null) return Result.Failure("Task not found.", 404);

        // Managers/Admins may delete any task; contributors only the tasks they created.
        var canDeleteAny = await _access.HasPermissionAsync(task.ProjectId, ProjectPermission.DeleteAnyTask, ct);
        var isCreator = string.Equals(task.CreatedBy, _currentUser.Email, StringComparison.OrdinalIgnoreCase);
        var canEdit = await _access.HasPermissionAsync(task.ProjectId, ProjectPermission.EditTask, ct);
        if (!(canDeleteAny || (canEdit && isCreator))) return Result.Failure("Access denied.", 403);

        _context.TaskItems.Remove(task);
        await _context.SaveChangesAsync(ct);
        await _audit.LogAsync("TaskItem", task.Id.ToString(), "Deleted", $"{task.TaskNumber}: {task.Title}", null, ct);
        return Result.Success();
    }
}
