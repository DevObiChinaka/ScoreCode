using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScoreCode.Database;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs;
using ScoreCode.Shared.DTOs.Monitoring;

namespace ScoreCode.Api.Controllers;

[ApiController]
[Route("api/v1/monitoring")]
[Authorize(Roles = Constants.Roles.AnyAdmin)]
public class MonitoringController : ControllerBase
{
    private readonly ScoreCodeDb _db;

    public MonitoringController(ScoreCodeDb db) => _db = db;

    // GET /api/v1/monitoring/overview
    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
    {
        var activeSessions = await _db.Sessions
            .Where(s => s.Status == Constants.SessionStatuses.Active)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new ActiveSessionDto
            {
                SessionId = s.Id,
                StudentMatric = s.Student.IdNumber,
                ExamTitle = s.Exam.Title,
                CourseCode = s.Exam.CourseCode,
                StartedAt = s.StartedAt,
                ViolationCount = _db.ActivityLogs.Count(a => a.SessionId == s.Id
                                    && Constants.ActivityEvents.Violations.Contains(a.EventType))
            })
            .ToListAsync();

        var recentViolations = await _db.ActivityLogs
            .Where(a => Constants.ActivityEvents.Violations.Contains(a.EventType))
            .OrderByDescending(a => a.OccurredAt)
            .Take(50)
            .Select(a => new ViolationEventDto
            {
                StudentMatric = a.Session.Student.IdNumber,
                ExamTitle = a.Session.Exam.Title,
                EventType = a.EventType,
                Detail = a.Detail,
                OccurredAt = a.OccurredAt
            })
            .ToListAsync();

        var startOfDayUtc = DateTime.UtcNow.Date;
        var completedToday = await _db.Sessions
            .CountAsync(s => s.Status == Constants.SessionStatuses.Completed
                          && s.EndedAt >= startOfDayUtc);

        var dto = new MonitoringOverviewDto
        {
            ActiveSessionCount = activeSessions.Count,
            TotalViolations = await _db.ActivityLogs
                .CountAsync(a => Constants.ActivityEvents.Violations.Contains(a.EventType)),
            CompletedTodayCount = completedToday,
            ActiveSessions = activeSessions,
            RecentViolations = recentViolations
        };

        return Ok(ApiResponse<MonitoringOverviewDto>.Ok(dto));
    }

    // GET /api/v1/monitoring/activity?examId=&eventType=&violationsOnly=&page=&pageSize=
    // The exam integrity log. Tenant filters on ActivityLog scope through
    // Session.InstitutionId, so an institution admin sees only their own rows
    // and a platform admin (null tenant) sees every institution's.
    [HttpGet("activity")]
    public async Task<IActionResult> Activity(
        Guid? examId = null,
        string? eventType = null,
        bool violationsOnly = false,
        int page = 1,
        int pageSize = 25)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.ActivityLogs.AsQueryable();

        if (examId is not null && examId != Guid.Empty)
            q = q.Where(a => a.Session.ExamId == examId);

        if (!string.IsNullOrWhiteSpace(eventType))
            q = q.Where(a => a.EventType == eventType);
        else if (violationsOnly)
            q = q.Where(a => Constants.ActivityEvents.Violations.Contains(a.EventType));

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityEntryDto
            {
                ExamId = a.Session.ExamId,
                ExamTitle = a.Session.Exam.Title,
                CourseCode = a.Session.Exam.CourseCode,
                StudentMatric = a.Session.Student.IdNumber,
                InstitutionName = a.Session.Institution != null ? a.Session.Institution.Name : "",
                EventType = a.EventType,
                Detail = a.Detail,
                OccurredAt = a.OccurredAt
            })
            .ToListAsync();

        // Totals describe the whole filtered set, not just this page.
        var violationQuery = q.Where(a => Constants.ActivityEvents.Violations.Contains(a.EventType));

        var dto = new ActivityLogDto
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
            ViolationCount = await violationQuery.CountAsync(),
            FlaggedStudentCount = await violationQuery.Select(a => a.Session.StudentId).Distinct().CountAsync(),
            Exams = await _db.ActivityLogs
                .Select(a => new ActivityExamDto
                {
                    Id = a.Session.ExamId,
                    Title = a.Session.Exam.Title,
                    CourseCode = a.Session.Exam.CourseCode
                })
                .Distinct()
                .OrderBy(e => e.Title)
                .ToListAsync()
        };

        return Ok(ApiResponse<ActivityLogDto>.Ok(dto));
    }
}
