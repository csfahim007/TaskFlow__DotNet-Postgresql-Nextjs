using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Events;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Projects;

public record ProjectDetailDto(
    Guid Id, string Name, string? Description, string Key, string Status, DateTime CreatedAt,
    Guid OwnerId, string OwnerName, string MyRole, int MemberCount, int TotalTasks, int OpenTasks, int OverdueTasks);

public record MemberDto(Guid UserId, string FullName, string Email, string Role, bool IsOwner, DateTime JoinedAt);

// ---------- Get one ----------
public record GetProjectQuery(Guid ProjectId) : IRequest<Result<ProjectDetailDto>>;

public class GetProjectQueryHandler : IRequestHandler<GetProjectQuery, Result<ProjectDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProjectAccessService _access;

    public GetProjectQueryHandler(IApplicationDbContext context, IProjectAccessService access)
    {
        _context = context;
        _access = access;
    }

    public async Task<Result<ProjectDetailDto>> Handle(GetProjectQuery r, CancellationToken ct)
    {
        var role = await _access.GetRoleAsync(r.ProjectId, ct);
        if (role is null) return Result<ProjectDetailDto>.Failure("Project not found.", 404); // don't leak existence

        var now = DateTime.UtcNow;
        var dto = await _context.Projects.AsNoTracking().Where(p => p.Id == r.ProjectId)
            .Select(p => new ProjectDetailDto(p.Id, p.Name, p.Description, p.Key, p.Status.ToString(), p.CreatedAt,
                p.OwnerId, p.Owner != null ? p.Owner.FirstName + " " + p.Owner.LastName : "", role.ToString()!,
                p.Members.Count, p.Tasks.Count,
                p.Tasks.Count(t => t.Status != TaskItemStatus.Done && t.Status != TaskItemStatus.Cancelled),
                p.Tasks.Count(t => t.DueDate < now && t.Status != TaskItemStatus.Done && t.Status != TaskItemStatus.Cancelled)))
            .FirstOrDefaultAsync(ct);

        return dto is null ? Result<ProjectDetailDto>.NotFound("Project not found.") : Result<ProjectDetailDto>.Success(dto);
    }
}

// ---------- Update ----------
public record UpdateProjectCommand(Guid ProjectId, string Name, string? Description) : IRequest<Result>;

public class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IProjectAccessService _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public UpdateProjectCommandHandler(IApplicationDbContext context, IProjectAccessService access, ICurrentUserService currentUser, IAuditService audit)
    {
        _context = context;
        _access = access;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Result> Handle(UpdateProjectCommand r, CancellationToken ct)
    {
        if (await _access.GetRoleAsync(r.ProjectId, ct) is null) return Result.Failure("Project not found.", 404);
        if (!await _access.HasPermissionAsync(r.ProjectId, ProjectPermission.ManageProject, ct))
            return Result.Failure("Only project managers can edit the project.", 403);

        var project = await _context.Projects.FirstAsync(p => p.Id == r.ProjectId, ct);
        var old = project.Name;
        project.Update(r.Name, r.Description);
        project.SetUpdatedBy(_currentUser.Email!);
        await _context.SaveChangesAsync(ct);
        await _audit.LogAsync("Project", project.Id.ToString(), "Updated", old, project.Name, ct);
        return Result.Success();
    }
}

// ---------- Change status (archive / complete / reactivate) ----------
public record SetProjectStatusCommand(Guid ProjectId, ProjectStatus Status) : IRequest<Result>;

public class SetProjectStatusCommandHandler : IRequestHandler<SetProjectStatusCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IProjectAccessService _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public SetProjectStatusCommandHandler(IApplicationDbContext context, IProjectAccessService access, ICurrentUserService currentUser, IAuditService audit)
    {
        _context = context;
        _access = access;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Result> Handle(SetProjectStatusCommand r, CancellationToken ct)
    {
        if (await _access.GetRoleAsync(r.ProjectId, ct) is null) return Result.Failure("Project not found.", 404);
        if (!await _access.HasPermissionAsync(r.ProjectId, ProjectPermission.ManageProject, ct))
            return Result.Failure("Only project managers can change project status.", 403);

        var project = await _context.Projects.FirstAsync(p => p.Id == r.ProjectId, ct);
        var old = project.Status.ToString();
        switch (r.Status)
        {
            case ProjectStatus.Archived: project.Archive(); break;
            case ProjectStatus.Completed: project.Complete(); break;
            default: project.Reactivate(); break;
        }
        project.SetUpdatedBy(_currentUser.Email!);
        await _context.SaveChangesAsync(ct);
        await _audit.LogAsync("Project", project.Id.ToString(), "StatusChanged", old, r.Status.ToString(), ct);
        return Result.Success();
    }
}

