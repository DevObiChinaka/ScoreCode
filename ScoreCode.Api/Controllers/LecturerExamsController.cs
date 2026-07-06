using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScoreCode.Api.Services;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs;
using ScoreCode.Shared.DTOs.Lecturer;
using System.Security.Claims;

namespace ScoreCode.Api.Controllers;

[ApiController]
[Route("api/v1/lecturer/exams")]
[Authorize(Roles = Constants.Roles.Lecturer)]
public class LecturerExamsController : ControllerBase
{
    private readonly ILecturerExamService _svc;
    private readonly ExamAutoPublishService _autoPublish;

    public LecturerExamsController(ILecturerExamService svc, ExamAutoPublishService autoPublish)
    {
        _svc = svc;
        _autoPublish = autoPublish;
    }

    private Guid GetLecturerId() =>
        Guid.Parse(User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException());

    // ── Exam CRUD ──────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExamRequest req)
    {
        var result = await _svc.CreateExamAsync(GetLecturerId(), req);
        // Schedule auto-publish timer for this exam
        _autoPublish.ScheduleExam(result.Id, result.ScheduledAt);
        return StatusCode(201, ApiResponse<LecturerExamDto>.Ok(result));
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var list = await _svc.GetExamsAsync(GetLecturerId());
        return Ok(ApiResponse<List<LecturerExamDto>>.Ok(list));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _svc.GetExamAsync(GetLecturerId(), id);
        return result is null
            ? NotFound(ApiResponse<object>.Fail("not_found", "Exam not found."))
            : Ok(ApiResponse<LecturerExamDto>.Ok(result));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExamRequest req)
    {
        try
        {
            var result = await _svc.UpdateExamAsync(GetLecturerId(), id, req);
            if (result is null)
                return NotFound(ApiResponse<object>.Fail("not_found", "Exam not found."));

            // Reschedule timer if schedule was updated
            if (result.Status == Constants.ExamStatuses.Draft)
                _autoPublish.ScheduleExam(result.Id, result.ScheduledAt);

            return Ok(ApiResponse<LecturerExamDto>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("invalid_state", ex.Message));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var ok = await _svc.DeleteExamAsync(GetLecturerId(), id);
            if (!ok) return NotFound(ApiResponse<object>.Fail("not_found", "Exam not found."));
            _autoPublish.CancelExam(id); // cancel pending timer
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("invalid_state", ex.Message));
        }
    }

    [HttpPatch("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        try
        {
            var ok = await _svc.PublishExamAsync(GetLecturerId(), id);
            if (!ok) return NotFound(ApiResponse<object>.Fail("not_found", "Exam not found."));
            _autoPublish.CancelExam(id); // no longer needed — manually published
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("invalid_state", ex.Message));
        }
    }

    [HttpPatch("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id)
    {
        try
        {
            var ok = await _svc.CloseExamAsync(GetLecturerId(), id);
            if (!ok) return NotFound(ApiResponse<object>.Fail("not_found", "Exam not found."));
            _autoPublish.CancelExam(id);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("invalid_state", ex.Message));
        }
    }

    // ── Questions ──────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/questions")]
    public async Task<IActionResult> GetQuestions(Guid id)
    {
        var list = await _svc.GetQuestionsAsync(GetLecturerId(), id);
        return Ok(ApiResponse<List<LecturerQuestionDto>>.Ok(list));
    }

    [HttpPost("{id:guid}/questions")]
    public async Task<IActionResult> AddQuestion(Guid id, [FromBody] CreateQuestionRequest req)
    {
        try
        {
            var result = await _svc.AddQuestionAsync(GetLecturerId(), id, req);
            return StatusCode(201, ApiResponse<LecturerQuestionDto>.Ok(result));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail("not_found", "Exam not found."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("invalid_state", ex.Message));
        }
    }

    [HttpPut("{id:guid}/questions/{qId:guid}")]
    public async Task<IActionResult> UpdateQuestion(Guid id, Guid qId, [FromBody] CreateQuestionRequest req)
    {
        var result = await _svc.UpdateQuestionAsync(GetLecturerId(), id, qId, req);
        return result is null
            ? NotFound(ApiResponse<object>.Fail("not_found", "Question not found."))
            : Ok(ApiResponse<LecturerQuestionDto>.Ok(result));
    }

    [HttpDelete("{id:guid}/questions/{qId:guid}")]
    public async Task<IActionResult> DeleteQuestion(Guid id, Guid qId)
    {
        var ok = await _svc.DeleteQuestionAsync(GetLecturerId(), id, qId);
        return ok
            ? Ok(ApiResponse<object>.Ok(new { }))
            : NotFound(ApiResponse<object>.Fail("not_found", "Question not found."));
    }

    // ── Students ───────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/students")]
    public async Task<IActionResult> GetStudents(Guid id)
    {
        var list = await _svc.GetStudentsAsync(GetLecturerId(), id);
        return Ok(ApiResponse<List<ExamStudentDto>>.Ok(list));
    }

    [HttpPost("{id:guid}/students")]
    public async Task<IActionResult> AssignStudents(Guid id, [FromBody] AssignStudentsRequest req)
    {
        try
        {
            var count = await _svc.AssignStudentsAsync(GetLecturerId(), id, req);
            return Ok(ApiResponse<object>.Ok(new { assigned = count }));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail("not_found", "Exam not found."));
        }
    }

    [HttpDelete("{id:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> RemoveStudent(Guid id, Guid studentId)
    {
        var ok = await _svc.RemoveStudentAsync(GetLecturerId(), id, studentId);
        return ok
            ? Ok(ApiResponse<object>.Ok(new { }))
            : NotFound(ApiResponse<object>.Fail("not_found", "Student not found."));
    }

    // ── Submissions & Grading ──────────────────────────────────────────────

    [HttpGet("{id:guid}/submissions")]
    public async Task<IActionResult> GetSubmissions(Guid id)
    {
        var list = await _svc.GetSubmissionsAsync(GetLecturerId(), id);
        return Ok(ApiResponse<List<SubmissionListItemDto>>.Ok(list));
    }

    // ── Release Results ───────────────────────────────────────────────────

    [HttpPost("{examId:guid}/release-results")]
    public async Task<IActionResult> ReleaseResults(Guid examId)
    {
        try
        {
            var ok = await _svc.ReleaseResultsAsync(GetLecturerId(), examId);
            return ok
                ? Ok(ApiResponse<string>.Ok("Results released to students."))
                : NotFound(ApiResponse<string>.Fail("not_found", "Exam not found."));
        }
        catch (InvalidOperationException ex) when (ex.Message == Constants.ErrorCodes.UngradedSubmissionsExist)
        {
            return BadRequest(ApiResponse<string>.Fail(
                Constants.ErrorCodes.UngradedSubmissionsExist,
                "Some submissions are not graded yet. Grade all submissions before releasing results."));
        }
    }
}

[ApiController]
[Route("api/v1/lecturer/submissions")]
[Authorize(Roles = Constants.Roles.Lecturer)]
public class LecturerSubmissionsController : ControllerBase
{
    private readonly ILecturerExamService _svc;
    private readonly IAiGradingService _ai;

    public LecturerSubmissionsController(ILecturerExamService svc, IAiGradingService ai)
    {
        _svc = svc;
        _ai = ai;
    }

    private Guid GetLecturerId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("{subId:guid}")]
    public async Task<IActionResult> GetDetail(Guid subId)
    {
        var detail = await _svc.GetSubmissionAsync(GetLecturerId(), subId);
        return detail is null
            ? NotFound(ApiResponse<object>.Fail("not_found", "Submission not found."))
            : Ok(ApiResponse<SubmissionDetailDto>.Ok(detail));
    }

    [HttpPost("{subId:guid}/grade")]
    public async Task<IActionResult> Grade(Guid subId, [FromBody] GradeSubmissionRequest req)
    {
        var ok = await _svc.GradeSubmissionAsync(GetLecturerId(), subId, req);
        return ok
            ? Ok(ApiResponse<object>.Ok(new { }))
            : NotFound(ApiResponse<object>.Fail("not_found", "Submission not found."));
    }

    // POST /api/v1/lecturer/submissions/{subId}/ai-suggest
    // Returns an AI-suggested score + feedback for the lecturer to review. Does
    // not persist anything — the lecturer saves via the normal grade endpoint.
    [HttpPost("{subId:guid}/ai-suggest")]
    public async Task<IActionResult> AiSuggest(Guid subId)
    {
        var detail = await _svc.GetSubmissionAsync(GetLecturerId(), subId);
        if (detail is null)
            return NotFound(ApiResponse<object>.Fail("not_found", "Submission not found."));

        try
        {
            var result = await _ai.SuggestGradeAsync(new AiGradeRequest(
                detail.QuestionText, detail.MarkingGuide, detail.MaxMarks,
                detail.Language, detail.Code));

            return Ok(ApiResponse<AiGradeSuggestionDto>.Ok(new AiGradeSuggestionDto
            {
                Score = result.Score,
                MaxScore = detail.MaxMarks,
                Feedback = result.Feedback
            }));
        }
        catch (AiNotConfiguredException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("ai_not_configured", ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(502, ApiResponse<object>.Fail("ai_error",
                $"AI grading failed: {ex.Message}"));
        }
    }
}

/// <summary>Lecturer-accessible student listing for exam assignment.</summary>
[ApiController]
[Route("api/v1/lecturer/students")]
[Authorize(Roles = Constants.Roles.Lecturer)]
public class LecturerStudentsController : ControllerBase
{
    private readonly ScoreCode.Database.ScoreCodeDb _db;
    public LecturerStudentsController(ScoreCode.Database.ScoreCodeDb db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetStudents(
        [FromQuery] string? programme,
        [FromQuery] string? level)
    {
        var query = _db.Users
            .Where(u => u.Role == Constants.Roles.Student && u.IsActive);

        if (!string.IsNullOrWhiteSpace(programme))
            query = query.Where(u => u.ProgrammeOfStudy == programme);
        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(u => u.Level == level);

        var students = await query
            .OrderBy(u => u.IdNumber)
            .Select(u => new ScoreCode.Shared.DTOs.Users.UserDto
            {
                Id = u.Id,
                Name = u.Name,
                IdNumber = u.IdNumber,
                Role = u.Role,
                Department = u.Department,
                ProgrammeOfStudy = u.ProgrammeOfStudy,
                Level = u.Level,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ScoreCode.Shared.DTOs.Users.UserDto>>.Ok(students));
    }
}
