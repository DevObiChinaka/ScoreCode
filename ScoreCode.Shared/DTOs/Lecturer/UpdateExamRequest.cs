namespace ScoreCode.Shared.DTOs.Lecturer;

public class UpdateExamRequest
{
    public string? Title { get; set; }
    public string? CourseCode { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public int? DurationMinutes { get; set; }
    public List<string>? Languages { get; set; }
    public bool? IntelliSenseEnabled { get; set; }
    public string? GradingMode { get; set; }
}
