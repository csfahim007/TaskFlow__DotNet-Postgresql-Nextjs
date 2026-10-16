using System.Reflection;
using System.Text.Json;

namespace TaskFlow.Application.Common.Events;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

public record TaskAssignedEvent(Guid TaskId, string TaskNumber, string Title, Guid ProjectId,
    Guid AssigneeId, Guid? ActorId, string ActorName) : IntegrationEvent;

public record TaskStatusChangedEvent(Guid TaskId, string TaskNumber, string Title, Guid ProjectId,
    string OldStatus, string NewStatus, Guid? AssigneeId, Guid? ActorId, string ActorName) : IntegrationEvent;

public record CommentAddedEvent(Guid TaskId, string TaskNumber, Guid ProjectId, Guid CommentId,
    Guid? AssigneeId, Guid ActorId, string ActorName, string Excerpt) : IntegrationEvent;

public record ProjectMemberAddedEvent(Guid ProjectId, string ProjectName, Guid UserId,
    string Role, Guid ActorId, string ActorName) : IntegrationEvent;

public record TaskDueSoonEvent(Guid TaskId, string TaskNumber, string Title, Guid ProjectId,
    Guid AssigneeId, DateTime DueDate) : IntegrationEvent;

public static class IntegrationEventSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, Type> Registry = typeof(IntegrationEvent).Assembly
        .GetTypes()
        .Where(t => !t.IsAbstract && typeof(IntegrationEvent).IsAssignableFrom(t))
        .ToDictionary(t => t.Name);

    public static string TypeName(IntegrationEvent e) => e.GetType().Name;

    public static string Serialize(IntegrationEvent e) => JsonSerializer.Serialize(e, e.GetType(), Options);

    public static IntegrationEvent Deserialize(string type, string payload)
    {
        if (!Registry.TryGetValue(type, out var clr))
            throw new InvalidOperationException($"Unknown integration event type '{type}'.");
        return (IntegrationEvent)JsonSerializer.Deserialize(payload, clr, Options)!;
    }
}
