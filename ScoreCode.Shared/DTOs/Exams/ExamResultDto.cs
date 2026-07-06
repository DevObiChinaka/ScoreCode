namespace ScoreCode.Shared.DTOs.Exams;

/// <summary>Student-facing exam result (only visible when lecturer releases results).</summary>
public class ExamResultDto
{
    public Guid ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime ResultsReleasedAt { get; set; }
    public List<QuestionResultDto> Questions { get; set; } = [];
    public int TotalScore { get; set; }
    public int TotalMaxScore { get; set; }
}

public class QuestionResultDto
{
    public Guid QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public string? Feedback { get; set; }
}
