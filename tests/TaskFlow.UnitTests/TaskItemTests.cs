using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using Xunit;

namespace TaskFlow.UnitTests;

public class TaskItemTests
{
    [Fact]
    public void Create_WithValidParameters_SetsInitialStateCorrectly()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var title = "Implement OAuth";
        var description = "Add Google and GitHub OAuth 2.0";
        var priority = Priority.High;
        var taskNumber = "WEB-001";
        var dueDate = DateTime.UtcNow.AddDays(7);
        var storyPoints = 5;

        // Act
        var task = TaskItem.Create(title, description, projectId, priority, taskNumber, dueDate, storyPoints);

        // Assert
        Assert.NotNull(task);
        Assert.Equal(title, task.Title);
        Assert.Equal(description, task.Description);
        Assert.Equal(projectId, task.ProjectId);
        Assert.Equal(priority, task.Priority);
        Assert.Equal(taskNumber, task.TaskNumber);
        Assert.Equal(dueDate, task.DueDate);
        Assert.Equal(storyPoints, task.StoryPoints);
        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.Null(task.AssigneeId);
    }

    [Fact]
    public void ChangeStatus_UpdatesStatusAndTimestamp()
    {
        // Arrange
        var task = TaskItem.Create("Task", null, Guid.NewGuid(), Priority.Low, "WEB-002");

        // Act
        task.ChangeStatus(TaskItemStatus.InProgress);

        // Assert
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.NotNull(task.UpdatedAt);
    }

    [Fact]
    public void Assign_SetsAssigneeIdAndTimestamp()
    {
        // Arrange
        var task = TaskItem.Create("Task", null, Guid.NewGuid(), Priority.Medium, "WEB-003");
        var userId = Guid.NewGuid();

        // Act
        task.Assign(userId);

        // Assert
        Assert.Equal(userId, task.AssigneeId);
        Assert.NotNull(task.UpdatedAt);

        // Act: unassign
        task.Assign(null);
        Assert.Null(task.AssigneeId);
    }

    [Fact]
    public void Update_ModifiesDetailsProperly()
    {
        // Arrange
        var task = TaskItem.Create("Original Title", "Original Desc", Guid.NewGuid(), Priority.Low, "WEB-004");
        var newDueDate = DateTime.UtcNow.AddDays(14);

        // Act
        task.Update("Updated Title", "Updated Desc", Priority.Critical, newDueDate, 8);

        // Assert
        Assert.Equal("Updated Title", task.Title);
        Assert.Equal("Updated Desc", task.Description);
        Assert.Equal(Priority.Critical, task.Priority);
        Assert.Equal(newDueDate, task.DueDate);
        Assert.Equal(8, task.StoryPoints);
        Assert.NotNull(task.UpdatedAt);
    }
}
