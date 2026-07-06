namespace ScoreCode.Shared.DTOs.Lecturer;

public class CreateQuestionRequest
{
    public string Text { get; set; } = string.Empty;
    public string MarkingGuide { get; set; } = string.Empty;
    public int Marks { get; set; }
    public string Language { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}
