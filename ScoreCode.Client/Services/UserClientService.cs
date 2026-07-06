using ScoreCode.Shared.DTOs;
using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Client.Services;

/// <summary>Wraps UsersController API endpoints (admin only).</summary>
public class UserClientService
{
    private readonly ApiService _api;

    public UserClientService(ApiService api) => _api = api;

    public async Task<List<UserDto>> GetUsersAsync(string? role = null)
    {
        var url = string.IsNullOrEmpty(role) ? "users" : $"users?role={role}";
        var resp = await _api.GetAsync<List<UserDto>>(url);
        return resp?.Data ?? [];
    }

    public async Task<UserDto?> CreateLecturerAsync(CreateLecturerRequest request)
    {
        var resp = await _api.PostAsync<UserDto>("users", request);
        return resp?.Data;
    }

    public async Task<bool> DeactivateUserAsync(Guid userId)
    {
        var resp = await _api.PatchAsync<object>($"users/{userId}/deactivate");
        return resp?.Success == true;
    }

    public async Task<bool> UpdateStudentProfileAsync(Guid userId, UpdateStudentProfileRequest request)
    {
        var resp = await _api.PatchAsync<object>($"users/{userId}/profile", request);
        return resp?.Success == true;
    }
}
