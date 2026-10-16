namespace TaskFlow.Domain.Enums;

/// <summary>
/// Role used both globally (User.Role) and per project (ProjectMember.Role).
/// Viewer = read-only, Member = contributor, ProjectManager = manages project/members, Admin = system-wide.
/// </summary>
public enum UserRole
{
    Viewer = 0,
    Member = 1,
    ProjectManager = 2,
    Admin = 3
}
