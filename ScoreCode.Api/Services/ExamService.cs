using Microsoft.EntityFrameworkCore;
using ScoreCode.Database;
using ScoreCode.Database.Entities;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs.Exams;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScoreCode.Api.Services;

public class ExamService : IExamService
{
    private readonly ScoreCodeDb _db;

    public ExamService(ScoreCodeDb db)
    {
        _db = db;
    }

    // ── List exams for student ─────────────────────────────────────────────
    public async Task<List<ExamListItemDto>> GetStudentExamsAsync(Guid studentId)
    {
        var exams = await _db.ExamStudents
            .Where(es => es.StudentId == studentId)
            .Include(es => es.Exam)
                .ThenInclude(e => e.Questions)
            .Select(es => new ExamListItemDto
            {
                Id = es.Exam.Id,
                Title = es.Exam.Title,
                CourseCode = es.Exam.CourseCode,
                ScheduledAt = es.Exam.ScheduledAt,
                DurationMinutes = es.Exam.DurationMinutes,
                Status = es.Exam.Status,
                QuestionCount = es.Exam.Questions.Count,
                Languages = ParseLanguages(es.Exam.Languages),
                SessionStatus = _db.Sessions
                    .Where(s => s.ExamId == es.ExamId && s.StudentId == studentId)
                    .OrderByDescending(s => s.StartedAt)
                    .Select(s => s.Status)
                    .FirstOrDefault()
            })
            .OrderByDescending(e => e.ScheduledAt)
            .ToListAsync();

        return exams;
    }

    // ── Start exam session ─────────────────────────────────────────────────
    public async Task<ExamStartResponse> StartExamAsync(Guid examId, Guid studentId, string deviceFingerprint)
    {
        var exam = await _db.Exams
            .Include(e => e.Questions.OrderBy(q => q.OrderIndex))
            .FirstOrDefaultAsync(e => e.Id == examId)
            ?? throw new InvalidOperationException("exam_not_found");

        // Verify student is assigned
        var isAssigned = await _db.ExamStudents
            .AnyAsync(es => es.ExamId == examId && es.StudentId == studentId);
        if (!isAssigned)
            throw new InvalidOperationException("not_assigned");

        // Verify exam is live
        if (exam.Status != Constants.ExamStatuses.Active)
            throw new InvalidOperationException("exam_not_available");

        // Check for existing active session
        var existingSession = await _db.Sessions
            .FirstOrDefaultAsync(s => s.ExamId == examId
                                   && s.StudentId == studentId
                                   && s.Status == Constants.SessionStatuses.Active);

        if (existingSession != null)
        {
            // Resume existing session
            return BuildStartResponse(exam, existingSession);
        }

        // Check if already completed
        var completedSession = await _db.Sessions
            .AnyAsync(s => s.ExamId == examId
                        && s.StudentId == studentId
                        && s.Status == Constants.SessionStatuses.Completed);
        if (completedSession)
            throw new InvalidOperationException("exam_already_completed");

        // ── Time gate (new sessions only) ──────────────────────────────────
        // A student may only *begin* an exam within
        //   [ScheduledAt, ScheduledAt + StartGraceMinutes].
        // Before that the exam hasn't opened; after it the start window has
        // closed. An already-active session (handled above) can always resume,
        // and a student keeps the full duration once started.
        var now = DateTime.UtcNow;
        if (now < exam.ScheduledAt)
            throw new InvalidOperationException(Constants.ErrorCodes.ExamNotOpen);

        var startDeadline = exam.ScheduledAt.AddMinutes(Constants.Exam.StartGraceMinutes);
        if (now > startDeadline)
            throw new InvalidOperationException(Constants.ErrorCodes.ExamWindowClosed);

        // Create new session
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var session = new Session
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ExamId = examId,
            DeviceFingerprint = deviceFingerprint,
            Nonce = nonce,
            StartedAt = now,
            Status = Constants.SessionStatuses.Active,
            LastHeartbeatAt = now
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        return BuildStartResponse(exam, session);
    }

    // ── Submit exam ────────────────────────────────────────────────────────
    public async Task SubmitExamAsync(Guid examId, Guid studentId, SubmitExamRequest request)
    {
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId
                                   && s.ExamId == examId
                                   && s.StudentId == studentId
                                   && s.Status == Constants.SessionStatuses.Active)
            ?? throw new InvalidOperationException("session_not_found");

