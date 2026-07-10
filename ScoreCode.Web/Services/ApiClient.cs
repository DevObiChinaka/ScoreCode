using System.Net.Http.Headers;
using System.Net.Http.Json;
using ScoreCode.Shared.DTOs;
using ScoreCode.Shared.DTOs.Auth;
using ScoreCode.Shared.DTOs.Institutions;
using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Web.Services;

/// <summary>Thin typed client over the central ScoreCode API.</summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly WebAuthState _auth;

    public ApiClient(HttpClient http, WebAuthState auth)
    {
        _http = http;
        _auth = auth;
    }

    private void Authorize() =>
        _http.DefaultRequestHeaders.Authorization =
            _auth.Token is null ? null : new AuthenticationHeaderValue("Bearer", _auth.Token);

    // ── Auth ────────────────────────────────────────────────────────────────
    public async Task<(bool Ok, string? Error, LoginResponse? Data)> LoginAsync(string idNumber, string password)
    {
        var resp = await _http.PostAsJsonAsync("/api/v1/auth/login", new { idNumber, password });
        var body = await Read<LoginResponse>(resp);
        return body?.Success == true && body.Data is not null
            ? (true, null, body.Data)
            : (false, body?.Message ?? "Invalid ID number or password.", null);
    }

    // ── Institutions ─────────────────────────────────────────────────────────
    public async Task<(bool Ok, string? Error)> ApplyInstitutionAsync(InstitutionApplicationRequest req)
    {
        var resp = await _http.PostAsJsonAsync("/api/v1/institutions/apply", req);
        var body = await Read<InstitutionDto>(resp);
        return body?.Success == true ? (true, null) : (false, body?.Message ?? "Application failed.");
    }

    public async Task<List<InstitutionDto>> GetInstitutionsAsync(string? status)
    {
        Authorize();
        var url = string.IsNullOrEmpty(status) ? "/api/v1/institutions" : $"/api/v1/institutions?status={status}";
        var body = await _http.GetFromJsonAsync<ApiResponse<List<InstitutionDto>>>(url);
        return body?.Data ?? [];
    }

    public async Task<PlatformDashboardDto?> GetPlatformDashboardAsync()
    {
        Authorize();
        var body = await _http.GetFromJsonAsync<ApiResponse<PlatformDashboardDto>>("/api/v1/dashboard/platform");
        return body?.Data;
    }

    public async Task<ApproveInstitutionResponse?> ApproveInstitutionAsync(Guid id)
    {
        Authorize();
        var resp = await _http.PostAsync($"/api/v1/institutions/{id}/approve", null);
        var body = await Read<ApproveInstitutionResponse>(resp);
        return body?.Data;
    }

    public async Task<bool> RejectInstitutionAsync(Guid id, string reason)
    {
        Authorize();
        var resp = await _http.PostAsJsonAsync($"/api/v1/institutions/{id}/reject",
            new RejectInstitutionRequest { Reason = reason });
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> SuspendInstitutionAsync(Guid id)
    {
        Authorize();
        var resp = await _http.PostAsync($"/api/v1/institutions/{id}/suspend", null);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> ReactivateInstitutionAsync(Guid id)
    {
        Authorize();
        var resp = await _http.PostAsync($"/api/v1/institutions/{id}/reactivate", null);
        return resp.IsSuccessStatusCode;
    }

    /// <summary>Activate or deactivate a user (institution admin, tenant-scoped).</summary>
    public async Task<bool> SetUserActiveAsync(Guid id, bool active)
    {
        Authorize();
        var verb = active ? "activate" : "deactivate";
        var resp = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{id}/{verb}"));
        return resp.IsSuccessStatusCode;
    }

    // ── Users (institution admin) ─────────────────────────────────────────────
    public async Task<List<UserDto>> GetUsersAsync(string? role)
    {
        Authorize();
        var url = string.IsNullOrEmpty(role) ? "/api/v1/users" : $"/api/v1/users?role={role}";
        var body = await _http.GetFromJsonAsync<ApiResponse<List<UserDto>>>(url);
        return body?.Data ?? [];
    }

    public async Task<(bool Ok, string? Error)> CreateLecturerAsync(CreateLecturerRequest req)
    {
        Authorize();
        var resp = await _http.PostAsJsonAsync("/api/v1/users", req);
        var body = await Read<UserDto>(resp);
        return body?.Success == true ? (true, null) : (false, body?.Message ?? "Failed to create lecturer.");
    }

    private static async Task<ApiResponse<T>?> Read<T>(HttpResponseMessage resp)
    {
        try { return await resp.Content.ReadFromJsonAsync<ApiResponse<T>>(); }
        catch { return null; }
    }
}
