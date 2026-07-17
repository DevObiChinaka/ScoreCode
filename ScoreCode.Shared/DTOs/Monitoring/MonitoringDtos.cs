namespace ScoreCode.Shared.DTOs.Monitoring;

/// <summary>Admin monitoring snapshot: live sessions + recent anti-cheat events.</summary>
public class MonitoringOverviewDto
{
    public int ActiveSessionCount { get; set; }
    public int TotalViolations { get; set; }
    public int CompletedTodayCount { get; set; }
    public List<ActiveSessionDto> ActiveSessions { get; set; } = [];
    public List<ViolationEventDto> RecentViolations { get; set; } = [];
}

/// <summary>A currently in-progress exam session.</summary>
public class ActiveSessionDto
{
    public Guid SessionId { get; set; }
    public string StudentMatric { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public int ViolationCount { get; set; }
}

/// <summary>A single anti-cheat / integrity event recorded during a session.</summary>
public class ViolationEventDto
{
    public string StudentMatric { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

/// <summary>One row of the exam integrity log, with enough context to read it standalone.</summary>
public class ActivityEntryDto
{
    public Guid ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string StudentMatric { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

/// <summary>A page of integrity-log rows plus the totals for the current filter.</summary>
public class ActivityLogDto
{
    public List<ActivityEntryDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int ViolationCount { get; set; }
    public int FlaggedStudentCount { get; set; }
    /// <summary>Exams that have at least one logged event — for the filter dropdown.</summary>
    public List<ActivityExamDto> Exams { get; set; } = [];
}

public class ActivityExamDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
}
