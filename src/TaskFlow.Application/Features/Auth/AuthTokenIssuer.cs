using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Features.Auth;

public record AuthResponse(
    string Token,
    string RefreshToken,
    int ExpiresIn,
    string Email,
    string FullName,
    string Role,
    Guid UserId);

/// <summary>Issues access + rotating refresh tokens. Does NOT save; callers commit the unit of work.</summary>
public class AuthTokenIssuer
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwt;
    private readonly ICurrentUserService _currentUser;

    public AuthTokenIssuer(IApplicationDbContext context, IJwtService jwt, ICurrentUserService currentUser)
    {
        _context = context;
        _jwt = jwt;
        _currentUser = currentUser;
    }

    public (AuthResponse Response, string NewHash) Issue(User user, Guid? familyId = null)
    {
        var access = _jwt.GenerateToken(user.Id, user.Email, user.Role.ToString());
        var raw = _jwt.GenerateRefreshToken();
        var hash = _jwt.HashToken(raw);

        _context.RefreshTokens.Add(RefreshToken.Create(
            user.Id, familyId ?? Guid.NewGuid(), hash,
            DateTime.UtcNow.Add(_jwt.RefreshTokenLifetime), _currentUser.IpAddress));

        return (new AuthResponse(access, raw, _jwt.AccessTokenLifetimeSeconds,
            user.Email, user.FullName, user.Role.ToString(), user.Id), hash);
    }
}
