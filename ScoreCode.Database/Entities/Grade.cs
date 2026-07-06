namespace ScoreCode.Database.Entities;

public class Grade
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public string GradedBy { get; set; } = string.Empty; // "AI" or "Manual"
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public string FeedbackJson { get; set; } = string.Empty; // JSON array
    public string AiModel { get; set; } = string.Empty;
    public string AiPromptHash { get; set; } = string.Empty;
    public Guid? OverriddenBy { get; set; }
    public string? OverrideReason { get; set; }
    public DateTime GradedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Submission Submission { get; set; } = null!;
}