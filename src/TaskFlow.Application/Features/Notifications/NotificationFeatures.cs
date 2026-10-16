using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;

namespace TaskFlow.Application.Features.Notifications;

// ---------- List ----------
public record GetNotificationsQuery(int Page = 1, int PageSize = 20, bool UnreadOnly = false)
    : IRequest<Result<NotificationsPage>>;

public record NotificationsPage(PagedResult<NotificationDto> Notifications, int UnreadCount);

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, Result<NotificationsPage>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<NotificationsPage>> Handle(GetNotificationsQuery r, CancellationToken ct)
    {
        var me = _currentUser.UserId;
        var page = Math.Max(1, r.Page);
        var size = Math.Clamp(r.PageSize, 1, 100);

        var q = _context.Notifications.AsNoTracking().Where(n => n.UserId == me);
        var unread = await q.CountAsync(n => !n.IsRead, ct);
        if (r.UnreadOnly) q = q.Where(n => !n.IsRead);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(n => n.CreatedAt).Skip((page - 1) * size).Take(size)
            .Select(n => new NotificationDto(n.Id, n.Type.ToString(), n.Title, n.Message, n.ProjectId, n.TaskId, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        return Result<NotificationsPage>.Success(new NotificationsPage(PagedResult<NotificationDto>.Create(items, total, page, size), unread));
    }
}

// ---------- Mark one read ----------
public record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Result>;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(MarkNotificationReadCommand r, CancellationToken ct)
    {
        var n = await _context.Notifications.FirstOrDefaultAsync(x => x.Id == r.NotificationId && x.UserId == _currentUser.UserId, ct);
        if (n is null) return Result.Failure("Notification not found.", 404);
        n.MarkRead();
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------- Mark all read ----------
public record MarkAllNotificationsReadCommand : IRequest<Result>;

public class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public MarkAllNotificationsReadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(MarkAllNotificationsReadCommand r, CancellationToken ct)
    {
        var unread = await _context.Notifications.Where(n => n.UserId == _currentUser.UserId && !n.IsRead).ToListAsync(ct);
        foreach (var n in unread) n.MarkRead();
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
