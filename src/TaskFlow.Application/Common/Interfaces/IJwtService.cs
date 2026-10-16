namespace TaskFlow.Application.Common.Interfaces;

public interface IJwtService
{
    string GenerateToken(Guid userId, string email, string role);
    int AccessTokenLifetimeSeconds { get; }
    TimeSpan RefreshTokenLifetime { get; }
    /// <summary>Cryptographically random opaque refresh token (raw value, never stored).</summary>
    string GenerateRefreshToken();
    string HashToken(string token);
    (Guid UserId, string Email, string Role)? ValidateToken(string token);
}
