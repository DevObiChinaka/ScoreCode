using ScoreCode.Shared.DTOs;
using ScoreCode.Shared.DTOs.Exams;

namespace ScoreCode.Client.Services;

/// <summary>
/// Client-side service for exam-related API calls.
/// </summary>
public class ExamClientService
{
    private readonly ApiService _api;

    public ExamClientService(ApiService api)
    {
        _api = api;
    }

    /// <summary>List exams assigned to the authenticated student.</summary>
    public async Task<List<ExamListItemDto>> GetStudentExamsAsync()
    {
        var response = await _api.GetAsync<List<ExamListItemDto>>("exams/student");
        return response?.Data ?? [];
    }

    /// <summary>
    /// Start an exam session. Returns the full API response so callers can
    /// surface server-side reasons (e.g. exam not open yet, start window closed).
    /// </summary>
    public async Task<ApiResponse<ExamStartResponse>?> StartExamAsync(Guid examId)
    {
        return await _api.PostAsync<ExamStartResponse>($"exams/{examId}/start", new { });
    }

    /// <summary>Submit all answers for an exam.</summary>
    public async Task<bool> SubmitExamAsync(Guid examId, SubmitExamRequest request)
    {
        var response = await _api.PostAsync<object>($"exams/{examId}/submit", request);
        return response?.Success == true;
    }

    /// <summary>Get released exam results for the current student.</summary>
    public async Task<List<ExamResultDto>> GetStudentResultsAsync()
    {
        var response = await _api.GetAsync<List<ExamResultDto>>("exams/results");
        return response?.Data ?? [];
    }

    /// <summary>Report an anti-cheat / integrity event for an active session (best-effort).</summary>
    public async Task LogViolationAsync(Guid examId, LogViolationRequest request)
    {
        try
        {
            await _api.PostAsync<object>($"exams/{examId}/violation", request);
        }
        catch
        {
            // Best-effort telemetry — never let a logging failure disrupt the exam.
        }
    }
}
