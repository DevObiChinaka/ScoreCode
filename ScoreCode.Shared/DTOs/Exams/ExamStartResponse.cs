namespace ScoreCode.Shared.DTOs.Exams;

/// <summary>Returned when a student starts an exam session.</summary>
public class ExamStartResponse
{
    public Guid ExamId { get; set; }
    public Guid SessionId { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IntelliSenseEnabled { get; set; } = true;
    public List<QuestionDto> Questions { get; set; } = [];
}
