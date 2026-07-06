namespace ScoreCode.Api.Services;

/// <summary>What the grader needs to assess one submission.</summary>
public record AiGradeRequest(
    string QuestionText,
    string MarkingGuide,
    int MaxMarks,
    string Language,
    string Code);

/// <summary>The grader's suggestion. Never authoritative — a lecturer reviews it.</summary>
public record AiGradeResult(int Score, string Feedback);

/// <summary>Thrown when AI grading is invoked but no provider key is configured.</summary>
public class AiNotConfiguredException : Exception
{
    public AiNotConfiguredException(string message) : base(message) { }
}

/// <summary>
/// Provider-agnostic AI grading seam. The default implementation targets Gemini;
/// a Claude (direct or via MCP) implementation can be dropped in behind this
/// interface without touching the grading flow.
/// </summary>
public interface IAiGradingService
{
    Task<AiGradeResult> SuggestGradeAsync(AiGradeRequest request, CancellationToken ct = default);
}
