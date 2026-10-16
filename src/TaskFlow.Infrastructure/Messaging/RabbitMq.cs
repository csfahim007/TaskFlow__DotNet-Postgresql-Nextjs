using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TaskFlow.Infrastructure.Messaging;

public static class RabbitTopology
{
    public const string EventsExchange = "taskflow.events";
    public const string RetryExchange = "taskflow.retry";
    public const string Queue = "taskflow.notifications";
    public const string RetryQueue = "taskflow.notifications.retry";
    public const string DeadQueue = "taskflow.notifications.dead";
    public const int RetryDelayMs = 15_000;
    public const int MaxDeliveries = 5;
}

/// <summary>RabbitMQ publisher with durable topology, persistent messages and publisher confirms.</summary>
public sealed class RabbitMqBroker : IMessageBroker, IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqBroker> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _publishChannel;

    public string Name => "rabbitmq";

    public RabbitMqBroker(IConfiguration config, ILogger<RabbitMqBroker> logger)
    {
        _logger = logger;
        _factory = new ConnectionFactory
        {
            Uri = new Uri(config["RabbitMq:Uri"]!),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            RequestedHeartbeat = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true }) return _connection;
        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true }) return _connection;
            _connection = await _factory.CreateConnectionAsync("taskflow-api", ct);
            _publishChannel = null; // recreate channel on the new connection
            await using var setup = await _connection.CreateChannelAsync(cancellationToken: ct);
            await DeclareTopologyAsync(setup, ct);
            _logger.LogInformation("Connected to RabbitMQ");
            return _connection;
        }
        finally { _gate.Release(); }
    }

    private static async Task DeclareTopologyAsync(IChannel ch, CancellationToken ct)
    {
        await ch.ExchangeDeclareAsync(RabbitTopology.EventsExchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
        await ch.ExchangeDeclareAsync(RabbitTopology.RetryExchange, ExchangeType.Direct, durable: true, cancellationToken: ct);

        // Main queue: rejected messages are dead-lettered into the delayed retry queue.
        await ch.QueueDeclareAsync(RabbitTopology.Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = RabbitTopology.RetryExchange,
                ["x-dead-letter-routing-key"] = "retry"
            }, cancellationToken: ct);
        await ch.QueueBindAsync(RabbitTopology.Queue, RabbitTopology.EventsExchange, "events", cancellationToken: ct);

        // Retry queue: holds messages for RetryDelayMs, then returns them to the main queue.
        await ch.QueueDeclareAsync(RabbitTopology.RetryQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = RabbitTopology.RetryDelayMs,
                ["x-dead-letter-exchange"] = RabbitTopology.EventsExchange,
                ["x-dead-letter-routing-key"] = "events"
            }, cancellationToken: ct);
        await ch.QueueBindAsync(RabbitTopology.RetryQueue, RabbitTopology.RetryExchange, "retry", cancellationToken: ct);

        // Dead-letter queue for poison messages (inspect / replay manually).
        await ch.QueueDeclareAsync(RabbitTopology.DeadQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await ch.QueueBindAsync(RabbitTopology.DeadQueue, RabbitTopology.EventsExchange, "dead", cancellationToken: ct);
    }

    public async Task PublishAsync(Guid messageId, string type, string payload, CancellationToken ct)
    {
        var conn = await GetConnectionAsync(ct);
        if (_publishChannel is not { IsOpen: true })
        {
            await _gate.WaitAsync(ct);
            try
            {
                if (_publishChannel is not { IsOpen: true })
                    _publishChannel = await conn.CreateChannelAsync(
                        new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                        ct);
            }
            finally { _gate.Release(); }
        }

        var props = new BasicProperties
        {
            Persistent = true,
            MessageId = messageId.ToString(),
            Type = type,
            ContentType = "application/json",
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        // With confirm tracking enabled this awaits the broker's ack (throws on nack / timeout).
        await _publishChannel!.BasicPublishAsync(RabbitTopology.EventsExchange, "events", mandatory: true,
            basicProperties: props, body: Encoding.UTF8.GetBytes(payload), cancellationToken: ct);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        try { return (await GetConnectionAsync(ct)).IsOpen; }
        catch { return false; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_publishChannel is not null) await _publishChannel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _gate.Dispose();
    }
}

/// <summary>Consumes events with manual acks, delayed retries (via DLX + TTL) and a dead-letter queue.</summary>
public class RabbitMqConsumerService : BackgroundService
{
    private readonly RabbitMqBroker _broker;
    private readonly IEventDispatcher _dispatcher;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    public RabbitMqConsumerService(RabbitMqBroker broker, IEventDispatcher dispatcher, ILogger<RabbitMqConsumerService> logger)
    {
        _broker = broker;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IChannel? channel = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var conn = await _broker.GetConnectionAsync(stoppingToken);
                channel = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, 10, false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, ea) => OnMessageAsync(channel, ea, stoppingToken);
                await channel.BasicConsumeAsync(RabbitTopology.Queue, autoAck: false, consumer, stoppingToken);
                _logger.LogInformation("RabbitMQ consumer listening on {Queue}", RabbitTopology.Queue);

                // Park until shutdown or the channel closes (automatic recovery re-attaches consumers).
                while (!stoppingToken.IsCancellationRequested && channel.IsOpen)
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ consumer unavailable, retrying in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        if (channel is not null) await channel.DisposeAsync();
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var type = ea.BasicProperties.Type ?? string.Empty;
        try
        {
            var payload = Encoding.UTF8.GetString(ea.Body.Span);
            await _dispatcher.DispatchAsync(type, payload, ct);
            await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var deliveries = DeliveryCount(ea.BasicProperties.Headers);
            if (deliveries >= RabbitTopology.MaxDeliveries)
            {
                _logger.LogError(ex, "Message {Id} ({Type}) failed {Count} times; moving to dead-letter queue",
                    ea.BasicProperties.MessageId, type, deliveries);
                await channel.BasicPublishAsync(RabbitTopology.EventsExchange, "dead", mandatory: false,
                    basicProperties: new BasicProperties(ea.BasicProperties), body: ea.Body, cancellationToken: ct);
                await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
            }
            else
            {
                _logger.LogWarning(ex, "Message {Id} ({Type}) failed (delivery {Count}); scheduling retry",
                    ea.BasicProperties.MessageId, type, deliveries + 1);
                await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, ct); // -> retry queue via DLX
            }
        }
    }

    /// <summary>Number of times RabbitMQ dead-lettered this message from the main queue (x-death header).</summary>
    private static long DeliveryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-death", out var raw) || raw is not IEnumerable<object> deaths) return 0;
        foreach (var d in deaths)
        {
            if (d is IDictionary<string, object?> entry
                && entry.TryGetValue("queue", out var q) && q is byte[] qb && Encoding.UTF8.GetString(qb) == RabbitTopology.Queue
                && entry.TryGetValue("count", out var c) && c is long count)
                return count;
        }
        return 0;
    }
}
