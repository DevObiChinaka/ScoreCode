namespace ScoreCode.Shared.DTOs.Lecturer;

public class LecturerExamDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public List<string> Languages { get; set; } = [];
    public bool IntelliSenseEnabled { get; set; }
    public string GradingMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int StudentCount { get; set; }
    public int TotalMarks { get; set; }
    public int SubmissionCount { get; set; }
    public int UngradedCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool ResultsReleased { get; set; }
}
