using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Common.Events;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Messaging;

/// <summary>Transport to the message broker. Implementations: RabbitMQ (prod) and in-memory (dev/test).</summary>
public interface IMessageBroker
{
    string Name { get; }
    Task PublishAsync(Guid messageId, string type, string payload, CancellationToken ct);
    Task<bool> IsHealthyAsync(CancellationToken ct);
}

/// <summary>Executes the consumers for a message that was delivered by the broker.</summary>
public interface IEventDispatcher
{
    Task DispatchAsync(string type, string payload, CancellationToken ct);
}

/// <summary>IEventBus implementation: stages the event as an outbox row inside the current unit of work.</summary>
public class OutboxEventBus : IEventBus
{
    private readonly IApplicationDbContext _context;

    public OutboxEventBus(IApplicationDbContext context) => _context = context;

    public void Publish(IntegrationEvent integrationEvent)
        => _context.OutboxMessages.Add(OutboxMessage.Create(
            integrationEvent.EventId,
            IntegrationEventSerializer.TypeName(integrationEvent),
            IntegrationEventSerializer.Serialize(integrationEvent)));
}

public class EventDispatcher : IEventDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EventDispatcher(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task DispatchAsync(string type, string payload, CancellationToken ct)
    {
        var evt = IntegrationEventSerializer.Deserialize(type, payload);
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<NotificationEventHandler>();
        await handler.HandleAsync(evt, ct);
    }
}

// ----------------------------------------------------------------------------------------------
// In-memory broker (single-process fallback when RabbitMQ is not configured)
// ----------------------------------------------------------------------------------------------
public class InMemoryBroker : IMessageBroker
{
    private readonly Channel<(string Type, string Payload)> _channel = Channel.CreateUnbounded<(string, string)>();

    public string Name => "in-memory";
    public ChannelReader<(string Type, string Payload)> Reader => _channel.Reader;

    public async Task PublishAsync(Guid messageId, string type, string payload, CancellationToken ct)
        => await _channel.Writer.WriteAsync((type, payload), ct);

    public Task<bool> IsHealthyAsync(CancellationToken ct) => Task.FromResult(true);
}

public class InMemoryConsumerService : BackgroundService
{
    private readonly InMemoryBroker _broker;
    private readonly IEventDispatcher _dispatcher;
    private readonly ILogger<InMemoryConsumerService> _logger;

    public InMemoryConsumerService(InMemoryBroker broker, IEventDispatcher dispatcher, ILogger<InMemoryConsumerService> logger)
    {
        _broker = broker;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message broker: in-memory (set RabbitMq:Uri for a durable, multi-instance broker)");
        await foreach (var (type, payload) in _broker.Reader.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await _dispatcher.DispatchAsync(type, payload, stoppingToken);
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to handle {Type} (attempt {Attempt}/3)", type, attempt);
                    await Task.Delay(TimeSpan.FromSeconds(attempt), stoppingToken);
                }
            }
        }
    }
}

// ----------------------------------------------------------------------------------------------
// Outbox relay: reliably moves committed outbox rows to the broker (at-least-once).
// FOR UPDATE SKIP LOCKED lets any number of API instances run this safely in parallel.
// ----------------------------------------------------------------------------------------------
public class OutboxPublisherService : BackgroundService
{
    private const int BatchSize = 50;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessageBroker _broker;
    private readonly ILogger<OutboxPublisherService> _logger;
    private DateTime _lastCleanup = DateTime.MinValue;

    public OutboxPublisherService(IServiceScopeFactory scopeFactory, IMessageBroker broker, ILogger<OutboxPublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _broker = broker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the host (and migrations) come up first.
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                processed = await ProcessBatchAsync(stoppingToken);
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox relay iteration failed");
            }

            if (processed < BatchSize)
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var now = DateTime.UtcNow;
            var batch = await db.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT * FROM "OutboxMessages"
                    WHERE "ProcessedAt" IS NULL AND "NextAttemptAt" <= {now}
                    ORDER BY "CreatedAt" LIMIT {BatchSize} FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

            foreach (var msg in batch)
            {
                try
                {
                    await _broker.PublishAsync(msg.Id, msg.Type, msg.Payload, ct);
                    msg.MarkProcessed();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Publishing outbox message {Id} ({Type}) failed (attempt {Attempt})", msg.Id, msg.Type, msg.Attempts + 1);
                    msg.MarkFailed(ex.Message);
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return batch.Count;
        });
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromHours(1)) return;
        _lastCleanup = DateTime.UtcNow;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var removed = await db.OutboxMessages.Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff).ExecuteDeleteAsync(ct);
        if (removed > 0) _logger.LogInformation("Outbox cleanup removed {Count} processed messages", removed);
    }
}
