using ScoreCode.Shared.DTOs;
using ScoreCode.Shared.DTOs.Lecturer;
using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Client.Services;

/// <summary>Wraps Lecturer API endpoints.</summary>
public class LecturerClientService
{
    private readonly ApiService _api;
    public LecturerClientService(ApiService api) => _api = api;

    // ── Exams ──────────────────────────────────────────────────────────────
    public async Task<List<LecturerExamDto>> GetExamsAsync()
    {
        var resp = await _api.GetAsync<List<LecturerExamDto>>("lecturer/exams");
        return resp?.Data ?? [];
    }

    public async Task<LecturerExamDto?> GetExamAsync(Guid id)
    {
        var resp = await _api.GetAsync<LecturerExamDto>($"lecturer/exams/{id}");
        return resp?.Data;
    }

    public async Task<LecturerExamDto?> CreateExamAsync(CreateExamRequest req)
    {
        var resp = await _api.PostAsync<LecturerExamDto>("lecturer/exams", req);
        return resp?.Data;
    }

    public async Task<LecturerExamDto?> UpdateExamAsync(Guid id, UpdateExamRequest req)
    {
        var resp = await _api.PatchAsync<LecturerExamDto>($"lecturer/exams/{id}", req);
        return resp?.Data;
    }

    public async Task<bool> DeleteExamAsync(Guid id)
    {
        var resp = await _api.DeleteAsync<object>($"lecturer/exams/{id}");
        return resp?.Success == true;
    }

    public async Task<bool> PublishExamAsync(Guid id)
    {
        var resp = await _api.PatchAsync<object>($"lecturer/exams/{id}/publish");
        return resp?.Success == true;
    }

    public async Task<bool> CloseExamAsync(Guid id)
    {
        var resp = await _api.PatchAsync<object>($"lecturer/exams/{id}/close");
        return resp?.Success == true;
    }

    /// <summary>
    /// Release results to students. Returns success plus a server message so the
    /// caller can explain a refusal (e.g. ungraded submissions still exist).
    /// </summary>
    public async Task<(bool Ok, string? Message)> ReleaseResultsAsync(Guid examId)
    {
        var resp = await _api.PostAsync<string>($"lecturer/exams/{examId}/release-results", new { });
        return (resp?.Success == true, resp?.Message);
    }

    // ── Questions ──────────────────────────────────────────────────────────
    public async Task<List<LecturerQuestionDto>> GetQuestionsAsync(Guid examId)
    {
        var resp = await _api.GetAsync<List<LecturerQuestionDto>>($"lecturer/exams/{examId}/questions");
        return resp?.Data ?? [];
    }

    public async Task<LecturerQuestionDto?> AddQuestionAsync(Guid examId, CreateQuestionRequest req)
    {
        var resp = await _api.PostAsync<LecturerQuestionDto>($"lecturer/exams/{examId}/questions", req);
        return resp?.Data;
    }

    public async Task<bool> DeleteQuestionAsync(Guid examId, Guid questionId)
    {
        var resp = await _api.DeleteAsync<object>($"lecturer/exams/{examId}/questions/{questionId}");
        return resp?.Success == true;
    }

    public async Task<LecturerQuestionDto?> UpdateQuestionAsync(Guid examId, Guid questionId, CreateQuestionRequest req)
    {
        var resp = await _api.PutAsync<LecturerQuestionDto>($"lecturer/exams/{examId}/questions/{questionId}", req);
        return resp?.Data;
    }

    // ── Students ──────────────────────────────────────────────────────────
    public async Task<List<ExamStudentDto>> GetStudentsAsync(Guid examId)
    {
        var resp = await _api.GetAsync<List<ExamStudentDto>>($"lecturer/exams/{examId}/students");
        return resp?.Data ?? [];
    }

    public async Task<int> AssignStudentsAsync(Guid examId, List<Guid> studentIds)
    {
        var req = new AssignStudentsRequest { StudentIds = studentIds };
        var resp = await _api.PostAsync<object>($"lecturer/exams/{examId}/students", req);
        return resp?.Success == true ? studentIds.Count : 0;
    }

    public async Task<bool> RemoveStudentAsync(Guid examId, Guid studentId)
    {
        var resp = await _api.DeleteAsync<object>($"lecturer/exams/{examId}/students/{studentId}");
        return resp?.Success == true;
    }

    // ── Submissions ───────────────────────────────────────────────────────
    public async Task<List<SubmissionListItemDto>> GetSubmissionsAsync(Guid examId)
    {
        var resp = await _api.GetAsync<List<SubmissionListItemDto>>($"lecturer/exams/{examId}/submissions");
        return resp?.Data ?? [];
    }

    public async Task<SubmissionDetailDto?> GetSubmissionDetailAsync(Guid submissionId)
    {
        var resp = await _api.GetAsync<SubmissionDetailDto>($"lecturer/submissions/{submissionId}");
        return resp?.Data;
    }

    public async Task<bool> GradeAsync(Guid submissionId, GradeSubmissionRequest req)
    {
        var resp = await _api.PostAsync<object>($"lecturer/submissions/{submissionId}/grade", req);
        return resp?.Success == true;
    }

    /// <summary>Request an AI-suggested grade. Returns the full response so the
    /// caller can surface configuration/provider errors.</summary>
    public async Task<ApiResponse<AiGradeSuggestionDto>?> AiSuggestAsync(Guid submissionId)
    {
        return await _api.PostAsync<AiGradeSuggestionDto>(
            $"lecturer/submissions/{submissionId}/ai-suggest", new { });
    }

    // ── Users (for student picker) ────────────────────────────────────────
    public async Task<List<UserDto>> GetStudentListAsync(string? programme = null, string? level = null)
    {
        var query = "lecturer/students";
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(programme)) parts.Add($"programme={Uri.EscapeDataString(programme)}");
        if (!string.IsNullOrWhiteSpace(level)) parts.Add($"level={Uri.EscapeDataString(level)}");
        if (parts.Count > 0) query += "?" + string.Join("&", parts);

        var resp = await _api.GetAsync<List<UserDto>>(query);
        return resp?.Data ?? [];
    }
}
