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
[Authorize(Roles = Constants.Roles.Admin)]
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
                ViolationCount = _db.ActivityLogs.Count(a => a.SessionId == s.Id)
            })
            .ToListAsync();

        var recentViolations = await _db.ActivityLogs
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
            TotalViolations = await _db.ActivityLogs.CountAsync(),
            CompletedTodayCount = completedToday,
            ActiveSessions = activeSessions,
            RecentViolations = recentViolations
        };

        return Ok(ApiResponse<MonitoringOverviewDto>.Ok(dto));
    }
}
