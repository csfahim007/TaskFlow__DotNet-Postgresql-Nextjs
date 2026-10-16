using TaskFlow.Application.Common.Events;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Messaging;
using Xunit;

namespace TaskFlow.UnitTests;

public class MessagingAndOutboxTests
{
    [Fact]
    public void IntegrationEventSerializer_SerializesAndDeserializes_TaskAssignedEvent()
    {
        var evt = new TaskAssignedEvent(
            Guid.NewGuid(),
            "TF-102",
            "Configure Redis AOF",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Alice Admin");

        var typeName = IntegrationEventSerializer.TypeName(evt);
        Assert.Equal("TaskAssignedEvent", typeName);

        var payload = IntegrationEventSerializer.Serialize(evt);
        var deserialized = IntegrationEventSerializer.Deserialize(typeName, payload);
        var typed = Assert.IsType<TaskAssignedEvent>(deserialized);

        Assert.Equal(evt.TaskId, typed.TaskId);
        Assert.Equal(evt.AssigneeId, typed.AssigneeId);
        Assert.Equal("Alice Admin", typed.ActorName);
    }

    [Fact]
    public void IntegrationEventSerializer_SerializesAndDeserializes_TaskStatusChangedEvent()
    {
        var evt = new TaskStatusChangedEvent(
            Guid.NewGuid(),
            "TF-101",
            "Setup Postgres replica",
            Guid.NewGuid(),
            "Todo",
            "InProgress",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Bob Dev");

        var typeName = IntegrationEventSerializer.TypeName(evt);
        Assert.Equal("TaskStatusChangedEvent", typeName);

        var payload = IntegrationEventSerializer.Serialize(evt);
        Assert.Contains("TF-101", payload);

        var deserialized = IntegrationEventSerializer.Deserialize(typeName, payload);
        var typed = Assert.IsType<TaskStatusChangedEvent>(deserialized);

        Assert.Equal(evt.TaskId, typed.TaskId);
        Assert.Equal(evt.TaskNumber, typed.TaskNumber);
        Assert.Equal("Todo", typed.OldStatus);
        Assert.Equal("InProgress", typed.NewStatus);
    }

    [Fact]
    public void IntegrationEventSerializer_SerializesAndDeserializes_CommentAddedEvent()
    {
        var evt = new CommentAddedEvent(
            Guid.NewGuid(),
            "TF-103",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Bob Dev",
            "Looks great, deployed to staging!");

        var typeName = IntegrationEventSerializer.TypeName(evt);
        Assert.Equal("CommentAddedEvent", typeName);

        var payload = IntegrationEventSerializer.Serialize(evt);
        var deserialized = IntegrationEventSerializer.Deserialize(typeName, payload);
        var typed = Assert.IsType<CommentAddedEvent>(deserialized);

        Assert.Equal(evt.CommentId, typed.CommentId);
        Assert.Equal("Looks great, deployed to staging!", typed.Excerpt);
    }

    [Fact]
    public void IntegrationEventSerializer_SerializesAndDeserializes_TaskDueSoonEvent()
    {
        var evt = new TaskDueSoonEvent(
            Guid.NewGuid(),
            "TF-104",
            "Security Audit",
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(24));

        var typeName = IntegrationEventSerializer.TypeName(evt);
        var payload = IntegrationEventSerializer.Serialize(evt);
        var deserialized = IntegrationEventSerializer.Deserialize(typeName, payload);
        var typed = Assert.IsType<TaskDueSoonEvent>(deserialized);

        Assert.Equal(evt.TaskId, typed.TaskId);
        Assert.Equal(evt.TaskNumber, typed.TaskNumber);
    }

    [Fact]
    public async Task InMemoryBroker_PublishAndReader_TransfersMessagesSuccessfully()
    {
        var broker = new InMemoryBroker();
        var messageId = Guid.NewGuid();
        var type = "SampleEvent";
        var payload = "{\"test\":true}";

        await broker.PublishAsync(messageId, type, payload, CancellationToken.None);

        var isHealthy = await broker.IsHealthyAsync(CancellationToken.None);
        Assert.True(isHealthy);
        Assert.Equal("in-memory", broker.Name);

        Assert.True(broker.Reader.TryRead(out var item));
        Assert.Equal(type, item.Type);
        Assert.Equal(payload, item.Payload);
    }

    [Fact]
    public void OutboxMessage_ExponentialBackoff_CalculatesIncreasingDelays()
    {
        var msg = OutboxMessage.Create(Guid.NewGuid(), "TaskCreated", "{}");

        msg.MarkFailed("Attempt 1 failure");
        Assert.Equal(1, msg.Attempts);
        var delay1 = msg.NextAttemptAt - DateTime.UtcNow;

        msg.MarkFailed("Attempt 2 failure");
        Assert.Equal(2, msg.Attempts);
        var delay2 = msg.NextAttemptAt - DateTime.UtcNow;

        msg.MarkFailed("Attempt 3 failure");
        Assert.Equal(3, msg.Attempts);
        var delay3 = msg.NextAttemptAt - DateTime.UtcNow;

        // Exponential backoff should increase: 2s, 4s, 8s...
        Assert.True(delay3 > delay2);
        Assert.True(delay2 > delay1);
    }
}
