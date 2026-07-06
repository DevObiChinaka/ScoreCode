using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoreCode.Database;
using ScoreCode.Database.Entities;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs.Lecturer;

namespace ScoreCode.Api.Services;

public class LecturerExamService : ILecturerExamService
{
    private readonly ScoreCodeDb _db;
    public LecturerExamService(ScoreCodeDb db) => _db = db;

    // ── Exam CRUD ──────────────────────────────────────────────────────────

    public async Task<LecturerExamDto> CreateExamAsync(Guid lecturerId, CreateExamRequest req)
    {
        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            LecturerId = lecturerId,
            Title = req.Title,
            CourseCode = req.CourseCode,
            ScheduledAt = req.ScheduledAt.ToUniversalTime(),
            DurationMinutes = req.DurationMinutes,
            Languages = JsonSerializer.Serialize(req.Languages),
            IntelliSenseEnabled = req.IntelliSenseEnabled,
            GradingMode = req.GradingMode,
            Status = Constants.ExamStatuses.Draft,
            CreatedAt = DateTime.UtcNow
        };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();
        return MapToDto(exam, 0, 0, 0);
    }

    public async Task<List<LecturerExamDto>> GetExamsAsync(Guid lecturerId)
    {
        return await _db.Exams
            .Where(e => e.LecturerId == lecturerId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new LecturerExamDto
            {
                Id = e.Id,
                Title = e.Title,
                CourseCode = e.CourseCode,
                ScheduledAt = e.ScheduledAt,
                DurationMinutes = e.DurationMinutes,
                Languages = DeserializeLanguages(e.Languages),
                IntelliSenseEnabled = e.IntelliSenseEnabled,
                GradingMode = e.GradingMode,
                Status = e.Status,
                QuestionCount = e.Questions.Count,
                StudentCount = e.ExamStudents.Count,
                TotalMarks = e.Questions.Sum(q => q.Marks),
                SubmissionCount = _db.Submissions.Count(s => s.ExamId == e.Id),
                UngradedCount = _db.Submissions.Count(s => s.ExamId == e.Id && s.Grade == null),
                CreatedAt = e.CreatedAt,
                ResultsReleased = e.ResultsReleased
            })
            .ToListAsync();
    }

    public async Task<LecturerExamDto?> GetExamAsync(Guid lecturerId, Guid examId)
    {
        return await _db.Exams
            .Where(e => e.Id == examId && e.LecturerId == lecturerId)
            .Select(e => new LecturerExamDto
            {
                Id = e.Id,
                Title = e.Title,
                CourseCode = e.CourseCode,
                ScheduledAt = e.ScheduledAt,
                DurationMinutes = e.DurationMinutes,
                Languages = DeserializeLanguages(e.Languages),
                IntelliSenseEnabled = e.IntelliSenseEnabled,
                GradingMode = e.GradingMode,
                Status = e.Status,
                QuestionCount = e.Questions.Count,
                StudentCount = e.ExamStudents.Count,
                TotalMarks = e.Questions.Sum(q => q.Marks),
                SubmissionCount = _db.Submissions.Count(s => s.ExamId == e.Id),
                UngradedCount = _db.Submissions.Count(s => s.ExamId == e.Id && s.Grade == null),
                CreatedAt = e.CreatedAt,
                ResultsReleased = e.ResultsReleased
            })
            .FirstOrDefaultAsync();
    }

    public async Task<LecturerExamDto?> UpdateExamAsync(Guid lecturerId, Guid examId, UpdateExamRequest req)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) return null;
        if (exam.Status != Constants.ExamStatuses.Draft) throw new InvalidOperationException("Only draft exams can be edited.");

        if (req.Title is not null) exam.Title = req.Title;
        if (req.CourseCode is not null) exam.CourseCode = req.CourseCode;
        if (req.ScheduledAt.HasValue) exam.ScheduledAt = req.ScheduledAt.Value.ToUniversalTime();
        if (req.DurationMinutes.HasValue) exam.DurationMinutes = req.DurationMinutes.Value;
        if (req.Languages is not null) exam.Languages = JsonSerializer.Serialize(req.Languages);
        if (req.IntelliSenseEnabled.HasValue) exam.IntelliSenseEnabled = req.IntelliSenseEnabled.Value;
        if (req.GradingMode is not null) exam.GradingMode = req.GradingMode;

        await _db.SaveChangesAsync();
        return await GetExamAsync(lecturerId, examId);
    }

    public async Task<bool> DeleteExamAsync(Guid lecturerId, Guid examId)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) return false;
        if (exam.Status != Constants.ExamStatuses.Draft) throw new InvalidOperationException("Only draft exams can be deleted.");
        _db.Exams.Remove(exam);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PublishExamAsync(Guid lecturerId, Guid examId)
    {
        var exam = await _db.Exams
            .Include(e => e.Questions)
            .FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) return false;
        if (exam.Status != Constants.ExamStatuses.Draft) throw new InvalidOperationException("Only draft exams can be published.");
        if (exam.Questions.Count == 0) throw new InvalidOperationException("Exam must have at least one question.");

        exam.Status = Constants.ExamStatuses.Active;
        exam.PublishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CloseExamAsync(Guid lecturerId, Guid examId)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) return false;
        if (exam.Status != Constants.ExamStatuses.Active) throw new InvalidOperationException("Only active exams can be closed.");

        exam.Status = Constants.ExamStatuses.Closed;
        exam.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReleaseResultsAsync(Guid lecturerId, Guid examId)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) return false;
        if (exam.ResultsReleased) return true; // Already released — idempotent

        // Guard: every submission must be graded before release. Otherwise an
        // ungraded answer would surface to the student as a real 0.
        var ungraded = await _db.Submissions
            .CountAsync(s => s.ExamId == examId && s.Grade == null);
        if (ungraded > 0)
            throw new InvalidOperationException(Constants.ErrorCodes.UngradedSubmissionsExist);

        exam.ResultsReleased = true;
        exam.ResultsReleasedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Questions ──────────────────────────────────────────────────────────

    public async Task<LecturerQuestionDto> AddQuestionAsync(Guid lecturerId, Guid examId, CreateQuestionRequest req)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) throw new KeyNotFoundException("Exam not found.");
        if (exam.Status != Constants.ExamStatuses.Draft) throw new InvalidOperationException("Questions can only be added to draft exams.");

        var question = new Question
        {
            Id = Guid.NewGuid(),
            ExamId = examId,
            Text = req.Text,
            MarkingGuide = req.MarkingGuide,
            Marks = req.Marks,
            Language = req.Language,
            OrderIndex = req.OrderIndex
        };
        _db.Questions.Add(question);
        await _db.SaveChangesAsync();

        return new LecturerQuestionDto
        {
            Id = question.Id, OrderIndex = question.OrderIndex,
            Text = question.Text, MarkingGuide = question.MarkingGuide,
            Marks = question.Marks, Language = question.Language
        };
    }

    public async Task<LecturerQuestionDto?> UpdateQuestionAsync(Guid lecturerId, Guid examId, Guid questionId, CreateQuestionRequest req)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) return null;

        var q = await _db.Questions.FirstOrDefaultAsync(x => x.Id == questionId && x.ExamId == examId);
        if (q is null) return null;

        q.Text = req.Text;
        q.MarkingGuide = req.MarkingGuide;
        q.Marks = req.Marks;
        q.Language = req.Language;
        q.OrderIndex = req.OrderIndex;
        await _db.SaveChangesAsync();

        return new LecturerQuestionDto
        {
            Id = q.Id, OrderIndex = q.OrderIndex,
            Text = q.Text, MarkingGuide = q.MarkingGuide,
            Marks = q.Marks, Language = q.Language
        };
    }

    public async Task<bool> DeleteQuestionAsync(Guid lecturerId, Guid examId, Guid questionId)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) return false;

        var q = await _db.Questions.FirstOrDefaultAsync(x => x.Id == questionId && x.ExamId == examId);
        if (q is null) return false;

        _db.Questions.Remove(q);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<LecturerQuestionDto>> GetQuestionsAsync(Guid lecturerId, Guid examId)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) return [];

        return await _db.Questions
            .Where(q => q.ExamId == examId)
            .OrderBy(q => q.OrderIndex)
            .Select(q => new LecturerQuestionDto
            {
                Id = q.Id, OrderIndex = q.OrderIndex,
                Text = q.Text, MarkingGuide = q.MarkingGuide,
                Marks = q.Marks, Language = q.Language
            })
            .ToListAsync();
    }

    // ── Students ───────────────────────────────────────────────────────────

    public async Task<int> AssignStudentsAsync(Guid lecturerId, Guid examId, AssignStudentsRequest req)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) throw new KeyNotFoundException("Exam not found.");

        var existing = await _db.ExamStudents
            .Where(es => es.ExamId == examId)
            .Select(es => es.StudentId)
            .ToListAsync();

        var newIds = req.StudentIds.Except(existing).ToList();
        foreach (var sid in newIds)
        {
            _db.ExamStudents.Add(new ExamStudent { ExamId = examId, StudentId = sid });
        }
        await _db.SaveChangesAsync();
        return newIds.Count;
    }

    public async Task<bool> RemoveStudentAsync(Guid lecturerId, Guid examId, Guid studentId)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) return false;

        var es = await _db.ExamStudents.FirstOrDefaultAsync(
            x => x.ExamId == examId && x.StudentId == studentId);
        if (es is null) return false;

        _db.ExamStudents.Remove(es);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ExamStudentDto>> GetStudentsAsync(Guid lecturerId, Guid examId)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) return [];

        return await _db.ExamStudents
            .Where(es => es.ExamId == examId)
            .Select(es => new ExamStudentDto
            {
                StudentId = es.StudentId,
                MatricNumber = es.Student.IdNumber,
                SessionStatus = _db.Sessions
                    .Where(s => s.ExamId == examId && s.StudentId == es.StudentId)
                    .OrderByDescending(s => s.StartedAt)
                    .Select(s => s.Status)
                    .FirstOrDefault()
            })
            .ToListAsync();
    }

    // ── Submissions & Grading ──────────────────────────────────────────────

    public async Task<List<SubmissionListItemDto>> GetSubmissionsAsync(Guid lecturerId, Guid examId)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == examId && e.LecturerId == lecturerId);
        if (exam is null) return [];

        return await _db.Submissions
            .Where(s => s.ExamId == examId)
            .OrderBy(s => s.Question.OrderIndex)
            .ThenBy(s => s.Student.IdNumber)
            .Select(s => new SubmissionListItemDto
            {
                Id = s.Id,
                StudentMatric = s.Student.IdNumber,
                QuestionNumber = s.Question.OrderIndex + 1,
                CodePreview = s.Code.Length > 80 ? s.Code.Substring(0, 80) + "…" : s.Code,
                Score = s.Grade != null ? s.Grade.Score : null,
                MaxScore = s.Question.Marks,
                GradedBy = s.Grade != null ? s.Grade.GradedBy : null,
                SubmittedAt = s.SubmittedAt
            })
            .ToListAsync();
    }

    public async Task<SubmissionDetailDto?> GetSubmissionAsync(Guid lecturerId, Guid submissionId)
    {
        return await _db.Submissions
            .Where(s => s.Id == submissionId && s.Question.Exam.LecturerId == lecturerId)
            .Select(s => new SubmissionDetailDto
            {
                Id = s.Id,
                StudentMatric = s.Student.IdNumber,
                QuestionText = s.Question.Text,
                MarkingGuide = s.Question.MarkingGuide,
                MaxMarks = s.Question.Marks,
                Code = s.Code,
                Language = s.Question.Language,
                SubmittedAt = s.SubmittedAt,
                Grade = s.Grade != null ? new GradeDto
                {
                    Score = s.Grade.Score,
                    MaxScore = s.Grade.MaxScore,
                    GradedBy = s.Grade.GradedBy,
                    Feedback = s.Grade.FeedbackJson,
                    GradedAt = s.Grade.GradedAt
                } : null
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> GradeSubmissionAsync(Guid lecturerId, Guid submissionId, GradeSubmissionRequest req)
    {
        var sub = await _db.Submissions
            .Include(s => s.Question)
            .ThenInclude(q => q.Exam)
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.Question.Exam.LecturerId == lecturerId);
        if (sub is null) return false;

        if (sub.Grade is not null)
        {
            // Update existing grade
            sub.Grade.Score = req.Score;
            sub.Grade.MaxScore = sub.Question.Marks;
            sub.Grade.FeedbackJson = req.Feedback;
            sub.Grade.GradedBy = "Manual";
            sub.Grade.GradedAt = DateTime.UtcNow;
            sub.Grade.OverriddenBy = lecturerId;
            sub.Grade.OverrideReason = "Manual re-grade";
        }
        else
        {
            var grade = new Grade
            {
                Id = Guid.NewGuid(),
                SubmissionId = submissionId,
                Score = req.Score,
                MaxScore = sub.Question.Marks,
                FeedbackJson = req.Feedback,
                GradedBy = "Manual",
                GradedAt = DateTime.UtcNow
            };
            _db.Grades.Add(grade);
        }

        await _db.SaveChangesAsync();
        return true;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static LecturerExamDto MapToDto(Exam e, int qCount, int sCount, int totalMarks) => new()
    {
        Id = e.Id, Title = e.Title, CourseCode = e.CourseCode,
        ScheduledAt = e.ScheduledAt, DurationMinutes = e.DurationMinutes,
        Languages = DeserializeLanguages(e.Languages),
        IntelliSenseEnabled = e.IntelliSenseEnabled, GradingMode = e.GradingMode,
        Status = e.Status, QuestionCount = qCount, StudentCount = sCount,
        TotalMarks = totalMarks, CreatedAt = e.CreatedAt,
        ResultsReleased = e.ResultsReleased
    };

    private static List<string> DeserializeLanguages(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
