namespace ScoreCode.Database.Entities;

public class ActivityLog
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Session Session { get; set; } = null!;
}