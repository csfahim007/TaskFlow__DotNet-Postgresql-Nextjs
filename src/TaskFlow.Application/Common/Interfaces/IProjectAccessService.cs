using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Interfaces;

/// <summary>Project-scoped RBAC. Global Admins are treated as ProjectManager on every project.</summary>
public interface IProjectAccessService
{
    Task<UserRole?> GetRoleAsync(Guid projectId, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(Guid projectId, ProjectPermission permission, CancellationToken ct = default);
    Task InvalidateAsync(Guid projectId, Guid userId, CancellationToken ct = default);
}
