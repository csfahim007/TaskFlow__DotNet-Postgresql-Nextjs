using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> NextTaskSequenceAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        // Single atomic UPDATE ... RETURNING: no read-modify-write race, even across API instances.
        var conn = Database.GetDbConnection();
        var opened = false;
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(cancellationToken);
            opened = true;
        }

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE \"Projects\" SET \"TaskCounter\" = \"TaskCounter\" + 1 WHERE \"Id\" = @id RETURNING \"TaskCounter\"";
            var p = cmd.CreateParameter();
            p.ParameterName = "id";
            p.Value = projectId;
            cmd.Parameters.Add(p);
            cmd.Transaction = Database.CurrentTransaction?.GetDbTransaction();
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull
                ? throw new InvalidOperationException($"Project {projectId} not found.")
                : Convert.ToInt32(result);
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }
    }
}
