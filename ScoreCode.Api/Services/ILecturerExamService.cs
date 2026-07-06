using ScoreCode.Shared.DTOs.Lecturer;

namespace ScoreCode.Api.Services;

public interface ILecturerExamService
{
    // Exam CRUD
    Task<LecturerExamDto> CreateExamAsync(Guid lecturerId, CreateExamRequest request);
    Task<List<LecturerExamDto>> GetExamsAsync(Guid lecturerId);
    Task<LecturerExamDto?> GetExamAsync(Guid lecturerId, Guid examId);
    Task<LecturerExamDto?> UpdateExamAsync(Guid lecturerId, Guid examId, UpdateExamRequest request);
    Task<bool> DeleteExamAsync(Guid lecturerId, Guid examId);
    Task<bool> PublishExamAsync(Guid lecturerId, Guid examId);
    Task<bool> CloseExamAsync(Guid lecturerId, Guid examId);
    Task<bool> ReleaseResultsAsync(Guid lecturerId, Guid examId);

    // Questions
    Task<LecturerQuestionDto> AddQuestionAsync(Guid lecturerId, Guid examId, CreateQuestionRequest request);
    Task<LecturerQuestionDto?> UpdateQuestionAsync(Guid lecturerId, Guid examId, Guid questionId, CreateQuestionRequest request);
    Task<bool> DeleteQuestionAsync(Guid lecturerId, Guid examId, Guid questionId);
    Task<List<LecturerQuestionDto>> GetQuestionsAsync(Guid lecturerId, Guid examId);

    // Students
    Task<int> AssignStudentsAsync(Guid lecturerId, Guid examId, AssignStudentsRequest request);
    Task<bool> RemoveStudentAsync(Guid lecturerId, Guid examId, Guid studentId);
    Task<List<ExamStudentDto>> GetStudentsAsync(Guid lecturerId, Guid examId);

    // Submissions
    Task<List<SubmissionListItemDto>> GetSubmissionsAsync(Guid lecturerId, Guid examId);
    Task<SubmissionDetailDto?> GetSubmissionAsync(Guid lecturerId, Guid submissionId);
    Task<bool> GradeSubmissionAsync(Guid lecturerId, Guid submissionId, GradeSubmissionRequest request);
}
