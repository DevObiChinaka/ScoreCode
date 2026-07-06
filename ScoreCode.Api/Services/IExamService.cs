using ScoreCode.Shared.DTOs.Exams;

namespace ScoreCode.Api.Services;

public interface IExamService
{
    /// <summary>List exams assigned to a student.</summary>
    Task<List<ExamListItemDto>> GetStudentExamsAsync(Guid studentId);

    /// <summary>Start an exam session. Returns questions + nonce.</summary>
    Task<ExamStartResponse> StartExamAsync(Guid examId, Guid studentId, string deviceFingerprint);

    /// <summary>Submit all answers for an exam session.</summary>
    Task SubmitExamAsync(Guid examId, Guid studentId, SubmitExamRequest request);

    /// <summary>Get graded results for a student (only released exams).</summary>
    Task<List<ExamResultDto>> GetStudentResultsAsync(Guid studentId);

    /// <summary>Record an anti-cheat / integrity violation against a session.</summary>
    Task LogViolationAsync(Guid examId, Guid studentId, LogViolationRequest request);
}
