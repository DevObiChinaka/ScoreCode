using Microsoft.EntityFrameworkCore;
using ScoreCode.Database;
using ScoreCode.Database.Entities;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Api.Services;

public class UserService : IUserService
{
    private readonly ScoreCodeDb _db;

    public UserService(ScoreCodeDb db)
    {
        _db = db;
    }

    public async Task<UserDto> CreateLecturerAsync(CreateLecturerRequest request, Guid institutionId)
    {
        // Staff IDs are globally unique — check across all tenants.
        var exists = await _db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.IdNumber == request.StaffId);
        if (exists)
            throw new InvalidOperationException(Constants.ErrorCodes.EmailTaken);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IdNumber = request.StaffId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = Constants.Roles.Lecturer,
            InstitutionId = institutionId,
            Department = request.Department,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return MapToDto(user);
    }

    public async Task<List<UserDto>> GetUsersAsync(string? roleFilter)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(roleFilter))
            query = query.Where(u => u.Role == roleFilter);

        var users = await query
            .OrderBy(u => u.Name)
            .ToListAsync();

        return users.Select(MapToDto).ToList();
    }

    public async Task DeactivateUserAsync(Guid id)
    {
        // FirstOrDefault (not Find) so the tenant query filter applies — an
        // institution admin can only deactivate users within their institution.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new KeyNotFoundException(Constants.ErrorCodes.UserNotFound);

        user.IsActive = false;
        await _db.SaveChangesAsync();
    }

    public async Task ReactivateUserAsync(Guid id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new KeyNotFoundException(Constants.ErrorCodes.UserNotFound);

        user.IsActive = true;
        await _db.SaveChangesAsync();
    }

    public async Task<string> ResetPasswordAsync(Guid id)
    {
        // Tenant filter applies — an institution admin can only reset users in
        // their own institution; a platform admin (no tenant) can reset anyone.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new KeyNotFoundException(Constants.ErrorCodes.UserNotFound);

        var temp = GenerateTempPassword();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temp);
        await _db.SaveChangesAsync();
        return temp;
    }

    // Readable temp password that satisfies the app's change-password rules
    // (upper, lower, digit, special). Ambiguous characters are excluded.
    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var buf = new char[8];
        for (int i = 0; i < buf.Length; i++)
            buf[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(buf) + Random.Shared.Next(10) + "!";
    }

    public async Task UpdateStudentProfileAsync(Guid id, UpdateStudentProfileRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new KeyNotFoundException(Constants.ErrorCodes.UserNotFound);

        if (request.MatricNumber is not null)
        {
            var taken = await _db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.IdNumber == request.MatricNumber && u.Id != id);
            if (taken)
                throw new InvalidOperationException(Constants.ErrorCodes.EmailTaken);
            user.IdNumber = request.MatricNumber;
        }

        if (request.ProgrammeOfStudy is not null)
            user.ProgrammeOfStudy = request.ProgrammeOfStudy;

        if (request.Level is not null)
            user.Level = request.Level;

        if (request.Department is not null)
            user.Department = request.Department;

        await _db.SaveChangesAsync();
    }

    private static UserDto MapToDto(User u) => new()
    {
        Id = u.Id,
        Name = u.Name,
        IdNumber = u.IdNumber,
        Role = u.Role,
        Department = u.Department,
        ProgrammeOfStudy = u.ProgrammeOfStudy,
        Level = u.Level,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt
    };
}
