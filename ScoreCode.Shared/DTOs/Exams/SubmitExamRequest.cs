namespace ScoreCode.Shared.DTOs.Exams;

/// <summary>Request body for submitting all answers at once.</summary>
public class SubmitExamRequest
{
    public Guid SessionId { get; set; }
    public List<AnswerDto> Answers { get; set; } = [];
}

/// <summary>A single answer within the submission.</summary>
public class AnswerDto
{
    public Guid QuestionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public DateTime FinalizedAt { get; set; }
}
