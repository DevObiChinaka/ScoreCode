using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ScoreCode.Shared.DTOs;

namespace ScoreCode.Client.Services;

/// <summary>
/// Base HTTP client wrapper. All API calls go through here.
/// Reads server config from appsettings.json using System.Text.Json directly.
/// </summary>
public class ApiService
{
    private readonly HttpClient _http;
    private readonly string _apiVersion;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiService()
    {
        // Load client appsettings.json with built-in System.Text.Json
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        var json = File.ReadAllText(configPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var serverUrl = root.GetProperty("ServerUrl").GetString()!.TrimEnd('/');
        _apiVersion = root.TryGetProperty("ApiVersion", out var v) ? v.GetString() ?? "v1" : "v1";

        _http = new HttpClient { BaseAddress = new Uri(serverUrl) };
    }

    /// <summary>Attach the stored JWT access token to all requests.</summary>
    public void SetBearerToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Clear the auth header (on logout).</summary>
    public void ClearBearerToken()
    {
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<ApiResponse<T>?> PostAsync<T>(string path, object body)
    {
        var url = $"/api/{_apiVersion}/{path.TrimStart('/')}";
        var response = await _http.PostAsJsonAsync(url, body, JsonOpts);
        var responseJson = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(responseJson))
            return response.IsSuccessStatusCode
                ? ApiResponse<T>.Ok(default!)
                : null;
        return JsonSerializer.Deserialize<ApiResponse<T>>(responseJson, JsonOpts);
    }

    public async Task<ApiResponse<T>?> GetAsync<T>(string path)
    {
        var url = $"/api/{_apiVersion}/{path.TrimStart('/')}";
        var response = await _http.GetAsync(url);
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(responseJson, JsonOpts);
    }

    public async Task<ApiResponse<T>?> PatchAsync<T>(string path, object? body = null)
    {
        var url = $"/api/{_apiVersion}/{path.TrimStart('/')}";
        HttpContent content = body is null
            ? new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            : JsonContent.Create(body, options: JsonOpts);
        var response = await _http.PatchAsync(url, content);
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(responseJson, JsonOpts);
    }

    public async Task<ApiResponse<T>?> PutAsync<T>(string path, object? body = null)
    {
        var url = $"/api/{_apiVersion}/{path.TrimStart('/')}";
        HttpContent content = body is null
            ? new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            : JsonContent.Create(body, options: JsonOpts);
        var response = await _http.PutAsync(url, content);
        var responseJson = await response.Content.ReadAsStringAsync();
        System.Diagnostics.Debug.WriteLine($"[PUT] {url} → {(int)response.StatusCode} {response.StatusCode}");
        System.Diagnostics.Debug.WriteLine($"[PUT] Body: {responseJson}");
        if (string.IsNullOrWhiteSpace(responseJson))
            return response.IsSuccessStatusCode
                ? ApiResponse<T>.Ok(default!)
                : null;
        return JsonSerializer.Deserialize<ApiResponse<T>>(responseJson, JsonOpts);
    }

    public async Task<ApiResponse<T>?> DeleteAsync<T>(string path)
    {
        var url = $"/api/{_apiVersion}/{path.TrimStart('/')}";
        var response = await _http.DeleteAsync(url);
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(responseJson, JsonOpts);
    }

    public async Task<ApiResponse<TResult>?> PostAsync<TBody, TResult>(string path, TBody body)
    {
        var url = $"/api/{_apiVersion}/{path.TrimStart('/')}";
        var response = await _http.PostAsJsonAsync(url, body, JsonOpts);
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<TResult>>(responseJson, JsonOpts);
    }
}
