namespace TaskFlow.Application.Features.Notifications;

public record NotificationDto(Guid Id, string Type, string Title, string Message,
    Guid? ProjectId, Guid? TaskId, bool IsRead, DateTime CreatedAt);
