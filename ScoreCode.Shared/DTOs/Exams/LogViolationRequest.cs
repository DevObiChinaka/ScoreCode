namespace ScoreCode.Shared.DTOs.Exams;

/// <summary>
/// Reports an anti-cheat / integrity event that occurred during an active
/// exam session (e.g. the student switched away from the locked window).
/// </summary>
public class LogViolationRequest
{
    public Guid SessionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
