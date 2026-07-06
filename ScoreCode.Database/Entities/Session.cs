namespace ScoreCode.Database.Entities;

public class Session
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ExamId { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime LastHeartbeatAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User Student { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
    public ICollection<Submission> Submissions { get; set; } = [];
    public ICollection<ActivityLog> ActivityLogs { get; set; } = [];
}