namespace TaskFlow.Application.Common.Interfaces;

/// <summary>Distributed cache abstraction (Redis in production, in-memory fallback in dev).</summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

public static class CacheKeys
{
    public static string ProjectRole(Guid projectId, Guid userId) => $"proj-role:{projectId}:{userId}";
    public static string RevokedToken(string jti) => $"revoked-jti:{jti}";
    public static string UserRevokedAfter(Guid userId) => $"user-revoked-after:{userId}";
    public static string Dashboard(Guid userId) => $"dashboard:{userId}";
}
