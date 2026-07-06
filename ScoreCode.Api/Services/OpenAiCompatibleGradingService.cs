using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ScoreCode.Api.Services;

/// <summary>
/// <see cref="IAiGradingService"/> for any OpenAI-compatible chat-completions
/// endpoint — Groq (free, no billing/card), OpenRouter, OpenAI, or a local
/// Ollama / LM Studio. Configure Ai:BaseUrl, Ai:Model and Ai:ApiKey.
/// </summary>
public class OpenAiCompatibleGradingService : IAiGradingService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public OpenAiCompatibleGradingService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Ai:ApiKey"] ?? "";
        _model = string.IsNullOrWhiteSpace(config["Ai:Model"])
            ? "llama-3.3-70b-versatile" : config["Ai:Model"]!;
        _baseUrl = (string.IsNullOrWhiteSpace(config["Ai:BaseUrl"])
            ? "https://api.groq.com/openai/v1" : config["Ai:BaseUrl"]!).TrimEnd('/');
    }

    public async Task<AiGradeResult> SuggestGradeAsync(AiGradeRequest request, CancellationToken ct = default)
    {
        // Local endpoints (Ollama / LM Studio) don't need a key; hosted ones do.
        var isLocal = _baseUrl.Contains("localhost") || _baseUrl.Contains("127.0.0.1");
        if (!isLocal && string.IsNullOrWhiteSpace(_apiKey))
            throw new AiNotConfiguredException(
                "AI grading is not configured. Set Ai:ApiKey (and Ai:BaseUrl / Ai:Model) in the API configuration.");

        var body = new
        {
            model = _model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = "You are a strict, fair computer-science examiner. Reply with JSON only." },
                new { role = "user", content = AiGradingPrompt.Build(request) }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        if (!string.IsNullOrWhiteSpace(_apiKey))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = JsonContent.Create(body);

        using var resp = await _http.SendAsync(req, ct);
        var payload = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            var message = (int)resp.StatusCode switch
            {
                429 => "AI grading is over its quota/rate limit. Wait a moment and retry, or check the provider's free limits.",
                401 or 403 => "AI grading key was rejected. Check Ai:ApiKey / Ai:BaseUrl in the API configuration.",
                _ => $"AI request failed ({(int)resp.StatusCode}). {Truncate(payload, 200)}"
            };
            throw new InvalidOperationException(message);
        }

        var content = ExtractContent(payload)
            ?? throw new InvalidOperationException("AI provider returned no gradable content.");

        var (score, feedback) = AiGradingPrompt.ParseGrade(content);
        score = Math.Clamp(score, 0, request.MaxMarks);
        return new AiGradeResult(score, feedback);
    }

    /// <summary>Pull choices[0].message.content from an OpenAI-style response.</summary>
    private static string? ExtractContent(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            return null;
        return choices[0].GetProperty("message").GetProperty("content").GetString();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
