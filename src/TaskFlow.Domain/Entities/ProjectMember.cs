using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class ProjectMember : BaseEntity
{
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public UserRole Role { get; private set; } = UserRole.Member;

    // Navigation
    public Project? Project { get; private set; }
    public User? User { get; private set; }

    private ProjectMember() { }

    public static ProjectMember Create(Guid projectId, Guid userId, UserRole role = UserRole.Member)
    {
        return new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = role
        };
    }

    public void ChangeRole(UserRole role) => Role = role;
}
