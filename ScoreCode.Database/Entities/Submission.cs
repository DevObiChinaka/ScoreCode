namespace ScoreCode.Database.Entities;

public class Submission
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ExamId { get; set; }
    public Guid QuestionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime FinalizedAt { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string UploadStatus { get; set; } = "Uploaded";

    // Tenant (denormalised from the session for direct filtering)
    public Guid? InstitutionId { get; set; }
    public Institution? Institution { get; set; }

    // Navigation
    public Session Session { get; set; } = null!;
    public User Student { get; set; } = null!;
    public Question Question { get; set; } = null!;
    public Grade? Grade { get; set; }
}