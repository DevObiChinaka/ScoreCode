using System.Net.Http.Json;
using System.Text.Json;

namespace ScoreCode.Api.Services;

/// <summary>
/// <see cref="IAiGradingService"/> backed by Google's Gemini API. Chosen as the
/// default provider for cost during development; the interface lets a Claude
/// (direct or MCP) implementation replace it later without touching callers.
/// </summary>
public class GeminiGradingService : IAiGradingService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiGradingService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Ai:ApiKey"] ?? "";
        _model = string.IsNullOrWhiteSpace(config["Ai:Model"]) ? "gemini-2.0-flash" : config["Ai:Model"]!;
    }

    public async Task<AiGradeResult> SuggestGradeAsync(AiGradeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new AiNotConfiguredException(
                "AI grading is not configured. Set the Ai:ApiKey value in the API configuration.");

        var prompt = BuildPrompt(request);

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        using var resp = await _http.PostAsJsonAsync(url, body, ct);
        var payload = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            var message = (int)resp.StatusCode switch
            {
                429 => "AI grading is over its Gemini quota/rate limit. Check the API key's plan & billing at Google AI Studio, or try again shortly.",
                401 or 403 => "AI grading key was rejected by Gemini. Check Ai:ApiKey in the API configuration.",
                _ => $"Gemini request failed ({(int)resp.StatusCode}). {Truncate(payload, 200)}"
            };
            throw new InvalidOperationException(message);
        }

        var text = ExtractText(payload)
            ?? throw new InvalidOperationException("Gemini returned no gradable content.");

        var (score, feedback) = ParseGrade(text);
        score = Math.Clamp(score, 0, request.MaxMarks);
        return new AiGradeResult(score, feedback);
    }

    private static string BuildPrompt(AiGradeRequest r)
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

    /// <summary>Pull candidates[0].content.parts[0].text out of the Gemini response.</summary>
    private static string? ExtractText(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.GetArrayLength() == 0)
            return null;

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        if (parts.GetArrayLength() == 0) return null;
        return parts[0].GetProperty("text").GetString();
    }

    /// <summary>Parse the model's JSON answer, tolerating stray text around it.</summary>
    private static (int Score, string Feedback) ParseGrade(string text)
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

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
