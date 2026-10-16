using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Infrastructure.Messaging;

namespace TaskFlow.API;

public class CacheHealthCheck : IHealthCheck
{
    private readonly ICacheService _cache;
    public CacheHealthCheck(ICacheService cache) => _cache = cache;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await _cache.SetAsync("health:ping", true, TimeSpan.FromSeconds(10), ct);
            return await _cache.ExistsAsync("health:ping", ct)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("Cache write was not readable");
        }
        catch (Exception ex) { return HealthCheckResult.Degraded("Cache unavailable", ex); }
    }
}

public class BrokerHealthCheck : IHealthCheck
{
    private readonly IMessageBroker _broker;
    public BrokerHealthCheck(IMessageBroker broker) => _broker = broker;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        => await _broker.IsHealthyAsync(ct)
            ? HealthCheckResult.Healthy($"broker: {_broker.Name}")
            : HealthCheckResult.Degraded($"broker unreachable: {_broker.Name} (events are safely queued in the outbox)");
}
