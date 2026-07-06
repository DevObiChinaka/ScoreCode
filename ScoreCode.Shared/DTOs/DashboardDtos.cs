namespace ScoreCode.Shared.DTOs;

public class StudentDashboardDto
{
    public int ExamsTaken { get; set; }
    public double AverageScore { get; set; }
    public int TotalSubmissions { get; set; }
    public string? NextExamTitle { get; set; }
    public DateTime? NextExamDate { get; set; }
    public List<RecentResultDto> RecentResults { get; set; } = [];
    public List<UpcomingExamDto> UpcomingExams { get; set; } = [];
}

public class LecturerDashboardDto
{
    public int ActiveExams { get; set; }
    public int TotalExams { get; set; }
    public int UngradedSubmissions { get; set; }
    public int TotalStudents { get; set; }
    public List<ExamSummaryDto> RecentExams { get; set; } = [];
}

public class RecentResultDto
{
    public string ExamTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class UpcomingExamDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
}

public class ExamSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int UngradedCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
