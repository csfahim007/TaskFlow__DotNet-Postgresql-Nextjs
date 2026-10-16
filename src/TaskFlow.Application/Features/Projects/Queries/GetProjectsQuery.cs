using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Application.Features.Projects.Commands;

namespace TaskFlow.Application.Features.Projects.Queries;

public record GetProjectsQuery(int Page = 1, int PageSize = 10, string? Search = null) : IRequest<Result<PagedResult<ProjectDto>>>;

public class GetProjectsQueryHandler : IRequestHandler<GetProjectsQuery, Result<PagedResult<ProjectDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetProjectsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<ProjectDto>>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        request = request with { Page = page, PageSize = size };

        var me = _currentUser.UserId;
        var isAdmin = _currentUser.IsAdmin;
        var query = _context.Projects
            .Include(p => p.Owner)
            .Include(p => p.Members)
            .Where(p => isAdmin || p.Members.Any(m => m.UserId == me))
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(p => p.Name.Contains(request.Search) || p.Key.Contains(request.Search));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.Key, p.Status.ToString(), p.CreatedAt, p.Owner != null ? p.Owner.FullName : ""))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<ProjectDto>>.Success(PagedResult<ProjectDto>.Create(items, total, request.Page, request.PageSize));
    }
}
