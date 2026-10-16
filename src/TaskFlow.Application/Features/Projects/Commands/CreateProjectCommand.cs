using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Features.Projects.Commands;

public record CreateProjectCommand(string Name, string Description, string Key) : IRequest<Result<ProjectDto>>;

public record ProjectDto(Guid Id, string Name, string? Description, string Key, string Status, DateTime CreatedAt, string OwnerName);

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Key).NotEmpty().MinimumLength(2).MaximumLength(10).Matches("^[A-Za-z]+$").WithMessage("Key must contain only letters.");
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Result<ProjectDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public CreateProjectCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IAuditService auditService)
    {
        _context = context;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<ProjectDto>> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var keyExists = await _context.Projects.AnyAsync(p => p.Key == request.Key.ToUpperInvariant(), cancellationToken);
        if (keyExists) return Result<ProjectDto>.Failure($"Project key '{request.Key.ToUpperInvariant()}' is already in use.", 409);

        var project = Project.Create(request.Name, request.Description, request.Key, _currentUser.UserId!.Value);
        project.SetCreatedBy(_currentUser.Email!);

        _context.Projects.Add(project);

        // Auto-add owner as ProjectManager
        var member = ProjectMember.Create(project.Id, _currentUser.UserId.Value, Domain.Enums.UserRole.ProjectManager);
        _context.ProjectMembers.Add(member);

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("Project", project.Id.ToString(), "Created",
            newValues: $"{{ Name: {project.Name}, Key: {project.Key} }}", cancellationToken: cancellationToken);

        var owner = await _context.Users.FindAsync([_currentUser.UserId.Value], cancellationToken);
        return Result<ProjectDto>.Success(new ProjectDto(project.Id, project.Name, project.Description, project.Key, project.Status.ToString(), project.CreatedAt, owner?.FullName ?? ""));
    }
}
