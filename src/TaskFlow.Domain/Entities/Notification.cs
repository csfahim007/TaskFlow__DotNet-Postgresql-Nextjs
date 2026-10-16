using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; private set; }
    /// <summary>Id of the integration event that produced it; (UserId, EventId) is unique => idempotent consumers.</summary>
    public Guid EventId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public Guid? ProjectId { get; private set; }
    public Guid? TaskId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public User? User { get; private set; }

    private Notification() { }

    public static Notification Create(Guid userId, Guid eventId, NotificationType type, string title,
        string message, Guid? projectId, Guid? taskId)
        => new()
        {
            UserId = userId, EventId = eventId, Type = type, Title = title,
            Message = message, ProjectId = projectId, TaskId = taskId
        };

    public void MarkRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
