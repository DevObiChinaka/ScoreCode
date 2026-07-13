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

    // GET /api/v1/dashboard/platform
    [HttpGet("platform")]
    [Authorize(Roles = Constants.Roles.PlatformOrAdmin)]
    public async Task<IActionResult> GetPlatformDashboard()
    {
        // Platform admins have no tenant scope, so these aggregate across all institutions.
        var institutions = await _db.Institutions.ToListAsync();

        var userCounts = await _db.Users
            .Where(u => u.InstitutionId != null)
            .GroupBy(u => u.InstitutionId)
            .Select(g => new
            {
                InstitutionId = g.Key,
                Students = g.Count(u => u.Role == Constants.Roles.Student),
                Lecturers = g.Count(u => u.Role == Constants.Roles.Lecturer)
            })
            .ToListAsync();

        var examCounts = await _db.Exams
            .Where(e => e.InstitutionId != null)
            .GroupBy(e => e.InstitutionId)
            .Select(g => new { InstitutionId = g.Key, Count = g.Count() })
            .ToListAsync();

        var top = institutions
            .Select(i =>
            {
                var uc = userCounts.FirstOrDefault(x => x.InstitutionId == i.Id);
                var ec = examCounts.FirstOrDefault(x => x.InstitutionId == i.Id);
                return new TopInstitutionDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    Type = i.Type,
                    Status = i.Status,
                    Students = uc?.Students ?? 0,
                    Lecturers = uc?.Lecturers ?? 0,
                    Exams = ec?.Count ?? 0
                };
            })
            .OrderByDescending(t => t.Students + t.Lecturers)
            .ThenByDescending(t => t.Exams)
            .Take(6)
            .ToList();

        var dto = new PlatformDashboardDto
        {
            TotalInstitutions = institutions.Count,
            PendingInstitutions = institutions.Count(i => i.Status == Constants.InstitutionStatuses.Pending),
            ApprovedInstitutions = institutions.Count(i => i.Status == Constants.InstitutionStatuses.Approved),
            SuspendedInstitutions = institutions.Count(i => i.Status == Constants.InstitutionStatuses.Suspended),
            TotalStudents = await _db.Users.CountAsync(u => u.Role == Constants.Roles.Student),
            TotalLecturers = await _db.Users.CountAsync(u => u.Role == Constants.Roles.Lecturer),
            TotalExams = await _db.Exams.CountAsync(),
            ActiveExams = await _db.Exams.CountAsync(e => e.Status == Constants.ExamStatuses.Active),
            TotalSubmissions = await _db.Submissions.CountAsync(),
            TopInstitutions = top
        };

        return Ok(ApiResponse<PlatformDashboardDto>.Ok(dto));
    }

    // GET /api/v1/dashboard/institution/exams
    // Institution admin oversight: exams created by their lecturers + counts.
    [HttpGet("institution/exams")]
    [Authorize(Roles = Constants.Roles.InstitutionAdmin)]
    public async Task<IActionResult> GetInstitutionExams()
    {
        // Tenant filter scopes every query below to the admin's own institution.
        var exams = await _db.Exams.OrderByDescending(e => e.ScheduledAt).ToListAsync();
        if (exams.Count == 0)
            return Ok(ApiResponse<List<InstitutionExamDto>>.Ok(new()));

        var examIds = exams.Select(e => e.Id).ToList();
        var lecturerIds = exams.Select(e => e.LecturerId).Distinct().ToList();

        var lecturers = await _db.Users
            .Where(u => lecturerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var studentCounts = (await _db.ExamStudents
            .Where(es => examIds.Contains(es.ExamId))
            .Select(es => es.ExamId)
            .ToListAsync())
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var submissions = await _db.Submissions
            .Where(s => examIds.Contains(s.ExamId))
            .Select(s => new { s.ExamId, s.StudentId })
            .ToListAsync();
        var submissionCounts = submissions
            .GroupBy(s => s.ExamId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StudentId).Distinct().Count());

        var dto = exams.Select(e => new InstitutionExamDto
        {
            Id = e.Id,
            Title = e.Title,
            CourseCode = e.CourseCode,
            Status = e.Status,
            LecturerName = lecturers.TryGetValue(e.LecturerId, out var n) && !string.IsNullOrWhiteSpace(n) ? n : "—",
            StudentCount = studentCounts.GetValueOrDefault(e.Id),
            SubmissionCount = submissionCounts.GetValueOrDefault(e.Id),
            ScheduledAt = e.ScheduledAt
        }).ToList();

        return Ok(ApiResponse<List<InstitutionExamDto>>.Ok(dto));
    }
}
