namespace ScoreCode.Shared.DTOs.Lecturer;

public class ExamStudentDto
{
    public Guid StudentId { get; set; }
    public string MatricNumber { get; set; } = string.Empty;
    public string? SessionStatus { get; set; }
}
