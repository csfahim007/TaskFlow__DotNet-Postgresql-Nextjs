using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class TaskItem : BaseEntity
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskItemStatus Status { get; private set; } = TaskItemStatus.Todo;
    public Priority Priority { get; private set; } = Priority.Medium;
    public DateTime? DueDate { get; private set; }
    public int? StoryPoints { get; private set; }
    public string TaskNumber { get; private set; } = string.Empty; // e.g. TF-001
    public Guid ProjectId { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? DueReminderSentAt { get; private set; }

    // Navigation
    public Project? Project { get; private set; }
    public User? Assignee { get; private set; }
    public ICollection<Comment> Comments { get; private set; } = new List<Comment>();

    private TaskItem() { }

    public static TaskItem Create(
        string title,
        string? description,
        Guid projectId,
        Priority priority,
        string taskNumber,
        DateTime? dueDate = null,
        int? storyPoints = null)
    {
        return new TaskItem
        {
            Title = title,
            Description = description,
            ProjectId = projectId,
            Priority = priority,
            TaskNumber = taskNumber,
            DueDate = dueDate,
            StoryPoints = storyPoints,
            Status = TaskItemStatus.Todo
        };
    }

    public void Update(string title, string? description, Priority priority, DateTime? dueDate, int? storyPoints)
    {
        Title = title;
        Description = description;
        Priority = priority;
        DueDate = dueDate;
        StoryPoints = storyPoints;
        DueReminderSentAt = null; // due date may have changed => allow a new reminder
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(TaskItemStatus status)
    {
        Status = status;
        CompletedAt = status == TaskItemStatus.Done ? DateTime.UtcNow : null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Assign(Guid? assigneeId)
    {
        AssigneeId = assigneeId;
        DueReminderSentAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDueReminderSent() => DueReminderSentAt = DateTime.UtcNow;

    public bool IsOpen => Status is not (TaskItemStatus.Done or TaskItemStatus.Cancelled);
}
