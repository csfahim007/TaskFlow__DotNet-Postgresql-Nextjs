using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Common.Events;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.BackgroundJobs;

/// <summary>
/// Scans for assigned tasks due within 24h and emits TaskDueSoonEvent through the outbox.
/// Safe with multiple instances: the event id is deterministic (task + due date), so the consumer's
/// idempotency guard collapses duplicates.
/// </summary>
public class DueDateReminderService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DueDateReminderService> _logger;

    public DueDateReminderService(IServiceScopeFactory scopeFactory, ILogger<DueDateReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Due-date reminder scan failed");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var horizon = now.AddHours(24);

        var tasks = await db.TaskItems
            .Include(t => t.Project)
            .Where(t => t.AssigneeId != null && t.DueDate != null && t.DueDate > now && t.DueDate <= horizon
                        && t.DueReminderSentAt == null
                        && t.Status != TaskItemStatus.Done && t.Status != TaskItemStatus.Cancelled
                        && t.Project!.Status == ProjectStatus.Active)
            .OrderBy(t => t.DueDate).Take(200)
            .ToListAsync(ct);
        if (tasks.Count == 0) return;

        foreach (var t in tasks)
        {
            var evt = new TaskDueSoonEvent(t.Id, t.TaskNumber, t.Title, t.ProjectId, t.AssigneeId!.Value, t.DueDate!.Value)
            {
                EventId = DeterministicId($"due:{t.Id}:{t.DueDate!.Value.Ticks}")
            };
            if (!await db.OutboxMessages.AnyAsync(m => m.Id == evt.EventId, ct))
                db.OutboxMessages.Add(Domain.Entities.OutboxMessage.Create(evt.EventId,
                    IntegrationEventSerializer.TypeName(evt), IntegrationEventSerializer.Serialize(evt)));
            t.MarkDueReminderSent();
        }

        try
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Queued {Count} due-soon reminders", tasks.Count);
        }
        catch (DbUpdateException ex)
        {
            // Another instance won the race (duplicate outbox id / concurrent update) — harmless.
            _logger.LogInformation(ex, "Due reminder batch conflicted with another instance; will re-evaluate next run");
        }
    }

    private static Guid DeterministicId(string seed) => new(MD5.HashData(Encoding.UTF8.GetBytes(seed)));
}
