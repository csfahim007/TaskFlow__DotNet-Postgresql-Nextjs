using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.API.Hubs;
using TaskFlow.Application.Features.Comments;
using TaskFlow.Application.Features.Tasks.Commands;
using TaskFlow.Application.Features.Tasks.Queries;
using TaskFlow.Domain.Enums;

namespace TaskFlow.API.Controllers;

[Authorize]
public class TasksController : BaseController
{
    private readonly IHubContext<TaskFlowHub> _hubContext;

    public TasksController(IHubContext<TaskFlowHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>Get paginated task list for a project with filters</summary>
    [HttpGet]
    public async Task<IActionResult> GetTasks([FromQuery] GetTasksQuery query, CancellationToken ct)
        => HandleResult(await Mediator.Send(query, ct));

    /// <summary>Tasks assigned to me across all my projects</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyTasks([FromQuery] GetMyTasksQuery query, CancellationToken ct)
        => HandleResult(await Mediator.Send(query, ct));

    [HttpGet("{taskId:guid}")]
    public async Task<IActionResult> GetTask(Guid taskId, CancellationToken ct)
        => HandleResult(await Mediator.Send(new GetTaskQuery(taskId), ct));

    /// <summary>Audit trail of a task</summary>
    [HttpGet("{taskId:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid taskId, CancellationToken ct)
        => HandleResult(await Mediator.Send(new GetTaskHistoryQuery(taskId), ct));

    /// <summary>Create a new task in a project</summary>
    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        if (result.Succeeded)
            await _hubContext.Clients.Group($"project-{command.ProjectId}").SendAsync("TaskCreated", result.Value, ct);
        return HandleResult(result);
    }

    /// <summary>Edit task fields</summary>
    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid taskId, [FromBody] UpdateTaskRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new UpdateTaskCommand(taskId, request.Title, request.Description,
            request.Priority, request.DueDate, request.StoryPoints), ct);
        if (result.Succeeded)
            await _hubContext.Clients.Group($"project-{result.Value!.ProjectId}").SendAsync("TaskUpdated", result.Value, ct);
        return HandleResult(result);
    }

    /// <summary>Update a task's status (drag-and-drop board)</summary>
    [HttpPatch("{taskId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid taskId, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new UpdateTaskStatusCommand(taskId, request.Status), ct);
        if (result.Succeeded)
            await _hubContext.Clients.Group($"project-{result.Value!.ProjectId}").SendAsync("TaskStatusUpdated", result.Value, ct);
        return HandleResult(result);
    }

    /// <summary>Assign or unassign (null) a task</summary>
    [HttpPatch("{taskId:guid}/assignee")]
    public async Task<IActionResult> Assign(Guid taskId, [FromBody] AssignRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(new AssignTaskCommand(taskId, request.AssigneeId), ct);
        if (result.Succeeded)
            await _hubContext.Clients.Group($"project-{result.Value!.ProjectId}").SendAsync("TaskUpdated", result.Value, ct);
        return HandleResult(result);
    }

    /// <summary>Delete a task (creator, project manager or admin)</summary>
    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid taskId, CancellationToken ct)
    {
        var task = await Mediator.Send(new GetTaskQuery(taskId), ct);
        var result = await Mediator.Send(new DeleteTaskCommand(taskId), ct);
        if (result.Succeeded && task.Value is { } t)
            await _hubContext.Clients.Group($"project-{t.ProjectId}").SendAsync("TaskDeleted", new { taskId, projectId = t.ProjectId }, ct);
        return HandleResult(result);
    }
}

public record UpdateStatusRequest(TaskItemStatus Status);
public record UpdateTaskRequest(string Title, string? Description, Priority Priority, DateTime? DueDate, int? StoryPoints);
public record AssignRequest(Guid? AssigneeId);
