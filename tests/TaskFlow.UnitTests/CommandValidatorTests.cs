using TaskFlow.Application.Features.Auth.Commands;
using TaskFlow.Application.Features.Comments.Commands;
using TaskFlow.Application.Features.Projects.Commands;
using TaskFlow.Application.Features.Tasks.Commands;
using TaskFlow.Domain.Enums;
using Xunit;

namespace TaskFlow.UnitTests;

public class CommandValidatorTests
{
    [Fact]
    public void RegisterCommandValidator_RequiresValidEmailAndPasswordLength()
    {
        var validator = new RegisterCommandValidator();

        var invalidCommand = new RegisterCommand("Bob", "Ross", "invalid-email", "123", "123");
        var result = validator.Validate(invalidCommand);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Email");
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");

        var validCommand = new RegisterCommand("Bob", "Ross", "bob@example.com", "SecurePassword123!", "SecurePassword123!");
        var validResult = validator.Validate(validCommand);
        Assert.True(validResult.IsValid);
    }

    [Fact]
    public void LoginCommandValidator_RequiresEmailAndPassword()
    {
        var validator = new LoginCommandValidator();

        var emptyCommand = new LoginCommand("", "");
        var result = validator.Validate(emptyCommand);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Email");
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");

        var validCommand = new LoginCommand("user@example.com", "Password123!");
        var validResult = validator.Validate(validCommand);
        Assert.True(validResult.IsValid);
    }

    [Fact]
    public void CreateProjectCommandValidator_EnforcesKeyLengthAndFormat()
    {
        var validator = new CreateProjectCommandValidator();

        var invalidKeyCommand = new CreateProjectCommand("My Project", "Desc", "A"); // too short (< 2)
        var result = validator.Validate(invalidKeyCommand);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Key");

        var invalidCharsCommand = new CreateProjectCommand("My Project", "Desc", "PR12"); // digits not allowed
        var resChars = validator.Validate(invalidCharsCommand);
        Assert.False(resChars.IsValid);
        Assert.Contains(resChars.Errors, e => e.PropertyName == "Key");

        var validCommand = new CreateProjectCommand("My Project", "Desc", "PROJ");
        var validResult = validator.Validate(validCommand);
        Assert.True(validResult.IsValid);
    }

    [Fact]
    public void CreateTaskCommandValidator_EnforcesTitleAndPointsRange()
    {
        var validator = new CreateTaskCommandValidator();

        var invalidCommand = new CreateTaskCommand(
            "",
            null,
            Guid.NewGuid(),
            Priority.Medium,
            null,
            200,
            null); // story points max is 100

        var result = validator.Validate(invalidCommand);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Title");
        Assert.Contains(result.Errors, e => e.PropertyName == "StoryPoints");

        var validCommand = new CreateTaskCommand(
            "Setup Redis cache",
            "Details",
            Guid.NewGuid(),
            Priority.High,
            DateTime.UtcNow.AddDays(2),
            8,
            null);

        var validResult = validator.Validate(validCommand);
        Assert.True(validResult.IsValid);
    }

    [Fact]
    public void AddCommentCommandValidator_EnforcesContentLength()
    {
        var validator = new AddCommentCommandValidator();

        var emptyCommand = new AddCommentCommand(Guid.NewGuid(), "");
        var result = validator.Validate(emptyCommand);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Content");

        var validCommand = new AddCommentCommand(Guid.NewGuid(), "Great progress today team!");
        var validResult = validator.Validate(validCommand);
        Assert.True(validResult.IsValid);
    }
}
