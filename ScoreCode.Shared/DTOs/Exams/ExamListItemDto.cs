namespace ScoreCode.Shared.DTOs.Exams;

/// <summary>Summary of an exam for the student exam list.</summary>
public class ExamListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public List<string> Languages { get; set; } = [];

    /// <summary>Non-null if the student has an active or completed session.</summary>
    public string? SessionStatus { get; set; }
}
