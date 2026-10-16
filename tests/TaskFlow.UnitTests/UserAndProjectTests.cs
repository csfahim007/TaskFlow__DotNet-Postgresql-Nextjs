using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using Xunit;

namespace TaskFlow.UnitTests;

public class UserAndProjectTests
{
    [Fact]
    public void CreateUser_NormalizesEmailAndSetsDefaultRole()
    {
        // Act
        var user = User.Create("Alice", "Smith", "ALICE@EXAMPLE.COM", "hashed_secret");

        // Assert
        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal("Alice Smith", user.FullName);
        Assert.Equal(UserRole.Member, user.Role);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void User_DeactivateAndActivate_TogglesIsActive()
    {
        // Arrange
        var user = User.Create("Bob", "Jones", "bob@example.com", "hash");

        // Act & Assert
        user.Deactivate();
        Assert.False(user.IsActive);

        user.Activate();
        Assert.True(user.IsActive);
    }

    [Fact]
    public void CreateProject_SetsPropertiesAndActiveStatus()
    {
        // Arrange
        var ownerId = Guid.NewGuid();

        // Act
        var project = Project.Create("Mobile App", "iOS & Android app", "MOB", ownerId);

        // Assert
        Assert.Equal("Mobile App", project.Name);
        Assert.Equal("iOS & Android app", project.Description);
        Assert.Equal("MOB", project.Key);
        Assert.Equal(ownerId, project.OwnerId);
        Assert.Equal(ProjectStatus.Active, project.Status);
    }

    [Fact]
    public void Project_Archive_ChangesStatusToArchived()
    {
        // Arrange
        var project = Project.Create("Design System", "Core UI kit", "DS", Guid.NewGuid());

        // Act
        project.Archive();

        // Assert
        Assert.Equal(ProjectStatus.Archived, project.Status);
        Assert.NotNull(project.UpdatedAt);
    }
}
