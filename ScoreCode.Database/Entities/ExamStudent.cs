namespace ScoreCode.Database.Entities;

public class ExamStudent
{
    public Guid ExamId { get; set; }
    public Guid StudentId { get; set; }
    public int ExtraTimeMinutes { get; set; } = 0;

    // Navigation
    public Exam Exam { get; set; } = null!;
    public User Student { get; set; } = null!;
}