using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class Project : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Key { get; private set; } = string.Empty; // e.g. "TF", "PROJ"
    public ProjectStatus Status { get; private set; } = ProjectStatus.Active;
    public Guid OwnerId { get; private set; }
    /// <summary>Incremented atomically in SQL (see IApplicationDbContext.NextTaskSequenceAsync).</summary>
    public int TaskCounter { get; private set; }

    // Navigation
    public User? Owner { get; private set; }
    public ICollection<ProjectMember> Members { get; private set; } = new List<ProjectMember>();
    public ICollection<TaskItem> Tasks { get; private set; } = new List<TaskItem>();

    private Project() { }

    public static Project Create(string name, string? description, string key, Guid ownerId)
    {
        return new Project
        {
            Name = name,
            Description = description,
            Key = key.ToUpperInvariant(),
            OwnerId = ownerId,
            Status = ProjectStatus.Active
        };
    }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = ProjectStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status = ProjectStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        Status = ProjectStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }
}
