using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

/// <summary>Rotating refresh token. Only the SHA-256 hash is persisted.</summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByHash { get; private set; }
    public string? CreatedByIp { get; private set; }

    public User? User { get; private set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, Guid familyId, string tokenHash, DateTime expiresAt, string? ip)
        => new() { UserId = userId, FamilyId = familyId, TokenHash = tokenHash, ExpiresAt = expiresAt, CreatedByIp = ip };

    public void Revoke(string? replacedByHash = null)
    {
        RevokedAt ??= DateTime.UtcNow;
        ReplacedByHash = replacedByHash ?? ReplacedByHash;
    }
}
