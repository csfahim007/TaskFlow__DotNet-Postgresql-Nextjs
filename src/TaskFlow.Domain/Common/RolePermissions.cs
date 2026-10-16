using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Common;

/// <summary>Single source of truth for the RBAC permission matrix.</summary>
public static class RolePermissions
{
    private static readonly Dictionary<UserRole, HashSet<ProjectPermission>> Matrix = new()
    {
        [UserRole.Viewer] = [ProjectPermission.View],
        [UserRole.Member] =
        [
            ProjectPermission.View, ProjectPermission.CreateTask,
            ProjectPermission.EditTask, ProjectPermission.Comment
        ],
        [UserRole.ProjectManager] = [.. Enum.GetValues<ProjectPermission>()],
        [UserRole.Admin] = [.. Enum.GetValues<ProjectPermission>()]
    };

    public static bool Has(UserRole role, ProjectPermission permission)
        => Matrix.TryGetValue(role, out var set) && set.Contains(permission);
}
