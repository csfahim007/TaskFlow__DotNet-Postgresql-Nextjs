namespace TaskFlow.Domain.Entities;

/// <summary>Transactional outbox record; written in the same transaction as the business change.</summary>
public class OutboxMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime NextAttemptAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(Guid id, string type, string payload)
        => new() { Id = id, Type = type, Payload = payload };

    public void MarkProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        Error = error.Length > 1000 ? error[..1000] : error;
        NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(Math.Pow(2, Attempts), 300));
    }
}
