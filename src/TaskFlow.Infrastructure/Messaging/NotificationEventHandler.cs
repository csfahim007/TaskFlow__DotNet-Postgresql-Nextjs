using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Common.Events;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Notifications;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Messaging;

/// <summary>
/// Consumer: turns integration events into persisted notifications and pushes them in real time.
/// Idempotent — (UserId, EventId) is unique, so redelivered messages never duplicate notifications.
/// </summary>
public class NotificationEventHandler
{
    private readonly ApplicationDbContext _db;
    private readonly IRealtimeNotifier _realtime;
    private readonly ICacheService _cache;
    private readonly ILogger<NotificationEventHandler> _logger;

    public NotificationEventHandler(ApplicationDbContext db, IRealtimeNotifier realtime, ICacheService cache,
        ILogger<NotificationEventHandler> logger)
    {
        _db = db;
        _realtime = realtime;
        _cache = cache;
        _logger = logger;
    }

    public async Task HandleAsync(IntegrationEvent evt, CancellationToken ct)
    {
        var drafts = await BuildAsync(evt, ct);
        if (drafts.Count == 0) return;

        var recipientIds = drafts.Select(d => d.UserId).Distinct().ToList();
        var alreadyDelivered = await _db.Notifications.AsNoTracking()
            .Where(n => n.EventId == evt.EventId && recipientIds.Contains(n.UserId))
            .Select(n => n.UserId).ToListAsync(ct);

        var created = new List<Notification>();
        foreach (var d in drafts.Where(d => !alreadyDelivered.Contains(d.UserId)).DistinctBy(d => d.UserId))
        {
            var n = Notification.Create(d.UserId, evt.EventId, d.Type, d.Title, d.Message, d.ProjectId, d.TaskId);
            _db.Notifications.Add(n);
            created.Add(n);
        }
        if (created.Count == 0) return;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            _logger.LogInformation("Event {EventId} already processed concurrently; skipping", evt.EventId);
            return;
        }

        foreach (var n in created)
        {
            // Invalidate the cached dashboard (unread counter) and push live; neither may fail the message.
            await _cache.RemoveAsync(CacheKeys.Dashboard(n.UserId), ct);
            try
            {
                await _realtime.NotifyUserAsync(n.UserId,
                    new NotificationDto(n.Id, n.Type.ToString(), n.Title, n.Message, n.ProjectId, n.TaskId, n.IsRead, n.CreatedAt), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Realtime push failed for notification {Id}", n.Id);
            }
        }
    }

    private record Draft(Guid UserId, NotificationType Type, string Title, string Message, Guid? ProjectId, Guid? TaskId);

    private async Task<List<Draft>> BuildAsync(IntegrationEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case TaskAssignedEvent e when e.AssigneeId != e.ActorId:
                return [new(e.AssigneeId, NotificationType.TaskAssigned, "New task assigned",
                    $"{e.ActorName} assigned you {e.TaskNumber}: {e.Title}", e.ProjectId, e.TaskId)];

            case TaskStatusChangedEvent e when e.AssigneeId is { } assignee && assignee != e.ActorId:
                return [new(assignee, NotificationType.TaskStatusChanged, "Task status changed",
                    $"{e.ActorName} moved {e.TaskNumber} from {e.OldStatus} to {e.NewStatus}", e.ProjectId, e.TaskId)];

            case CommentAddedEvent e:
            {
                var recipients = await _db.Comments.AsNoTracking().Where(c => c.TaskItemId == e.TaskId)
                    .Select(c => c.AuthorId).Distinct().ToListAsync(ct);
                if (e.AssigneeId is { } a) recipients.Add(a);
                return recipients.Where(r => r != e.ActorId).Distinct()
                    .Select(r => new Draft(r, NotificationType.CommentAdded, $"New comment on {e.TaskNumber}",
                        $"{e.ActorName}: {e.Excerpt}", e.ProjectId, e.TaskId)).ToList();
            }

            case ProjectMemberAddedEvent e when e.UserId != e.ActorId:
                return [new(e.UserId, NotificationType.AddedToProject, "Added to a project",
                    $"{e.ActorName} added you to {e.ProjectName} as {e.Role}", e.ProjectId, null)];

            case TaskDueSoonEvent e:
                return [new(e.AssigneeId, NotificationType.TaskDueSoon, "Task due soon",
                    $"{e.TaskNumber} \"{e.Title}\" is due {e.DueDate:yyyy-MM-dd HH:mm} UTC", e.ProjectId, e.TaskId)];

            default:
                return [];
        }
    }
}
