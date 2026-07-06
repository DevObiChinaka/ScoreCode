using ScoreCode.Shared.DTOs.Auth;
using ScoreCode.Shared.DTOs.Institutions;

namespace ScoreCode.Client.Services;

/// <summary>
/// Handles all auth API calls: login, register, refresh, logout.
/// Stores and retrieves tokens via SecureStorage.
/// </summary>
public class AuthClientService
{
    private readonly ApiService _api;

    public AuthClientService(ApiService api)
    {
        _api = api;
    }

    public async Task<(bool Success, string? Error, LoginResponse? Data)> LoginAsync(
        string idNumber, string password)
    {
        var result = await _api.PostAsync<LoginResponse>("auth/login",
            new { IdNumber = idNumber, Password = password });

        if (result is null)
            return (false, "No response from server.", null);

        if (!result.Success || result.Data is null)
            return (false, result.Message ?? "Invalid credentials.", null);

        // Store tokens and user info locally
        Helpers.SecureStorage.SaveSession(result.Data);
        _api.SetBearerToken(result.Data.AccessToken);

        return (true, null, result.Data);
    }

    /// <summary>Approved institutions for the registration dropdown (anonymous).</summary>
    public async Task<List<InstitutionOption>> GetInstitutionOptionsAsync()
    {
        var result = await _api.GetAsync<List<InstitutionOption>>("institutions/options");
        return result?.Data ?? [];
    }

    public async Task<(bool Success, string? Error)> RegisterStudentAsync(
        Guid institutionId, string matricNumber, string programme, string level, string password, string confirmPassword)
    {
        var result = await _api.PostAsync<LoginResponse>("auth/register", new
        {
            InstitutionId    = institutionId,
            MatricNumber   = matricNumber,
            ProgrammeOfStudy = programme,
            Level          = level,
            Password       = password,
            ConfirmPassword = confirmPassword
        });

        if (result is null)
            return (false, "No response from server.");

        if (!result.Success)
            return (false, result.Message ?? "Registration failed.");

        return (true, null);
    }

    public async Task<bool> TryRefreshAsync()
    {
        var session = Helpers.SecureStorage.LoadSession();
        if (session is null) return false;

        var result = await _api.PostAsync<LoginResponse>("auth/refresh",
            new { RefreshToken = session.RefreshToken });

        if (result is null || !result.Success || result.Data is null)
        {
            Helpers.SecureStorage.ClearSession();
            return false;
        }

        Helpers.SecureStorage.SaveSession(result.Data);
        _api.SetBearerToken(result.Data.AccessToken);
        return true;
    }

    public async Task LogoutAsync()
    {
        var session = Helpers.SecureStorage.LoadSession();
        if (session is not null)
        {
            await _api.PostAsync<object>("auth/logout",
                new { RefreshToken = session.RefreshToken });
        }
        Helpers.SecureStorage.ClearSession();
        _api.ClearBearerToken();
    }
}
