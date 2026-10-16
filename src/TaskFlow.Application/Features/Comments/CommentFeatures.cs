using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Application.Features.Comments.Commands;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Comments;

// ---------- List ----------
public record GetCommentsQuery(Guid TaskId) : IRequest<Result<List<CommentDto>>>;

public class GetCommentsQueryHandler : IRequestHandler<GetCommentsQuery, Result<List<CommentDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProjectAccessService _access;

    public GetCommentsQueryHandler(IApplicationDbContext context, IProjectAccessService access)
    {
        _context = context;
        _access = access;
    }

    public async Task<Result<List<CommentDto>>> Handle(GetCommentsQuery r, CancellationToken ct)
    {
        var projectId = await _context.TaskItems.AsNoTracking().Where(t => t.Id == r.TaskId)
            .Select(t => (Guid?)t.ProjectId).FirstOrDefaultAsync(ct);
        if (projectId is null || !await _access.HasPermissionAsync(projectId.Value, ProjectPermission.View, ct))
            return Result<List<CommentDto>>.NotFound("Task not found.");

        var list = await _context.Comments.AsNoTracking().Where(c => c.TaskItemId == r.TaskId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(c.Id, c.Content, c.TaskItemId, c.AuthorId,
                c.Author!.FirstName + " " + c.Author.LastName, c.CreatedAt, c.UpdatedAt))
            .ToListAsync(ct);
        return Result<List<CommentDto>>.Success(list);
    }
}

// ---------- Edit (author only) ----------
public record EditCommentCommand(Guid CommentId, string Content) : IRequest<Result<CommentDto>>;

public class EditCommentCommandValidator : AbstractValidator<EditCommentCommand>
{
    public EditCommentCommandValidator() => RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
}

public class EditCommentCommandHandler : IRequestHandler<EditCommentCommand, Result<CommentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EditCommentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<CommentDto>> Handle(EditCommentCommand r, CancellationToken ct)
    {
        var c = await _context.Comments.Include(x => x.Author).FirstOrDefaultAsync(x => x.Id == r.CommentId, ct);
        if (c is null) return Result<CommentDto>.NotFound("Comment not found.");
        if (c.AuthorId != _currentUser.UserId) return Result<CommentDto>.Failure("You can only edit your own comments.", 403);

        c.Edit(r.Content.Trim());
        c.SetUpdatedBy(_currentUser.Email!);
        await _context.SaveChangesAsync(ct);
        return Result<CommentDto>.Success(new CommentDto(c.Id, c.Content, c.TaskItemId, c.AuthorId, c.Author?.FullName ?? "", c.CreatedAt, c.UpdatedAt));
    }
}

// ---------- Delete (author or project manager) ----------
public record DeleteCommentCommand(Guid CommentId) : IRequest<Result>;

public class DeleteCommentCommandHandler : IRequestHandler<DeleteCommentCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectAccessService _access;

    public DeleteCommentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IProjectAccessService access)
    {
        _context = context;
        _currentUser = currentUser;
        _access = access;
    }

    public async Task<Result> Handle(DeleteCommentCommand r, CancellationToken ct)
    {
        var c = await _context.Comments.Include(x => x.TaskItem).FirstOrDefaultAsync(x => x.Id == r.CommentId, ct);
        if (c is null) return Result.Failure("Comment not found.", 404);

        var isAuthor = c.AuthorId == _currentUser.UserId;
        if (!isAuthor && !await _access.HasPermissionAsync(c.TaskItem!.ProjectId, ProjectPermission.ManageProject, ct))
            return Result.Failure("Access denied.", 403);

        _context.Comments.Remove(c);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
