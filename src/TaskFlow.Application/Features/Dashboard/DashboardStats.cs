using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Dashboard;

public record DashboardStatsDto(
    int ProjectCount,
    int MyOpenTasks,
    int MyOverdueTasks,
    int MyDueThisWeek,
    int MyCompletedLast7Days,
    Dictionary<string, int> MyTasksByStatus,
    Dictionary<string, int> MyTasksByPriority,
    int UnreadNotifications);

public record GetDashboardStatsQuery : IRequest<Result<DashboardStatsDto>>;

public class GetDashboardStatsQueryHandler : IRequestHandler<GetDashboardStatsQuery, Result<DashboardStatsDto>>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public GetDashboardStatsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, ICacheService cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result<DashboardStatsDto>> Handle(GetDashboardStatsQuery request, CancellationToken ct)
    {
        var me = _currentUser.UserId!.Value;
        var dto = await _cache.GetOrCreateAsync(CacheKeys.Dashboard(me), async token =>
        {
            var now = DateTime.UtcNow;
            var weekAhead = now.AddDays(7);
            var weekAgo = now.AddDays(-7);

            var mine = _context.TaskItems.AsNoTracking()
                .Where(t => t.AssigneeId == me && t.Project!.Members.Any(m => m.UserId == me));

            var byStatus = await mine.GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(token);
            var byPriority = await mine
                .Where(t => t.Status != TaskItemStatus.Done && t.Status != TaskItemStatus.Cancelled)
                .GroupBy(t => t.Priority).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(token);

            var open = mine.Where(t => t.Status != TaskItemStatus.Done && t.Status != TaskItemStatus.Cancelled);

            return new DashboardStatsDto(
                await _context.ProjectMembers.CountAsync(m => m.UserId == me, token),
                await open.CountAsync(token),
                await open.CountAsync(t => t.DueDate < now, token),
                await open.CountAsync(t => t.DueDate >= now && t.DueDate <= weekAhead, token),
                await mine.CountAsync(t => t.Status == TaskItemStatus.Done && t.CompletedAt >= weekAgo, token),
                byStatus.ToDictionary(x => x.Key.ToString(), x => x.Count),
                byPriority.ToDictionary(x => x.Key.ToString(), x => x.Count),
                await _context.Notifications.CountAsync(n => n.UserId == me && !n.IsRead, token));
        }, Ttl, ct);

        return Result<DashboardStatsDto>.Success(dto);
    }
}