        // Validate each answer's hash
        foreach (var answer in request.Answers)
        {
            var expectedHash = ComputeHash(
                session.Nonce, studentId, answer.QuestionId,
                answer.FinalizedAt, answer.Code);

            if (answer.Hash != expectedHash)
                throw new InvalidOperationException("hash_mismatch");

            // Upsert submission
            var existing = await _db.Submissions
                .FirstOrDefaultAsync(s => s.StudentId == studentId
                                       && s.QuestionId == answer.QuestionId);

            if (existing != null)
            {
                existing.Code = answer.Code;
                existing.Hash = answer.Hash;
                existing.FinalizedAt = answer.FinalizedAt;
                existing.UploadStatus = "Uploaded";
            }
            else
            {
                _db.Submissions.Add(new Submission
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    StudentId = studentId,
                    ExamId = examId,
                    QuestionId = answer.QuestionId,
                    Code = answer.Code,
                    SubmittedAt = DateTime.UtcNow,
                    FinalizedAt = answer.FinalizedAt,
                    Hash = answer.Hash,
                    UploadStatus = "Uploaded"
                });
            }
        }

        // Close session
        session.Status = Constants.SessionStatuses.Completed;
        session.EndedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static ExamStartResponse BuildStartResponse(Exam exam, Session session)
    {
        // Calculate extra time for the student
        return new ExamStartResponse
        {
            ExamId = exam.Id,
            SessionId = session.Id,
            Nonce = session.Nonce,
            Title = exam.Title,
            CourseCode = exam.CourseCode,
            DurationMinutes = exam.DurationMinutes,
            StartedAt = session.StartedAt,
            EndsAt = session.StartedAt.AddMinutes(exam.DurationMinutes),
            IntelliSenseEnabled = exam.IntelliSenseEnabled,
            Questions = exam.Questions.Select(q => new QuestionDto
            {
                Id = q.Id,
                OrderIndex = q.OrderIndex,
                Text = q.Text,
                Marks = q.Marks,
                Language = q.Language
            }).ToList()
        };
    }

    /// <summary>
    /// SHA256( nonce | studentId | questionId | finalizedAt | code )
    /// </summary>
    internal static string ComputeHash(
        string nonce, Guid studentId, Guid questionId,
        DateTime finalizedAt, string code)
    {
        var input = $"{nonce}|{studentId}|{questionId}|{finalizedAt:O}|{code}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    private static List<string> ParseLanguages(string languagesJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(languagesJson) ?? [];
        }
        catch
        {
            return string.IsNullOrWhiteSpace(languagesJson) ? [] : [languagesJson];
        }
    }

    // ── Student Results ────────────────────────────────────────────────────
    public async Task<List<ExamResultDto>> GetStudentResultsAsync(Guid studentId)
    {
        // Get all exams where: student has a completed session AND results are released
        var results = await _db.Sessions
            .Where(s => s.StudentId == studentId && s.Status == Constants.SessionStatuses.Completed)
            .Join(_db.Exams.Where(e => e.ResultsReleased),
                  s => s.ExamId, e => e.Id,
                  (s, e) => new { Session = s, Exam = e })
            .OrderByDescending(x => x.Session.EndedAt)
            .ToListAsync();

        var output = new List<ExamResultDto>();

        foreach (var r in results)
        {
            // Build the result from ALL exam questions (not just submitted ones) so
            // a skipped question counts as 0 out of its marks — otherwise the total
            // max score shrinks and skipping isn't reflected in the percentage.
            var questions = await _db.Questions
                .Where(q => q.ExamId == r.Exam.Id)
                .OrderBy(q => q.OrderIndex)
                .ToListAsync();

            var submissions = await _db.Submissions
                .Where(s => s.StudentId == studentId && s.ExamId == r.Exam.Id)
                .Include(s => s.Grade)
                .ToListAsync();
            var submissionByQuestion = submissions
                .GroupBy(s => s.QuestionId)
                .ToDictionary(g => g.Key, g => g.First());

            var questionResults = questions.Select(q =>
            {
                submissionByQuestion.TryGetValue(q.Id, out var s);
                return new QuestionResultDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.Text,
                    Language = q.Language,
                    Code = s?.Code ?? string.Empty,
                    Score = s?.Grade?.Score ?? 0,
                    MaxScore = q.Marks,   // always the question's full marks
                    Feedback = s?.Grade?.FeedbackJson
                };
            }).ToList();

            output.Add(new ExamResultDto
            {
                ExamId = r.Exam.Id,
                ExamTitle = r.Exam.Title,
                CourseCode = r.Exam.CourseCode,
                SubmittedAt = r.Session.EndedAt ?? r.Session.StartedAt,
                ResultsReleasedAt = r.Exam.ResultsReleasedAt ?? DateTime.UtcNow,
                Questions = questionResults,
                TotalScore = questionResults.Sum(q => q.Score),
                TotalMaxScore = questionResults.Sum(q => q.MaxScore)
            });
        }

        return output;
    }

    // ── Anti-cheat violation logging ───────────────────────────────────────
    public async Task LogViolationAsync(Guid examId, Guid studentId, LogViolationRequest request)
    {
        // The session must belong to this student & exam. We don't require it to
        // still be Active — a late-arriving report during submission is still worth
        // recording — but we do bind it to a real session the caller owns.
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId
                                   && s.ExamId == examId
                                   && s.StudentId == studentId)
            ?? throw new InvalidOperationException(Constants.ErrorCodes.SessionNotFound);

        var eventType = string.IsNullOrWhiteSpace(request.EventType)
            ? "unknown" : request.EventType.Trim();
        if (eventType.Length > 64) eventType = eventType[..64];

        var detail = (request.Detail ?? string.Empty).Trim();
        if (detail.Length > 500) detail = detail[..500];

        _db.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            EventType = eventType,
            Detail = detail,
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
