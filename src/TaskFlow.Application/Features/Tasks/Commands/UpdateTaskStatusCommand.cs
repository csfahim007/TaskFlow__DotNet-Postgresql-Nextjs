using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Events;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Application.Features.Tasks.Queries;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Tasks.Commands;

public record UpdateTaskStatusCommand(Guid TaskId, TaskItemStatus Status) : IRequest<Result<TaskDto>>;

public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, Result<TaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IProjectAccessService _access;
    private readonly IEventBus _bus;

    public UpdateTaskStatusCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser,
        IAuditService auditService, IProjectAccessService access, IEventBus bus)
    {
        _context = context;
        _currentUser = currentUser;
        _auditService = auditService;
        _access = access;
        _bus = bus;
    }

    public async Task<Result<TaskDto>> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.TaskItems
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

        if (task is null) return Result<TaskDto>.NotFound("Task not found.");
        if (!await _access.HasPermissionAsync(task.ProjectId, ProjectPermission.EditTask, cancellationToken))
            return Result<TaskDto>.Failure("Access denied.", 403);
        if (task.Project?.Status == ProjectStatus.Archived)
            return Result<TaskDto>.Failure("Project is archived and read-only.", 409);

        var oldStatus = task.Status;
        if (oldStatus == request.Status) return Result<TaskDto>.Success(TaskGuards.ToDto(task, task.Project?.Key ?? "", task.Assignee?.FullName));

        task.ChangeStatus(request.Status);
        task.SetUpdatedBy(_currentUser.Email!);

        var actor = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == _currentUser.UserId, cancellationToken);
        _bus.Publish(new TaskStatusChangedEvent(task.Id, task.TaskNumber, task.Title, task.ProjectId,
            oldStatus.ToString(), request.Status.ToString(), task.AssigneeId, actor.Id, actor.FullName));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<TaskDto>.Failure("The task was modified by someone else. Refresh and try again.", 409);
        }

        await _auditService.LogAsync("TaskItem", task.Id.ToString(), "StatusChanged",
            oldValues: oldStatus.ToString(), newValues: request.Status.ToString(), cancellationToken: cancellationToken);

        return Result<TaskDto>.Success(TaskGuards.ToDto(task, task.Project?.Key ?? "", task.Assignee?.FullName));
    }
}
