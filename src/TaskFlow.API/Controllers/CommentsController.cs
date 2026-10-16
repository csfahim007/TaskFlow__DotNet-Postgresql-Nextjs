using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.API.Hubs;
using TaskFlow.Application.Features.Comments;
using TaskFlow.Application.Features.Comments.Commands;
using TaskFlow.Application.Features.Tasks.Queries;

namespace TaskFlow.API.Controllers;

[Authorize]
public class CommentsController : BaseController
{
    private readonly IHubContext<TaskFlowHub> _hub;

    public CommentsController(IHubContext<TaskFlowHub> hub) => _hub = hub;

    /// <summary>List comments of a task</summary>
    [HttpGet]
    public async Task<IActionResult> GetComments([FromQuery] Guid taskId, CancellationToken ct)
        => HandleResult(await Mediator.Send(new GetCommentsQuery(taskId), ct));

    /// <summary>Add a comment to a task</summary>
    [HttpPost]
    public async Task<IActionResult> AddComment([FromBody] AddCommentCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        if (result.Succeeded)
        {
            var projectId = (await Mediator.Send(new GetTaskQuery(command.TaskId), ct)).Value?.ProjectId;
            if (projectId is not null)
                await _hub.Clients.Group($"project-{projectId}").SendAsync("CommentAdded", result.Value, ct);
        }
        return HandleResult(result);
    }

    /// <summary>Edit your own comment</summary>
    [HttpPut("{commentId:guid}")]
    public async Task<IActionResult> EditComment(Guid commentId, [FromBody] EditCommentRequest request, CancellationToken ct)
        => HandleResult(await Mediator.Send(new EditCommentCommand(commentId, request.Content), ct));

    /// <summary>Delete a comment (author or project manager)</summary>
    [HttpDelete("{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken ct)
        => HandleResult(await Mediator.Send(new DeleteCommentCommand(commentId), ct));
}

public record EditCommentRequest(string Content);
