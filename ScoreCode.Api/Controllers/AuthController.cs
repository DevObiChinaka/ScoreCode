using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoreCode.Api.Services;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs;
using ScoreCode.Shared.DTOs.Auth;

namespace ScoreCode.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    // POST /api/v1/auth/register  — student self-registration
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword)
            return BadRequest(ApiResponse<object>.Fail("validation_failed", "Passwords do not match."));

        if (request.InstitutionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.MatricNumber) ||
            string.IsNullOrWhiteSpace(request.ProgrammeOfStudy) ||
            string.IsNullOrWhiteSpace(request.Level))
            return BadRequest(ApiResponse<object>.Fail("validation_failed", "All fields, including an institution, are required."));

        try
        {
            var result = await _auth.RegisterStudentAsync(request);
            return StatusCode(201, ApiResponse<LoginResponse>.Ok(result));
        }
        catch (InvalidOperationException ex) when (ex.Message == Constants.ErrorCodes.EmailTaken)
        {
            return Conflict(ApiResponse<object>.Fail(
                Constants.ErrorCodes.EmailTaken,
                "A student with that matric number already exists."));
        }
        catch (InvalidOperationException ex) when (ex.Message == Constants.ErrorCodes.InvalidInstitution)
        {
            return BadRequest(ApiResponse<object>.Fail(
                Constants.ErrorCodes.InvalidInstitution,
                "The selected institution was not found or is not approved."));
        }
    }

    // POST /api/v1/auth/login  — all roles
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        if (result is null)
            return Unauthorized(ApiResponse<object>.Fail(
                Constants.ErrorCodes.InvalidCredentials,
                "Invalid ID number or password."));

        return Ok(ApiResponse<LoginResponse>.Ok(result));
    }

    // POST /api/v1/auth/refresh
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _auth.RefreshAsync(request.RefreshToken);
        if (result is null)
            return Unauthorized(ApiResponse<object>.Fail(
                Constants.ErrorCodes.InvalidToken,
                "Refresh token is invalid or expired."));

        return Ok(ApiResponse<LoginResponse>.Ok(result));
    }

    // POST /api/v1/auth/logout  — requires valid JWT
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await _auth.LogoutAsync(request.RefreshToken);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
    // POST /api/v1/auth/change-password
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = Guid.Parse(
            User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException());

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest(ApiResponse<object>.Fail("validation_failed", "Password must be at least 6 characters."));

        var ok = await _auth.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        return ok
            ? Ok(ApiResponse<object>.Ok(new { }))
            : BadRequest(ApiResponse<object>.Fail("invalid_password", "Current password is incorrect."));
    }
}
