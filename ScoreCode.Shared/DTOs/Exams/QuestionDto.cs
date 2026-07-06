namespace ScoreCode.Shared.DTOs.Exams;

/// <summary>A single question as sent to the student during an exam.</summary>
public class QuestionDto
{
    public Guid Id { get; set; }
    public int OrderIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Marks { get; set; }
    public string Language { get; set; } = string.Empty;
}
