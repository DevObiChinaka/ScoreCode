namespace ScoreCode.Shared.DTOs.Lecturer;

public class SubmissionListItemDto
{
    public Guid Id { get; set; }
    public string StudentMatric { get; set; } = string.Empty;
    public int QuestionNumber { get; set; }
    public string CodePreview { get; set; } = string.Empty;
    public int? Score { get; set; }
    public int MaxScore { get; set; }
    public string? GradedBy { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class SubmissionDetailDto
{
    public Guid Id { get; set; }
    public string StudentMatric { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string MarkingGuide { get; set; } = string.Empty;
    public int MaxMarks { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public GradeDto? Grade { get; set; }
}

public class GradeDto
{
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public string GradedBy { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
    public DateTime GradedAt { get; set; }
}

public class GradeSubmissionRequest
{
    public int Score { get; set; }
    public string Feedback { get; set; } = string.Empty;
}

/// <summary>An AI-suggested grade for a submission. The lecturer reviews it,
/// may edit it, and saves it through the normal grade endpoint.</summary>
public class AiGradeSuggestionDto
{
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public string Feedback { get; set; } = string.Empty;
}
