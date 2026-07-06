namespace ScoreCode.Database.Entities;

public class Question
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public int OrderIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string MarkingGuide { get; set; } = string.Empty;
    public int Marks { get; set; }
    public string Language { get; set; } = string.Empty;

    // Navigation
    public Exam Exam { get; set; } = null!;
    public ICollection<Submission> Submissions { get; set; } = [];
}