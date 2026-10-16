using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Infrastructure.Services;

public class ProjectAccessService : IProjectAccessService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);
    private const string None = "None";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public ProjectAccessService(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<UserRole?> GetRoleAsync(Guid projectId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return null;

        // Global admins act as managers everywhere (the project must still exist).
        if (_currentUser.IsAdmin)
            return await _context.Projects.AnyAsync(p => p.Id == projectId, ct) ? UserRole.ProjectManager : null;

        var value = await _cache.GetOrCreateAsync(CacheKeys.ProjectRole(projectId, userId), async token =>
        {
            var role = await _context.ProjectMembers.AsNoTracking()
                .Where(m => m.ProjectId == projectId && m.UserId == userId)
                .Select(m => (UserRole?)m.Role).FirstOrDefaultAsync(token);
            return role?.ToString() ?? None;
        }, Ttl, ct);

        return Enum.TryParse<UserRole>(value, out var parsed) ? parsed : null;
    }

    public async Task<bool> HasPermissionAsync(Guid projectId, ProjectPermission permission, CancellationToken ct = default)
    {
        var role = await GetRoleAsync(projectId, ct);
        return role is not null && RolePermissions.Has(role.Value, permission);
    }

    public Task InvalidateAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => _cache.RemoveAsync(CacheKeys.ProjectRole(projectId, userId), ct);
}