// ---------- Members: list ----------
public record GetProjectMembersQuery(Guid ProjectId) : IRequest<Result<List<MemberDto>>>;

public class GetProjectMembersQueryHandler : IRequestHandler<GetProjectMembersQuery, Result<List<MemberDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProjectAccessService _access;

    public GetProjectMembersQueryHandler(IApplicationDbContext context, IProjectAccessService access)
    {
        _context = context;
        _access = access;
    }

    public async Task<Result<List<MemberDto>>> Handle(GetProjectMembersQuery r, CancellationToken ct)
    {
        if (!await _access.HasPermissionAsync(r.ProjectId, ProjectPermission.View, ct))
            return Result<List<MemberDto>>.Failure("Project not found.", 404);

        var members = await _context.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == r.ProjectId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MemberDto(m.UserId, m.User!.FirstName + " " + m.User.LastName, m.User.Email,
                m.Role.ToString(), m.Project!.OwnerId == m.UserId, m.CreatedAt))
            .ToListAsync(ct);
        return Result<List<MemberDto>>.Success(members);
    }
}

// ---------- Members: add ----------
public record AddProjectMemberCommand(Guid ProjectId, string Email, UserRole Role) : IRequest<Result<MemberDto>>;

public class AddProjectMemberCommandValidator : AbstractValidator<AddProjectMemberCommand>
{
    public AddProjectMemberCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role).Must(r => r is UserRole.Viewer or UserRole.Member or UserRole.ProjectManager)
            .WithMessage("Project role must be Viewer, Member or ProjectManager.");
    }
}

public class AddProjectMemberCommandHandler : IRequestHandler<AddProjectMemberCommand, Result<MemberDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IProjectAccessService _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IEventBus _bus;
    private readonly IAuditService _audit;

    public AddProjectMemberCommandHandler(IApplicationDbContext context, IProjectAccessService access,
        ICurrentUserService currentUser, IEventBus bus, IAuditService audit)
    {
        _context = context;
        _access = access;
        _currentUser = currentUser;
        _bus = bus;
        _audit = audit;
    }

    public async Task<Result<MemberDto>> Handle(AddProjectMemberCommand r, CancellationToken ct)
    {
        if (await _access.GetRoleAsync(r.ProjectId, ct) is null) return Result<MemberDto>.Failure("Project not found.", 404);
        if (!await _access.HasPermissionAsync(r.ProjectId, ProjectPermission.ManageMembers, ct))
            return Result<MemberDto>.Failure("Only project managers can add members.", 403);

        var email = r.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);
        if (user is null) return Result<MemberDto>.Failure("No active user with that email.", 404);

        if (await _context.ProjectMembers.AnyAsync(m => m.ProjectId == r.ProjectId && m.UserId == user.Id, ct))
            return Result<MemberDto>.Failure("User is already a member of this project.", 409);

        var project = await _context.Projects.AsNoTracking().FirstAsync(p => p.Id == r.ProjectId, ct);
        var member = ProjectMember.Create(r.ProjectId, user.Id, r.Role);
        member.SetCreatedBy(_currentUser.Email!);
        _context.ProjectMembers.Add(member);

        var actor = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == _currentUser.UserId, ct);
        _bus.Publish(new ProjectMemberAddedEvent(project.Id, project.Name, user.Id, r.Role.ToString(), actor.Id, actor.FullName));
        await _context.SaveChangesAsync(ct);

        await _access.InvalidateAsync(r.ProjectId, user.Id, ct);
        await _audit.LogAsync("Project", project.Id.ToString(), "MemberAdded", null, $"{user.Email}:{r.Role}", ct);
        return Result<MemberDto>.Success(new MemberDto(user.Id, user.FullName, user.Email, r.Role.ToString(), false, member.CreatedAt));
    }
}

