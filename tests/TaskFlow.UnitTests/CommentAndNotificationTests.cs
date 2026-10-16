using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using Xunit;

namespace TaskFlow.UnitTests;

public class CommentAndNotificationTests
{
    [Fact]
    public void Comment_CreateAndEdit_ValidatesContentAndTimestamps()
    {
        var taskId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var content = "This task needs unit tests before PR merge.";

        var comment = Comment.Create(content, taskId, authorId);

        Assert.Equal(taskId, comment.TaskItemId);
        Assert.Equal(authorId, comment.AuthorId);
        Assert.Equal(content, comment.Content);
        Assert.Null(comment.UpdatedAt);

        var updatedContent = "Unit tests added with 100% pass rate.";
        comment.Edit(updatedContent);

        Assert.Equal(updatedContent, comment.Content);
        Assert.NotNull(comment.UpdatedAt);
    }

    [Fact]
    public void Notification_CreateAndMarkRead_UpdatesStatusProperly()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var notif = Notification.Create(
            userId,
            eventId,
            NotificationType.TaskAssigned,
            "Task Assigned",
            "You were assigned to TF-101",
            projectId,
            taskId);

        Assert.Equal(userId, notif.UserId);
        Assert.Equal(eventId, notif.EventId);
        Assert.Equal(NotificationType.TaskAssigned, notif.Type);
        Assert.Equal("Task Assigned", notif.Title);
        Assert.Equal("You were assigned to TF-101", notif.Message);
        Assert.Equal(projectId, notif.ProjectId);
        Assert.Equal(taskId, notif.TaskId);
        Assert.False(notif.IsRead);
        Assert.Null(notif.ReadAt);

        notif.MarkRead();

        Assert.True(notif.IsRead);
        Assert.NotNull(notif.ReadAt);
    }

    [Fact]
    public void ProjectMember_CreateAndChangeRole_BehavesCorrectly()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var member = ProjectMember.Create(projectId, userId, UserRole.Member);

        Assert.Equal(projectId, member.ProjectId);
        Assert.Equal(userId, member.UserId);
        Assert.Equal(UserRole.Member, member.Role);

        member.ChangeRole(UserRole.ProjectManager);
        Assert.Equal(UserRole.ProjectManager, member.Role);

        member.ChangeRole(UserRole.Viewer);
        Assert.Equal(UserRole.Viewer, member.Role);
    }

    [Fact]
    public void AuditLog_Create_CapturesEntityActionAndUser()
    {
        var entityName = "TaskItem";
        var entityId = Guid.NewGuid().ToString();
        var action = "StatusUpdated";
        var userId = Guid.NewGuid();
        var userEmail = "dev@taskflow.local";
        var oldValues = "Todo";
        var newValues = "InProgress";

        var log = AuditLog.Create(entityName, entityId, action, userId, userEmail, oldValues, newValues);

        Assert.Equal(entityName, log.EntityName);
        Assert.Equal(entityId, log.EntityId);
        Assert.Equal(action, log.Action);
        Assert.Equal(userId, log.UserId);
        Assert.Equal(userEmail, log.UserEmail);
        Assert.Equal(oldValues, log.OldValues);
        Assert.Equal(newValues, log.NewValues);
        Assert.True(log.CreatedAt <= DateTime.UtcNow);
    }
}
