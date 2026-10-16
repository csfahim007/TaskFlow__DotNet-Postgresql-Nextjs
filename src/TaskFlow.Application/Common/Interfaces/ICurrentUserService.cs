namespace TaskFlow.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    string? IpAddress { get; }
    /// <summary>JWT id (jti) of the current access token.</summary>
    string? TokenId { get; }
    DateTime? TokenExpiresAt { get; }
}