// ---------- Members: change role ----------
public record ChangeProjectMemberRoleCommand(Guid ProjectId, Guid UserId, UserRole Role) : IRequest<Result>;

public class ChangeProjectMemberRoleCommandValidator : AbstractValidator<ChangeProjectMemberRoleCommand>
{
    public ChangeProjectMemberRoleCommandValidator() =>
        RuleFor(x => x.Role).Must(r => r is UserRole.Viewer or UserRole.Member or UserRole.ProjectManager)
            .WithMessage("Project role must be Viewer, Member or ProjectManager.");
}

public class ChangeProjectMemberRoleCommandHandler : IRequestHandler<ChangeProjectMemberRoleCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IProjectAccessService _access;
    private readonly IAuditService _audit;

    public ChangeProjectMemberRoleCommandHandler(IApplicationDbContext context, IProjectAccessService access, IAuditService audit)
    {
        _context = context;
        _access = access;
        _audit = audit;
    }

    public async Task<Result> Handle(ChangeProjectMemberRoleCommand r, CancellationToken ct)
    {
        if (await _access.GetRoleAsync(r.ProjectId, ct) is null) return Result.Failure("Project not found.", 404);
        if (!await _access.HasPermissionAsync(r.ProjectId, ProjectPermission.ManageMembers, ct))
            return Result.Failure("Only project managers can change roles.", 403);

        var project = await _context.Projects.AsNoTracking().FirstAsync(p => p.Id == r.ProjectId, ct);
        if (project.OwnerId == r.UserId) return Result.Failure("The project owner's role cannot be changed.", 400);

        var member = await _context.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == r.ProjectId && m.UserId == r.UserId, ct);
        if (member is null) return Result.Failure("Member not found.", 404);

        var old = member.Role.ToString();
        member.ChangeRole(r.Role);
        await _context.SaveChangesAsync(ct);

        await _access.InvalidateAsync(r.ProjectId, r.UserId, ct);
        await _audit.LogAsync("Project", r.ProjectId.ToString(), "MemberRoleChanged", old, $"{r.UserId}:{r.Role}", ct);
        return Result.Success();
    }
}

// ---------- Members: remove ----------
public record RemoveProjectMemberCommand(Guid ProjectId, Guid UserId) : IRequest<Result>;

public class RemoveProjectMemberCommandHandler : IRequestHandler<RemoveProjectMemberCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IProjectAccessService _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public RemoveProjectMemberCommandHandler(IApplicationDbContext context, IProjectAccessService access, ICurrentUserService currentUser, IAuditService audit)
    {
        _context = context;
        _access = access;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Result> Handle(RemoveProjectMemberCommand r, CancellationToken ct)
    {
        if (await _access.GetRoleAsync(r.ProjectId, ct) is null) return Result.Failure("Project not found.", 404);

        var isSelf = r.UserId == _currentUser.UserId;
        if (!isSelf && !await _access.HasPermissionAsync(r.ProjectId, ProjectPermission.ManageMembers, ct))
            return Result.Failure("Only project managers can remove members.", 403);

        var project = await _context.Projects.AsNoTracking().FirstAsync(p => p.Id == r.ProjectId, ct);
        if (project.OwnerId == r.UserId) return Result.Failure("The project owner cannot be removed.", 400);

        var member = await _context.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == r.ProjectId && m.UserId == r.UserId, ct);
        if (member is null) return Result.Failure("Member not found.", 404);

        // Unassign their open work so nothing is orphaned on a user who can no longer see it.
        var tasks = await _context.TaskItems.Where(t => t.ProjectId == r.ProjectId && t.AssigneeId == r.UserId).ToListAsync(ct);
        foreach (var t in tasks) t.Assign(null);

        _context.ProjectMembers.Remove(member);
        await _context.SaveChangesAsync(ct);

        await _access.InvalidateAsync(r.ProjectId, r.UserId, ct);
        await _audit.LogAsync("Project", r.ProjectId.ToString(), "MemberRemoved", r.UserId.ToString(), null, ct);
        return Result.Success();
    }
}
