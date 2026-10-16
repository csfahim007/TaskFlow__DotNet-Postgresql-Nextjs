using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class AuditLog : BaseEntity
{
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty; // Created, Updated, Deleted
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public Guid? UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public string? IpAddress { get; private set; }

    // Navigation
    public User? User { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        string entityName,
        string entityId,
        string action,
        Guid? userId,
        string? userEmail,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null)
    {
        return new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            UserEmail = userEmail,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress
        };
    }
}
