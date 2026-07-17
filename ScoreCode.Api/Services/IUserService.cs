using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Api.Services;

public interface IUserService
{
    Task<UserDto> CreateLecturerAsync(CreateLecturerRequest request, Guid institutionId);
    Task<List<UserDto>> GetUsersAsync(string? roleFilter, Guid? institutionId = null);
    Task DeactivateUserAsync(Guid id);
    Task ReactivateUserAsync(Guid id);
    Task<string> ResetPasswordAsync(Guid id);

    /// <summary>Pending "forgot password" requests this admin can action.</summary>
    Task<List<ResetRequestDto>> GetResetRequestsAsync();

    /// <summary>Reset the requester's password and close the request; returns the temp password.</summary>
    Task<string> ResolveResetRequestAsync(Guid requestId);
    Task UpdateStudentProfileAsync(Guid id, UpdateStudentProfileRequest request);
}
