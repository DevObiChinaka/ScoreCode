namespace ScoreCode.Shared.DTOs.Lecturer;

public class CreateExamRequest
{
    public string Title { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public List<string> Languages { get; set; } = [];
    public bool IntelliSenseEnabled { get; set; }
    public string GradingMode { get; set; } = "Manual";
}
