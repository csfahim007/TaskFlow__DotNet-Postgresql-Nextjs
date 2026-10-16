using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Dashboard;
using TaskFlow.Application.Features.Notifications;

namespace TaskFlow.API.Controllers;

[Authorize]
public class NotificationsController : BaseController
{
    /// <summary>Notifications of the current user, with unread counter</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetNotificationsQuery query, CancellationToken ct)
        => HandleResult(await Mediator.Send(query, ct));

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        => HandleResult(await Mediator.Send(new MarkNotificationReadCommand(id), ct));

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        => HandleResult(await Mediator.Send(new MarkAllNotificationsReadCommand(), ct));
}

[Authorize]
[Route("api/dashboard")]
public class DashboardController : BaseController
{
    /// <summary>Personal workload statistics (cached 30s)</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
        => HandleResult(await Mediator.Send(new GetDashboardStatsQuery(), ct));
}
