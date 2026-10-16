namespace TaskFlow.Application.Common.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        string entityName,
        string entityId,
        string action,
        string? oldValues = null,
        string? newValues = null,
        CancellationToken cancellationToken = default);
}
