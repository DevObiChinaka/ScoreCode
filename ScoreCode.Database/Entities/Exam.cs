namespace ScoreCode.Database.Entities;

public class Exam
{
    public Guid Id { get; set; }
    public Guid LecturerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public string Languages { get; set; } = string.Empty; // JSON array stored as string
    public bool IntelliSenseEnabled { get; set; }
    public string GradingMode { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool ResultsReleased { get; set; }
    public DateTime? ResultsReleasedAt { get; set; }

    // Tenant (denormalised from the owning lecturer for direct filtering)
    public Guid? InstitutionId { get; set; }
    public Institution? Institution { get; set; }

    // Navigation
    public User Lecturer { get; set; } = null!;
    public ICollection<Question> Questions { get; set; } = [];
    public ICollection<ExamStudent> ExamStudents { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
}