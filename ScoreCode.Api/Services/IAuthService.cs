using ScoreCode.Database.Entities;
using ScoreCode.Shared.DTOs.Auth;
using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<LoginResponse> RegisterStudentAsync(RegisterRequest request);

    /// <summary>Raise a password-reset request for an admin to action.</summary>
    /// <summary>
    /// Start a reset: emails a one-time code when an address is on file,
    /// otherwise queues the request for an administrator.
    /// </summary>
    Task RequestPasswordResetAsync(string idNumber);

    /// <summary>Complete a reset with the emailed code. False if it isn't valid.</summary>
    Task<bool> ResetWithOtpAsync(string idNumber, string code, string newPassword);
    Task<LoginResponse?> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);

    /// <summary>The authenticated user's own profile.</summary>
    Task<UserDto?> GetProfileAsync(Guid userId);

    /// <summary>Change the caller's own recovery email. False if it isn't valid.</summary>
    Task<bool> UpdateOwnEmailAsync(Guid userId, string? email);
}
