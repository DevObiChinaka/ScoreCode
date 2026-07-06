using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoreCode.Api.Services;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs;
using ScoreCode.Shared.DTOs.Users;
using System.Security.Claims;

namespace ScoreCode.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = Constants.Roles.AnyAdmin)]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users)
    {
        _users = users;
    }

    private Guid? GetInstitutionId() =>
        Guid.TryParse(User.FindFirstValue("institutionId"), out var id) ? id : null;

    // POST /api/v1/users  — an institution admin creates a lecturer for their school
    [HttpPost]
    public async Task<IActionResult> CreateLecturer([FromBody] CreateLecturerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.StaffId) ||
            string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(ApiResponse<object>.Fail("validation_failed", "Name, StaffId, and Password are required."));

        // Lecturers are created within an institution. The platform admin has no
        // institution context, so they can't create lecturers directly.
        var institutionId = GetInstitutionId();
        if (institutionId is null)
            return BadRequest(ApiResponse<object>.Fail(
                Constants.ErrorCodes.NoInstitutionContext,
                "Only an institution admin can create lecturers."));

        try
        {
            var result = await _users.CreateLecturerAsync(request, institutionId.Value);
            return StatusCode(201, ApiResponse<UserDto>.Ok(result));
        }
        catch (InvalidOperationException ex) when (ex.Message == Constants.ErrorCodes.EmailTaken)
        {
            return Conflict(ApiResponse<object>.Fail(
                Constants.ErrorCodes.EmailTaken,
                "A user with that Staff ID already exists."));
        }
    }

    // GET /api/v1/users?role=Lecturer
    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] string? role)
    {
        var result = await _users.GetUsersAsync(role);
        return Ok(ApiResponse<List<UserDto>>.Ok(result));
    }

    // PATCH /api/v1/users/{id}/deactivate
    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        try
        {
            await _users.DeactivateUserAsync(id);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(
                Constants.ErrorCodes.UserNotFound,
                "User not found."));
        }
    }

    // PATCH /api/v1/users/{id}/activate  — reactivate a deactivated user
    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        try
        {
            await _users.ReactivateUserAsync(id);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(
                Constants.ErrorCodes.UserNotFound, "User not found."));
        }
    }

    // PATCH /api/v1/users/{id}/profile  — update student immutable fields (admin only)
    [HttpPatch("{id:guid}/profile")]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] UpdateStudentProfileRequest request)
    {
        try
        {
            await _users.UpdateStudentProfileAsync(id, request);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail(
                Constants.ErrorCodes.UserNotFound,
                "User not found."));
        }
        catch (InvalidOperationException ex) when (ex.Message == Constants.ErrorCodes.EmailTaken)
        {
            return Conflict(ApiResponse<object>.Fail(
                Constants.ErrorCodes.EmailTaken,
                "That matric number is already in use."));
        }
    }
}
