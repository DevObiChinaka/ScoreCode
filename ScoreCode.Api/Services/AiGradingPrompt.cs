using System.Text.Json;

namespace ScoreCode.Api.Services;

/// <summary>Shared grading prompt + tolerant JSON parsing, used by all AI providers.</summary>
public static class AiGradingPrompt
{
    public static string Build(AiGradeRequest r)
    {
        var guide = string.IsNullOrWhiteSpace(r.MarkingGuide)
            ? "(No marking guide provided — grade on correctness, clarity and completeness.)"
            : r.MarkingGuide;

        return $$"""
            You are an experienced computer science examiner grading a student's code answer.
            Grade strictly against the marking guide and award a whole-number score out of {{r.MaxMarks}}.

            Return ONLY JSON of the form: {"score": <integer 0..{{r.MaxMarks}}>, "feedback": "<2-4 sentence justification>"}.

            QUESTION:
            {{r.QuestionText}}

            MARKING GUIDE:
            {{guide}}

            LANGUAGE: {{r.Language}}

            STUDENT CODE:
            ```
            {{r.Code}}
            ```
            """;
    }

    /// <summary>Parse the model's JSON answer, tolerating stray text around it.</summary>
    public static (int Score, string Feedback) ParseGrade(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        var json = (start >= 0 && end > start) ? text[start..(end + 1)] : text;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var score = root.TryGetProperty("score", out var s)
            ? (s.ValueKind == JsonValueKind.Number ? s.GetInt32() : int.TryParse(s.GetString(), out var v) ? v : 0)
            : 0;
        var feedback = root.TryGetProperty("feedback", out var f) ? f.GetString() ?? "" : "";
        return (score, feedback);
    }
}
