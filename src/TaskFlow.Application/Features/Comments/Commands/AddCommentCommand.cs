using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Events;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Comments.Commands;

public record AddCommentCommand(Guid TaskId, string Content) : IRequest<Result<CommentDto>>;

public record CommentDto(Guid Id, string Content, Guid TaskId, Guid AuthorId, string AuthorName, DateTime CreatedAt, DateTime? UpdatedAt = null);

public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}

public class AddCommentCommandHandler : IRequestHandler<AddCommentCommand, Result<CommentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectAccessService _access;
    private readonly IEventBus _bus;

    public AddCommentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IProjectAccessService access, IEventBus bus)
    {
        _context = context;
        _currentUser = currentUser;
        _access = access;
        _bus = bus;
    }

    public async Task<Result<CommentDto>> Handle(AddCommentCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);
        if (task is null) return Result<CommentDto>.NotFound("Task not found.");

        if (!await _access.HasPermissionAsync(task.ProjectId, ProjectPermission.Comment, cancellationToken))
            return Result<CommentDto>.Failure("Access denied.", 403);

        var content = request.Content.Trim();
        var comment = Comment.Create(content, request.TaskId, _currentUser.UserId!.Value);
        comment.SetCreatedBy(_currentUser.Email!);
        _context.Comments.Add(comment);

        var author = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == _currentUser.UserId, cancellationToken);
        _bus.Publish(new CommentAddedEvent(task.Id, task.TaskNumber, task.ProjectId, comment.Id, task.AssigneeId,
            author.Id, author.FullName, content.Length > 140 ? content[..140] + "…" : content));

        await _context.SaveChangesAsync(cancellationToken);

        return Result<CommentDto>.Success(new CommentDto(comment.Id, comment.Content, comment.TaskItemId,
            comment.AuthorId, author.FullName, comment.CreatedAt));
    }
}
