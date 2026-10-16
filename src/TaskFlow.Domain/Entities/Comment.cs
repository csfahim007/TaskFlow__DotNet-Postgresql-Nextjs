using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class Comment : BaseEntity
{
    public string Content { get; private set; } = string.Empty;
    public Guid TaskItemId { get; private set; }
    public Guid AuthorId { get; private set; }

    // Navigation
    public TaskItem? TaskItem { get; private set; }
    public User? Author { get; private set; }

    private Comment() { }

    public static Comment Create(string content, Guid taskItemId, Guid authorId)
    {
        return new Comment
        {
            Content = content,
            TaskItemId = taskItemId,
            AuthorId = authorId
        };
    }

    public void Edit(string content)
    {
        Content = content;
        UpdatedAt = DateTime.UtcNow;
    }
}
