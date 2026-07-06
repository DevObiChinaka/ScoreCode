using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoreCode.Api.Services;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs;
using ScoreCode.Shared.DTOs.Exams;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ScoreCode.Api.Controllers;

[ApiController]
[Route("api/v1/exams")]
[Authorize]
public class ExamsController : ControllerBase
{
    private readonly IExamService _exams;

    public ExamsController(IExamService exams)
    {
        _exams = exams;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException());

    // GET /api/v1/exams/student — List exams assigned to the authenticated student
    [HttpGet("student")]
    public async Task<IActionResult> GetStudentExams()
    {
        var studentId = GetUserId();
        var exams = await _exams.GetStudentExamsAsync(studentId);
        return Ok(ApiResponse<List<ExamListItemDto>>.Ok(exams));
    }

    // POST /api/v1/exams/{id}/start — Start an exam session
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> StartExam(Guid id)
    {
        var studentId = GetUserId();

        try
        {
            var result = await _exams.StartExamAsync(id, studentId, GetDeviceFingerprint());
            return Ok(ApiResponse<ExamStartResponse>.Ok(result));
        }
        catch (InvalidOperationException ex) when (ex.Message == "exam_not_found")
        {
            return NotFound(ApiResponse<object>.Fail("exam_not_found", "Exam does not exist."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "not_assigned")
        {
            return StatusCode(403, ApiResponse<object>.Fail("not_assigned", "You are not assigned to this exam."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "exam_not_available")
        {
            return BadRequest(ApiResponse<object>.Fail("exam_not_available", "This exam is not currently available."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "exam_already_completed")
        {
            return BadRequest(ApiResponse<object>.Fail("exam_already_completed", "You have already completed this exam."));
        }
        catch (InvalidOperationException ex) when (ex.Message == Constants.ErrorCodes.ExamNotOpen)
        {
            return BadRequest(ApiResponse<object>.Fail(Constants.ErrorCodes.ExamNotOpen,
                "This exam has not opened yet. Please wait until the scheduled start time."));
        }
        catch (InvalidOperationException ex) when (ex.Message == Constants.ErrorCodes.ExamWindowClosed)
        {
            return BadRequest(ApiResponse<object>.Fail(Constants.ErrorCodes.ExamWindowClosed,
                "The window to start this exam has closed. Contact your lecturer."));
        }
    }

    // POST /api/v1/exams/{id}/violation — record an anti-cheat / integrity event
    [HttpPost("{id:guid}/violation")]
    public async Task<IActionResult> LogViolation(Guid id, [FromBody] LogViolationRequest request)
    {
        var studentId = GetUserId();
        try
        {
            await _exams.LogViolationAsync(id, studentId, request);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (InvalidOperationException ex) when (ex.Message == Constants.ErrorCodes.SessionNotFound)
        {
            return NotFound(ApiResponse<object>.Fail(Constants.ErrorCodes.SessionNotFound,
                "No session found for this exam."));
        }
    }

    // POST /api/v1/exams/{id}/submit — Submit all answers
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitExam(Guid id, [FromBody] SubmitExamRequest request)
    {
        var studentId = GetUserId();

        try
        {
            await _exams.SubmitExamAsync(id, studentId, request);
            return Ok(ApiResponse<object>.Ok(new { message = "Exam submitted successfully." }));
        }
        catch (InvalidOperationException ex) when (ex.Message == "session_not_found")
        {
            return NotFound(ApiResponse<object>.Fail("session_not_found",
                "No active session found for this exam."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "hash_mismatch")
        {
            return BadRequest(ApiResponse<object>.Fail("hash_mismatch",
                "Submission integrity check failed. One or more answers have invalid hashes."));
        }
    }

    // GET /api/v1/exams/results — Get graded results for student (only released)
    [HttpGet("results")]
    public async Task<IActionResult> GetResults()
    {
        var studentId = GetUserId();
        var results = await _exams.GetStudentResultsAsync(studentId);
        return Ok(ApiResponse<List<ExamResultDto>>.Ok(results));
    }

    private string GetDeviceFingerprint() =>
        Request.Headers.UserAgent.ToString();
}
