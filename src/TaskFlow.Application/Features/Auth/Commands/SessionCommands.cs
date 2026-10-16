using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using BC = BCrypt.Net.BCrypt;

namespace TaskFlow.Application.Features.Auth.Commands;

// ---------- Refresh (rotation + reuse detection) ----------
public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwt;
    private readonly AuthTokenIssuer _issuer;

    public RefreshTokenCommandHandler(IApplicationDbContext context, IJwtService jwt, AuthTokenIssuer issuer)
    {
        _context = context;
        _jwt = jwt;
        _issuer = issuer;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var hash = _jwt.HashToken(request.RefreshToken);
        var stored = await _context.RefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored?.User is null) return Result<AuthResponse>.Failure("Invalid refresh token.", 401);

        if (stored.RevokedAt is not null)
        {
            // A rotated token was presented again => likely stolen. Kill the whole family.
            var family = await _context.RefreshTokens
                .Where(t => t.FamilyId == stored.FamilyId && t.RevokedAt == null).ToListAsync(ct);
            foreach (var t in family) t.Revoke();
            await _context.SaveChangesAsync(ct);
            return Result<AuthResponse>.Failure("Refresh token reuse detected. Please sign in again.", 401);
        }

        if (stored.ExpiresAt <= DateTime.UtcNow || !stored.User.IsActive)
            return Result<AuthResponse>.Failure("Refresh token expired.", 401);

        var (response, newHash) = _issuer.Issue(stored.User, stored.FamilyId);
        stored.Revoke(newHash);
        await _context.SaveChangesAsync(ct);
        return Result<AuthResponse>.Success(response);
    }
}

// ---------- Logout ----------
public record LogoutCommand(string? RefreshToken) : IRequest<Result>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwt;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public LogoutCommandHandler(IApplicationDbContext context, IJwtService jwt, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _jwt = jwt;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = _jwt.HashToken(request.RefreshToken);
            var userId = _currentUser.UserId;
            var token = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);
            token?.Revoke();
            await _context.SaveChangesAsync(ct);
        }

        // Blacklist the access token until it would have expired anyway.
        if (_currentUser.TokenId is { } jti && _currentUser.TokenExpiresAt is { } exp && exp > DateTime.UtcNow)
            await _cache.SetAsync(CacheKeys.RevokedToken(jti), true, exp - DateTime.UtcNow, ct);

        return Result.Success();
    }
}

// ---------- Change password ----------
public record ChangePasswordCommand(string CurrentPassword, string NewPassword, string ConfirmPassword) : IRequest<Result>;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from the current password.");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;
    private readonly IAuditService _audit;

    public ChangePasswordCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache, IAuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
        _audit = audit;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null) return Result.Failure("User not found.", 404);
        if (!BC.Verify(request.CurrentPassword, user.PasswordHash))
            return Result.Failure("Current password is incorrect.", 400);

        user.ChangePassword(BC.HashPassword(request.NewPassword));

        // Sign out everywhere: revoke refresh tokens + invalidate all access tokens issued before now.
        var tokens = await _context.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in tokens) t.Revoke();
        await _context.SaveChangesAsync(ct);

        await SessionRevocation.RevokeAllAsync(_cache, user.Id, ct);
        await _audit.LogAsync("User", user.Id.ToString(), "PasswordChanged", cancellationToken: ct);
        return Result.Success();
    }
}

internal static class SessionRevocation
{
    // Must outlive the longest access token lifetime.
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(1);

    public static Task RevokeAllAsync(ICacheService cache, Guid userId, CancellationToken ct)
        => cache.SetAsync(CacheKeys.UserRevokedAfter(userId), DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Ttl, ct);
}
