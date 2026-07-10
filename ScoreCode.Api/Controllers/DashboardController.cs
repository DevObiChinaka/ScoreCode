using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScoreCode.Database;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ScoreCode.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ScoreCodeDb _db;

    public DashboardController(ScoreCodeDb db) => _db = db;

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException());

    // GET /api/v1/dashboard/student
    [HttpGet("student")]
    [Authorize(Roles = Constants.Roles.Student)]
    public async Task<IActionResult> GetStudentDashboard()
    {
        var studentId = GetUserId();

        // All submissions for this student (load into memory for aggregation)
        var submissions = await _db.Submissions
            .Where(s => s.StudentId == studentId)
            .Include(s => s.Grade)
            .Include(s => s.Question).ThenInclude(q => q.Exam)
            .ToListAsync();

        // Count exams (distinct) the student has submitted to
        var examsTaken = submissions.Select(s => s.ExamId).Distinct().Count();

        // Average score across released, fully-graded exams (per-exam aggregate).
        // Gated on release so a score is never surfaced before the lecturer
        // releases it — keeping the summary coherent with the results below.
        var examGroups = submissions
            .GroupBy(s => s.ExamId)
            .Where(g => g.All(s => s.Grade != null)
                     && g.First().Question?.Exam?.ResultsReleased == true)
            .ToList();

        double avgScore = -1; // -1 indicates "no data" so client shows "—"
        if (examGroups.Count > 0)
        {
            var examPercentages = examGroups.Select(g =>
            {
                var score = g.Sum(s => s.Grade!.Score);
                var max = g.Sum(s => s.Question?.Marks ?? 0);
                return max > 0 ? (double)score / max * 100 : 0;
            }).ToList();
            avgScore = Math.Round(examPercentages.Average(), 1);
        }

        // Available / upcoming exams: any assigned Active exam the student hasn't
        // completed yet — including ones already open (ScheduledAt in the past).
        // Currently-available exams are surfaced first, then future ones.
        var completedExamIds = submissions.Select(s => s.ExamId).Distinct().ToHashSet();
        var now = DateTime.UtcNow;

        var assignedActive = await _db.ExamStudents
            .Where(es => es.StudentId == studentId)
            .Select(es => es.Exam)
            .Where(e => e.Status == Constants.ExamStatuses.Active)
            .Select(e => new UpcomingExamDto
            {
                Id = e.Id,
                Title = e.Title,
                CourseCode = e.CourseCode,
                ScheduledAt = e.ScheduledAt,
                DurationMinutes = e.DurationMinutes
            })
            .ToListAsync();

        var upcoming = assignedActive
            .Where(e => !completedExamIds.Contains(e.Id))
            .OrderBy(e => e.ScheduledAt <= now ? 0 : 1) // available now first
            .ThenBy(e => e.ScheduledAt)
            .Take(5)
            .ToList();

        // Recent results (released exams, aggregated per exam — client-side)
        var recentResults = submissions
            .Where(s => s.Question?.Exam?.ResultsReleased == true)
            .GroupBy(s => s.ExamId)
            .Select(g =>
            {
                var exam = g.First().Question!.Exam;
                return new RecentResultDto
                {
                    ExamTitle = exam.Title,
                    CourseCode = exam.CourseCode,
                    Score = g.Sum(s => s.Grade?.Score ?? 0),
                    MaxScore = g.Sum(s => s.Question?.Marks ?? 0),
                    SubmittedAt = g.Max(s => s.SubmittedAt)
                };
            })
            .OrderByDescending(r => r.SubmittedAt)
            .Take(5)
            .ToList();

        var dto = new StudentDashboardDto
        {
            ExamsTaken = examsTaken,
            AverageScore = avgScore,
            TotalSubmissions = examsTaken, // per-exam count
            NextExamTitle = upcoming.FirstOrDefault()?.Title,
            NextExamDate = upcoming.FirstOrDefault()?.ScheduledAt,
            UpcomingExams = upcoming,
            RecentResults = recentResults
        };

        return Ok(ApiResponse<StudentDashboardDto>.Ok(dto));
    }

    // GET /api/v1/dashboard/lecturer
    [HttpGet("lecturer")]
    [Authorize(Roles = Constants.Roles.Lecturer)]
    public async Task<IActionResult> GetLecturerDashboard()
    {
        var lecturerId = GetUserId();

        var exams = await _db.Exams
            .Where(e => e.LecturerId == lecturerId)
            .ToListAsync();

        var examIdList = exams.Select(e => e.Id).ToList();
        var activeExams = exams.Count(e => e.Status == Constants.ExamStatuses.Active);
        var totalExams = exams.Count;

        // Total unique students
        var totalStudents = await _db.ExamStudents
            .Where(es => examIdList.Contains(es.ExamId))
            .Select(es => es.StudentId)
            .Distinct()
            .CountAsync();

        // Recent 3 exams with per-exam ungraded count
        var recentExamEntities = exams.OrderByDescending(e => e.CreatedAt).Take(3).ToList();
        var recentExams = new List<ExamSummaryDto>();
        var totalUngraded = 0;

        foreach (var exam in recentExamEntities)
        {
            var studentCount = await _db.ExamStudents.CountAsync(es => es.ExamId == exam.Id);
            var examUngraded = await _db.Submissions
                .Where(s => s.ExamId == exam.Id && s.Grade == null)
                .CountAsync();

            totalUngraded += examUngraded;

            recentExams.Add(new ExamSummaryDto
            {
                Id = exam.Id,
                Title = exam.Title,
                CourseCode = exam.CourseCode,
                Status = exam.Status,
                StudentCount = studentCount,
                UngradedCount = examUngraded,
                CreatedAt = exam.CreatedAt
            });
        }

        // Also count ungraded for exams not in the top 3
        var otherExamIds = examIdList.Except(recentExamEntities.Select(e => e.Id)).ToList();
        if (otherExamIds.Count > 0)
        {
            totalUngraded += await _db.Submissions
                .Where(s => otherExamIds.Contains(s.ExamId) && s.Grade == null)
                .CountAsync();
        }

        var dto = new LecturerDashboardDto
        {
            ActiveExams = activeExams,
            TotalExams = totalExams,
            UngradedSubmissions = totalUngraded,
            TotalStudents = totalStudents,
            RecentExams = recentExams
        };

        return Ok(ApiResponse<LecturerDashboardDto>.Ok(dto));
    }
}
