using Microsoft.AspNetCore.SignalR;
using TaskFlow.API.Hubs;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Notifications;

namespace TaskFlow.API.Realtime;

public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<TaskFlowHub> _hub;

    public SignalRRealtimeNotifier(IHubContext<TaskFlowHub> hub) => _hub = hub;

    public Task NotifyUserAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
        => _hub.Clients.Group($"user-{userId}").SendAsync("NotificationReceived", notification, ct);
}
