using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ScoreCode.Database;
using ScoreCode.Shared;

namespace ScoreCode.Api.Services;

/// <summary>
/// Schedules a precise timer for each Draft exam so it auto-publishes
/// exactly when its ScheduledAt time arrives.  When a lecturer updates
/// an exam's schedule, call <see cref="ScheduleExam"/> to refresh the timer.
/// </summary>
public class ExamAutoPublishService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExamAutoPublishService> _logger;
    private readonly ConcurrentDictionary<Guid, Timer> _timers = new();

    public ExamAutoPublishService(IServiceScopeFactory scopeFactory, ILogger<ExamAutoPublishService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ── Public API (called from controllers / services) ───────────────────

    /// <summary>Schedule or re-schedule a timer for an exam.</summary>
    public void ScheduleExam(Guid examId, DateTime scheduledAtUtc)
    {
        CancelExam(examId); // remove old timer if any

        var delay = scheduledAtUtc - DateTime.UtcNow;
        if (delay <= TimeSpan.Zero)
        {
            // Already past due — publish immediately
            _ = PublishExamAsync(examId);
            return;
        }

        _logger.LogInformation("Scheduled auto-publish for exam {ExamId} in {Delay}", examId, delay);

        var timer = new Timer(
            callback: _ => _ = PublishExamAsync(examId),
            state: null,
            dueTime: delay,
            period: Timeout.InfiniteTimeSpan // one-shot
        );

        _timers[examId] = timer;
    }

    /// <summary>Cancel a pending timer (e.g. exam deleted or manually published).</summary>
    public void CancelExam(Guid examId)
    {
        if (_timers.TryRemove(examId, out var timer))
        {
            timer.Dispose();
        }
    }

    // ── Startup: load all pending Draft exams ─────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay to let the app fully start
        await Task.Delay(2000, stoppingToken);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScoreCodeDb>();

        var draftExams = await db.Exams
            .Where(e => e.Status == Constants.ExamStatuses.Draft && e.Questions.Count > 0)
            .Select(e => new { e.Id, e.ScheduledAt })
            .ToListAsync(stoppingToken);

        foreach (var exam in draftExams)
        {
            ScheduleExam(exam.Id, exam.ScheduledAt);
        }

        _logger.LogInformation("ExamAutoPublishService started — {Count} exam(s) scheduled", draftExams.Count);

        // Keep the service alive until shutdown
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    // ── Internal publish logic ────────────────────────────────────────────

    private async Task PublishExamAsync(Guid examId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ScoreCodeDb>();

            var exam = await db.Exams
                .Include(e => e.Questions)
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam is null || exam.Status != Constants.ExamStatuses.Draft || exam.Questions.Count == 0)
            {
                _timers.TryRemove(examId, out _);
                return;
            }

            exam.Status = Constants.ExamStatuses.Active;
            await db.SaveChangesAsync();

            _timers.TryRemove(examId, out _);
            _logger.LogInformation("Auto-published exam {ExamId} ({Title})", exam.Id, exam.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-publish exam {ExamId}", examId);
        }
    }

    public override void Dispose()
    {
        foreach (var timer in _timers.Values)
            timer.Dispose();
        _timers.Clear();
        base.Dispose();
    }
}
