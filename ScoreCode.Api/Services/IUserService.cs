using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Api.Services;

public interface IUserService
{
    Task<UserDto> CreateLecturerAsync(CreateLecturerRequest request, Guid institutionId);
    Task<List<UserDto>> GetUsersAsync(string? roleFilter);
    Task DeactivateUserAsync(Guid id);
    Task ReactivateUserAsync(Guid id);
    Task UpdateStudentProfileAsync(Guid id, UpdateStudentProfileRequest request);
}
