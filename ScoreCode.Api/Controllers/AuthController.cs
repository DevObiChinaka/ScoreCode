using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoreCode.Api.Services;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs;
using ScoreCode.Shared.DTOs.Auth;
using ScoreCode.Shared.DTOs.Users;

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
        LoginResponse? result;
        try
        {
            result = await _auth.LoginAsync(request);
        }
        catch (InvalidOperationException ex) when (ex.Message == Constants.ErrorCodes.AccountLocked)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                Constants.ErrorCodes.AccountLocked,
                "Too many failed attempts. Try again in a few minutes, or ask an admin to reset your password."));
        }

        if (result is null)
            return Unauthorized(ApiResponse<object>.Fail(
                Constants.ErrorCodes.InvalidCredentials,
                "Invalid ID number or password."));

        return Ok(ApiResponse<LoginResponse>.Ok(result));
    }

    // POST /api/v1/auth/forgot-password — email a code, or queue an admin reset
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.IdNumber))
            await _auth.RequestPasswordResetAsync(request.IdNumber.Trim());

        // Always the same answer — never reveal whether an ID exists, or
        // whether it has an email on file.
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    // POST /api/v1/auth/reset-with-code — finish a reset using the emailed code
    [HttpPost("reset-with-code")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetWithCode([FromBody] ResetWithOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdNumber) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(ApiResponse<object>.Fail("validation_failed", "Enter your ID number and the code."));

        if (!IsStrongPassword(request.NewPassword))
            return BadRequest(ApiResponse<object>.Fail("validation_failed",
                "Password must be at least 8 characters and include an uppercase letter, a number and a special character."));

        var ok = await _auth.ResetWithOtpAsync(request.IdNumber.Trim(), request.Code.Trim(), request.NewPassword);

        // One message for every failure — wrong, expired, or no such account.
        return ok
            ? Ok(ApiResponse<object>.Ok(new { }))
            : BadRequest(ApiResponse<object>.Fail(Constants.ErrorCodes.InvalidOtp,
                "That code is invalid or has expired. Request a new one."));
    }

    private static bool IsStrongPassword(string? p) =>
        !string.IsNullOrWhiteSpace(p) && p.Length >= 8
        && p.Any(char.IsUpper) && p.Any(char.IsDigit) && p.Any(c => !char.IsLetterOrDigit(c));

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

    private Guid CurrentUserId() => Guid.Parse(
        User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException());

    // GET /api/v1/auth/me  — the authenticated user's own profile
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var profile = await _auth.GetProfileAsync(CurrentUserId());
        return profile is null
            ? NotFound(ApiResponse<object>.Fail("not_found", "User not found."))
            : Ok(ApiResponse<UserDto>.Ok(profile));
    }

    // PATCH /api/v1/auth/me/email — change your own recovery email
    [HttpPatch("me/email")]
    [Authorize]
    public async Task<IActionResult> UpdateMyEmail([FromBody] UpdateEmailRequest request)
    {
        var ok = await _auth.UpdateOwnEmailAsync(CurrentUserId(), request.Email);
        return ok
            ? Ok(ApiResponse<object>.Ok(new { }))
            : BadRequest(ApiResponse<object>.Fail("validation_failed", "Enter a valid email address, or leave it blank to remove it."));
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
        var userId = CurrentUserId();

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest(ApiResponse<object>.Fail("validation_failed", "Password must be at least 6 characters."));

        var ok = await _auth.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        return ok
            ? Ok(ApiResponse<object>.Ok(new { }))
            : BadRequest(ApiResponse<object>.Fail("invalid_password", "Current password is incorrect."));
    }
}
