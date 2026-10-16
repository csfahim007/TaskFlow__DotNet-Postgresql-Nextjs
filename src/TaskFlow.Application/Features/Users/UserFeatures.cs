using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Application.Features.Auth.Commands;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Users;

public record UserDto(Guid Id, string FirstName, string LastName, string FullName, string Email,
    string Role, bool IsActive, DateTime CreatedAt, DateTime? LastLoginAt);

// ---------- Me ----------
public record GetCurrentUserQuery : IRequest<Result<UserDto>>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetCurrentUserQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        var u = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == _currentUser.UserId, ct);
        return u is null
            ? Result<UserDto>.NotFound("User not found.")
            : Result<UserDto>.Success(new UserDto(u.Id, u.FirstName, u.LastName, u.FullName, u.Email,
                u.Role.ToString(), u.IsActive, u.CreatedAt, u.LastLoginAt));
    }
}

// ---------- Admin: list ----------
public record GetUsersQuery(int Page = 1, int PageSize = 20, string? Search = null, UserRole? Role = null)
    : IRequest<Result<PagedResult<UserDto>>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<PagedResult<UserDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUsersQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<UserDto>>> Handle(GetUsersQuery r, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Result<PagedResult<UserDto>>.Failure("Admin only.", 403);

        var page = Math.Max(1, r.Page);
        var size = Math.Clamp(r.PageSize, 1, 100);
        var q = _context.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim().ToLower();
            q = q.Where(u => u.Email.Contains(s) || u.FirstName.ToLower().Contains(s) || u.LastName.ToLower().Contains(s));
        }
        if (r.Role.HasValue) q = q.Where(u => u.Role == r.Role.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(u => u.Email).Skip((page - 1) * size).Take(size)
            .Select(u => new UserDto(u.Id, u.FirstName, u.LastName, u.FirstName + " " + u.LastName, u.Email,
                u.Role.ToString(), u.IsActive, u.CreatedAt, u.LastLoginAt))
            .ToListAsync(ct);
        return Result<PagedResult<UserDto>>.Success(PagedResult<UserDto>.Create(items, total, page, size));
    }
}

// ---------- Admin: change role ----------
public record ChangeUserRoleCommand(Guid UserId, UserRole Role) : IRequest<Result>;

public class ChangeUserRoleCommandHandler : IRequestHandler<ChangeUserRoleCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;
    private readonly IAuditService _audit;

    public ChangeUserRoleCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache, IAuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
        _audit = audit;
    }

    public async Task<Result> Handle(ChangeUserRoleCommand r, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Result.Failure("Admin only.", 403);
        if (r.UserId == _currentUser.UserId) return Result.Failure("You cannot change your own role.", 400);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        if (user is null) return Result.Failure("User not found.", 404);

        var old = user.Role.ToString();
        user.ChangeRole(r.Role);
        await _context.SaveChangesAsync(ct);

        // Role is embedded in the JWT => force re-authentication so the new role takes effect.
        await SessionRevocation.RevokeAllAsync(_cache, user.Id, ct);
        await _audit.LogAsync("User", user.Id.ToString(), "RoleChanged", old, r.Role.ToString(), ct);
        return Result.Success();
    }
}

// ---------- Admin: activate / deactivate ----------
public record SetUserActiveCommand(Guid UserId, bool IsActive) : IRequest<Result>;

public class SetUserActiveCommandHandler : IRequestHandler<SetUserActiveCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;
    private readonly IAuditService _audit;

    public SetUserActiveCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache, IAuditService audit)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
        _audit = audit;
    }

    public async Task<Result> Handle(SetUserActiveCommand r, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin) return Result.Failure("Admin only.", 403);
        if (r.UserId == _currentUser.UserId) return Result.Failure("You cannot deactivate yourself.", 400);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        if (user is null) return Result.Failure("User not found.", 404);

        if (r.IsActive) user.Activate();
        else
        {
            user.Deactivate();
            var tokens = await _context.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct);
            foreach (var t in tokens) t.Revoke();
        }
        await _context.SaveChangesAsync(ct);

        if (!r.IsActive) await SessionRevocation.RevokeAllAsync(_cache, user.Id, ct);
        await _audit.LogAsync("User", user.Id.ToString(), r.IsActive ? "Activated" : "Deactivated", cancellationToken: ct);
        return Result.Success();
    }
}
