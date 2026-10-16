using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using Xunit;

namespace TaskFlow.UnitTests;

public class RbacAndAuthFeatureTests
{
    [Theory]
    [InlineData(UserRole.Viewer, ProjectPermission.View, true)]
    [InlineData(UserRole.Viewer, ProjectPermission.CreateTask, false)]
    [InlineData(UserRole.Viewer, ProjectPermission.EditTask, false)]
    [InlineData(UserRole.Viewer, ProjectPermission.DeleteAnyTask, false)]
    [InlineData(UserRole.Viewer, ProjectPermission.ManageMembers, false)]
    [InlineData(UserRole.Member, ProjectPermission.View, true)]
    [InlineData(UserRole.Member, ProjectPermission.CreateTask, true)]
    [InlineData(UserRole.Member, ProjectPermission.EditTask, true)]
    [InlineData(UserRole.Member, ProjectPermission.Comment, true)]
    [InlineData(UserRole.Member, ProjectPermission.DeleteAnyTask, false)]
    [InlineData(UserRole.Member, ProjectPermission.ManageMembers, false)]
    [InlineData(UserRole.ProjectManager, ProjectPermission.DeleteAnyTask, true)]
    [InlineData(UserRole.ProjectManager, ProjectPermission.ManageMembers, true)]
    [InlineData(UserRole.ProjectManager, ProjectPermission.ManageProject, true)]
    [InlineData(UserRole.Admin, ProjectPermission.ManageProject, true)]
    public void RolePermissions_Has_ReturnsExpectedForMatrix(UserRole role, ProjectPermission permission, bool expected)
    {
        var result = RolePermissions.Has(role, permission);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void User_RegisterFailedLogin_TriggersLockoutAfterThreshold()
    {
        var user = User.Create("Dave", "Lock", "dave@example.com", "hash");
        var maxAttempts = 3;
        var lockoutDuration = TimeSpan.FromMinutes(10);

        Assert.False(user.IsLockedOut);

        user.RegisterFailedLogin(maxAttempts, lockoutDuration);
        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut);

        user.RegisterFailedLogin(maxAttempts, lockoutDuration);
        Assert.Equal(2, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut);

        user.RegisterFailedLogin(maxAttempts, lockoutDuration);
        Assert.True(user.IsLockedOut);
        Assert.NotNull(user.LockoutEnd);

        // Successful login resets lockout
        user.RegisterSuccessfulLogin();
        Assert.False(user.IsLockedOut);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.NotNull(user.LastLoginAt);
    }

    [Fact]
    public void RefreshToken_CreateAndRevoke_BehavesCorrectly()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var tokenHash = "abc123hash";
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var token = RefreshToken.Create(userId, familyId, tokenHash, expiresAt, "127.0.0.1");

        Assert.Equal(userId, token.UserId);
        Assert.Equal(familyId, token.FamilyId);
        Assert.Equal(tokenHash, token.TokenHash);
        Assert.True(token.IsActive);

        token.Revoke("nextHash456");
        Assert.False(token.IsActive);
        Assert.NotNull(token.RevokedAt);
        Assert.Equal("nextHash456", token.ReplacedByHash);
    }

    [Fact]
    public void OutboxMessage_MarkProcessedAndFailed_CalculatesRetry()
    {
        var msg = OutboxMessage.Create(Guid.NewGuid(), "TaskAssignedEvent", "{}");

        Assert.Null(msg.ProcessedAt);
        Assert.Equal(0, msg.Attempts);

        msg.MarkFailed("Network timeout error");
        Assert.Equal(1, msg.Attempts);
        Assert.Equal("Network timeout error", msg.Error);
        Assert.True(msg.NextAttemptAt > DateTime.UtcNow);

        msg.MarkProcessed();
        Assert.NotNull(msg.ProcessedAt);
        Assert.Null(msg.Error);
    }

    [Fact]
    public void Notification_CreateAndMarkRead_UpdatesState()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var notif = Notification.Create(userId, eventId, NotificationType.TaskAssigned, "Task Assigned", "You were assigned TF-001", null, null);

        Assert.False(notif.IsRead);
        Assert.Null(notif.ReadAt);

        notif.MarkRead();
        Assert.True(notif.IsRead);
        Assert.NotNull(notif.ReadAt);
    }
}
