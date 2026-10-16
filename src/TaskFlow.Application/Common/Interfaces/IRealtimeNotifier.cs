using TaskFlow.Application.Features.Notifications;

namespace TaskFlow.Application.Common.Interfaces;

public interface IRealtimeNotifier
{
    Task NotifyUserAsync(Guid userId, NotificationDto notification, CancellationToken ct = default);
}
