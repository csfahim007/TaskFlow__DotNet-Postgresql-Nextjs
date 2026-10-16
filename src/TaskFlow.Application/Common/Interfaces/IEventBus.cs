using TaskFlow.Application.Common.Events;

namespace TaskFlow.Application.Common.Interfaces;

/// <summary>
/// Transactional-outbox event bus. Publish() only stages the event in the DbContext;
/// it becomes durable atomically with the next SaveChangesAsync and is relayed to the broker asynchronously.
/// </summary>
public interface IEventBus
{
    void Publish(IntegrationEvent integrationEvent);
}
