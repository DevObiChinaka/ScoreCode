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

    public AuthService(ScoreCodeDb db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
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
    /// Raise a reset request for an admin to action. Always succeeds from the
    /// caller's point of view so an unknown ID can't be probed.
    /// </summary>
    public async Task RequestPasswordResetAsync(string idNumber)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.IdNumber == idNumber && u.IsActive);
        if (user is null) return;

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
                CreatedAt = u.CreatedAt
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
