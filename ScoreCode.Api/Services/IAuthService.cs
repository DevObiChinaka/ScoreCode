using ScoreCode.Database.Entities;
using ScoreCode.Shared.DTOs.Auth;
using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<LoginResponse> RegisterStudentAsync(RegisterRequest request);
    Task<LoginResponse?> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);

    /// <summary>The authenticated user's own profile.</summary>
    Task<UserDto?> GetProfileAsync(Guid userId);
}
