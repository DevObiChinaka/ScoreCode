using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ScoreCode.Database;
using ScoreCode.Database.Entities;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs.Auth;
using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Api.Services;

public class AuthService : IAuthService
{
    private readonly ScoreCodeDb _db;
    private readonly ITokenService _tokens;
    private readonly IEmailService _email;

    public AuthService(ScoreCodeDb db, ITokenService tokens, IEmailService email)
    {
        _db = db;
        _tokens = tokens;
        _email = email;
    }

    /// <summary>Consecutive failures before sign-in is temporarily refused.</summary>
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.IdNumber == request.IdNumber && u.IsActive);

        if (user is null)
            return null; // unknown ID — say nothing more

        // Refuse while locked out, without revealing whether the password was right.
        if (user.LockoutUntil is not null && user.LockoutUntil > DateTime.UtcNow)
            throw new InvalidOperationException(Constants.ErrorCodes.AccountLocked);

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginAttempts = 0;
            }
            await _db.SaveChangesAsync();
            return null;
        }

        // Success — clear any failure state.
        if (user.FailedLoginAttempts != 0 || user.LockoutUntil is not null)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutUntil = null;
            await _db.SaveChangesAsync();
        }

        return await IssueTokensAsync(user);
    }

    /// <summary>
    /// Start a password reset. When the account has an email on file a one-time
    /// code is sent there; otherwise (or if the send fails) an administrator is
    /// asked to reset it instead. Always succeeds from the caller's point of
    /// view so an unknown ID can't be probed.
    /// </summary>
    public async Task RequestPasswordResetAsync(string idNumber)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.IdNumber == idNumber && u.IsActive);
        if (user is null) return;

        if (_email.IsEnabled && !string.IsNullOrWhiteSpace(user.Email))
        {
            try
            {
                await SendResetCodeAsync(user);
                return;
            }
            catch
            {
                // Mail is down or the address is bad — fall through to an admin
                // reset rather than leave the user waiting for a code.
            }
        }

        await RaiseAdminResetRequestAsync(user);
    }

    /// <summary>Issue a fresh code, replacing any code already outstanding.</summary>
    private async Task SendResetCodeAsync(User user)
    {
        var code = GenerateOtp();

        user.ResetOtpHash = BCrypt.Net.BCrypt.HashPassword(code);
        user.ResetOtpExpiresAt = DateTime.UtcNow.AddMinutes(Constants.PasswordReset.OtpExpiryMinutes);
        user.ResetOtpAttempts = 0;
        await _db.SaveChangesAsync();

        await _email.SendPasswordResetCodeAsync(
            user.Email!, user.IdNumber, code, Constants.PasswordReset.OtpExpiryMinutes);
    }

    private async Task RaiseAdminResetRequestAsync(User user)
    {
        var alreadyPending = await _db.PasswordResetRequests.IgnoreQueryFilters()
            .AnyAsync(r => r.UserId == user.Id && r.Status == Constants.ResetRequestStatuses.Pending);
        if (alreadyPending) return;

        _db.PasswordResetRequests.Add(new PasswordResetRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            InstitutionId = user.InstitutionId,
            RequestedAt = DateTime.UtcNow,
            Status = Constants.ResetRequestStatuses.Pending
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Complete a reset with the emailed code. Returns false for any failure —
    /// wrong, expired, exhausted or absent — so nothing about the account leaks.
    /// </summary>
    public async Task<bool> ResetWithOtpAsync(string idNumber, string code, string newPassword)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.IdNumber == idNumber && u.IsActive);

        if (user?.ResetOtpHash is null || user.ResetOtpExpiresAt is null) return false;

        if (user.ResetOtpExpiresAt < DateTime.UtcNow
            || user.ResetOtpAttempts >= Constants.PasswordReset.MaxOtpAttempts)
        {
            ClearOtp(user);
            await _db.SaveChangesAsync();
            return false;
        }

        if (!BCrypt.Net.BCrypt.Verify(code, user.ResetOtpHash))
        {
            // Count the guess so a 6-digit code can't be brute forced.
            user.ResetOtpAttempts++;
            await _db.SaveChangesAsync();
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        // They chose this password themselves, so nothing to force on next login.
        user.MustChangePassword = false;
        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;   // a reset also lifts a lockout
        ClearOtp(user);

        // Any pending admin request is now moot.
        var pending = await _db.PasswordResetRequests.IgnoreQueryFilters()
            .Where(r => r.UserId == user.Id && r.Status == Constants.ResetRequestStatuses.Pending)
            .ToListAsync();
        foreach (var r in pending)
        {
            r.Status = Constants.ResetRequestStatuses.Handled;
            r.HandledAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    private static void ClearOtp(User user)
    {
        user.ResetOtpHash = null;
        user.ResetOtpExpiresAt = null;
        user.ResetOtpAttempts = 0;
    }

    /// <summary>Trim and lowercase an address; blank becomes null (no address on file).</summary>
    internal static string? Normalize(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static string GenerateOtp()
    {
        var digits = new char[Constants.PasswordReset.OtpLength];
        for (var i = 0; i < digits.Length; i++)
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        return new string(digits);
    }

    public async Task<LoginResponse> RegisterStudentAsync(RegisterRequest request)
    {
        // The chosen institution must exist and be approved.
        var institution = await _db.Institutions
            .FirstOrDefaultAsync(i => i.Id == request.InstitutionId
                                   && i.Status == Constants.InstitutionStatuses.Approved);
        if (institution is null)
            throw new InvalidOperationException(Constants.ErrorCodes.InvalidInstitution);

        // Matric numbers are globally unique (institution-prefixed). Check across
        // all tenants (IgnoreQueryFilters) so the friendly error fires reliably.
        var exists = await _db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.IdNumber == request.MatricNumber);
        if (exists)
            throw new InvalidOperationException(Constants.ErrorCodes.EmailTaken); // reusing error code

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.MatricNumber,   // anonymous — no display name collected
            IdNumber = request.MatricNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Constants.Roles.Student,
            InstitutionId = institution.Id,
            ProgrammeOfStudy = request.ProgrammeOfStudy,
            Level = request.Level,
            Email = Normalize(request.Email),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return await IssueTokensAsync(user);
    }

    public async Task<LoginResponse?> RefreshAsync(string refreshToken)
    {
        var stored = await _tokens.ValidateRefreshTokenAsync(refreshToken);
        if (stored is null) return null;

        // Rotate: revoke old, issue new
        await _tokens.RevokeRefreshTokenAsync(refreshToken);
        return await IssueTokensAsync(stored.User);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        await _tokens.RevokeRefreshTokenAsync(refreshToken);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return false;

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = false; // they've now set their own
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<UserDto?> GetProfileAsync(Guid userId)
    {
        return await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Name = u.Name,
                IdNumber = u.IdNumber,
                Role = u.Role,
                Department = u.Department,
                ProgrammeOfStudy = u.ProgrammeOfStudy,
                Level = u.Level,
                InstitutionName = u.Institution != null ? u.Institution.Name : string.Empty,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                Email = u.Email   // GetProfileAsync — the owner's own record
            })
            .FirstOrDefaultAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<LoginResponse> IssueTokensAsync(User user)
    {
        var accessToken = _tokens.CreateAccessToken(user);
        var refreshToken = await _tokens.CreateRefreshTokenAsync(user.Id);

        return new LoginResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiry = DateTime.UtcNow.AddMinutes(15),
            RefreshToken = refreshToken.Token,
            User = new LoginResponse.UserInfo
            {
                Id = user.Id,
                Name = user.Name,
                IdNumber = user.IdNumber,
                Role = user.Role,
                MustChangePassword = user.MustChangePassword
            }
        };
    }
}
