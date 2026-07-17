using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ScoreCode.Database;
using ScoreCode.Database.Entities;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs.Institutions;
using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Api.Services;

public class InstitutionService : IInstitutionService
{
    private readonly ScoreCodeDb _db;
    private readonly IEmailService _email;

    public InstitutionService(ScoreCodeDb db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    // ── Public ──────────────────────────────────────────────────────────────
    public async Task<InstitutionDto> ApplyAsync(InstitutionApplicationRequest req)
    {
        var inst = new Institution
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            Type = string.IsNullOrWhiteSpace(req.Type) ? "University" : req.Type.Trim(),
            Prefix = req.Prefix.Trim().ToUpperInvariant(),
            ContactEmail = req.ContactEmail.Trim(),
            ContactPerson = req.ContactPerson.Trim(),
            Status = Constants.InstitutionStatuses.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.Institutions.Add(inst);
        await _db.SaveChangesAsync();
        return ToDto(inst);
    }

    public async Task<List<InstitutionOption>> GetApprovedOptionsAsync() =>
        await _db.Institutions
            .Where(i => i.Status == Constants.InstitutionStatuses.Approved)
            .OrderBy(i => i.Name)
            .Select(i => new InstitutionOption { Id = i.Id, Name = i.Name, Prefix = i.Prefix })
            .ToListAsync();

    // ── Platform admin ──────────────────────────────────────────────────────
    public async Task<List<InstitutionDto>> GetAllAsync(string? status)
    {
        var q = _db.Institutions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(i => i.Status == status);
        return await q.OrderByDescending(i => i.CreatedAt)
            .Select(i => new InstitutionDto
            {
                Id = i.Id, Name = i.Name, Type = i.Type, Prefix = i.Prefix,
                ContactEmail = i.ContactEmail, ContactPerson = i.ContactPerson,
                Status = i.Status, CreatedAt = i.CreatedAt,
                ApprovedAt = i.ApprovedAt, RejectionReason = i.RejectionReason
            })
            .ToListAsync();
    }

    public async Task<ApproveInstitutionResponse?> ApproveAsync(Guid institutionId)
    {
        var inst = await _db.Institutions.FirstOrDefaultAsync(i => i.Id == institutionId);
        if (inst is null || inst.Status != Constants.InstitutionStatuses.Pending)
            return null;

        inst.Status = Constants.InstitutionStatuses.Approved;
        inst.ApprovedAt = DateTime.UtcNow;

        // Provision the institution's first admin account.
        var idNumber = await GenerateAdminIdNumberAsync(inst);
        var tempPassword = GenerateTemporaryPassword();

        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = $"{inst.Name} Administrator",
            IdNumber = idNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword),
            Role = Constants.Roles.InstitutionAdmin,
            InstitutionId = inst.Id,
            IsActive = true,
            MustChangePassword = true,   // temporary credentials handed to the school
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _email.SendInstitutionApprovedAsync(
            inst.ContactEmail, inst.Name, idNumber, tempPassword);

        return new ApproveInstitutionResponse
        {
            InstitutionId = inst.Id,
            InstitutionName = inst.Name,
            AdminIdNumber = idNumber,
            TemporaryPassword = tempPassword
        };
    }

    public async Task<(InstitutionDto? Institution, string? Error)> UpdateAsync(
        Guid institutionId, UpdateInstitutionRequest req)
    {
        var inst = await _db.Institutions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == institutionId);
        if (inst is null) return (null, "Institution not found.");

        var name = req.Name.Trim();
        var email = req.ContactEmail.Trim();
        if (name.Length == 0) return (null, "Institution name is required.");
        if (email.Length == 0) return (null, "A contact email is required.");

        inst.Name = name;
        inst.ContactEmail = email;
        inst.ContactPerson = req.ContactPerson.Trim();
        if (!string.IsNullOrWhiteSpace(req.Type)) inst.Type = req.Type.Trim();

        var prefix = req.Prefix.Trim().ToUpperInvariant();
        if (prefix.Length > 0 && prefix != inst.Prefix)
        {
            // Matric and staff numbers already issued embed the old prefix, so
            // changing it would orphan them from their institution's identity.
            var hasUsers = await _db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.InstitutionId == institutionId);
            if (hasUsers)
                return (null, "The prefix cannot be changed once accounts have been issued under it.");

            var taken = await _db.Institutions.IgnoreQueryFilters()
                .AnyAsync(i => i.Id != institutionId && i.Prefix == prefix);
            if (taken)
                return (null, "That prefix is already used by another institution.");

            inst.Prefix = prefix;
        }

        await _db.SaveChangesAsync();
        return (ToDto(inst), null);
    }

    public async Task<bool> RejectAsync(Guid institutionId, string reason)
    {
        var inst = await _db.Institutions.FirstOrDefaultAsync(i => i.Id == institutionId);
        if (inst is null || inst.Status != Constants.InstitutionStatuses.Pending)
            return false;

        inst.Status = Constants.InstitutionStatuses.Rejected;
        inst.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "No reason given." : reason.Trim();
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SuspendAsync(Guid institutionId)
    {
        var inst = await _db.Institutions.FirstOrDefaultAsync(i => i.Id == institutionId);
        if (inst is null || inst.Status != Constants.InstitutionStatuses.Approved) return false;
        inst.Status = Constants.InstitutionStatuses.Suspended;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReactivateAsync(Guid institutionId)
    {
        var inst = await _db.Institutions.FirstOrDefaultAsync(i => i.Id == institutionId);
        if (inst is null || inst.Status != Constants.InstitutionStatuses.Suspended) return false;
        inst.Status = Constants.InstitutionStatuses.Approved;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private async Task<string> GenerateAdminIdNumberAsync(Institution inst)
    {
        var baseId = (string.IsNullOrWhiteSpace(inst.Prefix) ? Slug(inst.Name) : inst.Prefix) + "-ADMIN";
        var candidate = baseId;
        var n = 1;
        // IgnoreQueryFilters so uniqueness is checked platform-wide, not per-tenant.
        while (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.IdNumber == candidate))
            candidate = $"{baseId}{++n}";
        return candidate;
    }

    private static string Slug(string name)
    {
        var chars = name.ToUpperInvariant().Where(char.IsLetterOrDigit).Take(6).ToArray();
        return chars.Length > 0 ? new string(chars) : "INST";
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digit = "23456789";
        const string special = "!@#$%*";
        const string all = upper + lower + digit + special;

        var sb = new StringBuilder();
        sb.Append(Pick(upper));
        sb.Append(Pick(lower));
        sb.Append(Pick(digit));
        sb.Append(Pick(special));
        for (var i = 0; i < 8; i++) sb.Append(Pick(all));

        // Shuffle so the guaranteed-class characters aren't always first.
        return new string(sb.ToString().OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());

        static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
    }

    private static InstitutionDto ToDto(Institution i) => new()
    {
        Id = i.Id, Name = i.Name, Type = i.Type, Prefix = i.Prefix,
        ContactEmail = i.ContactEmail, ContactPerson = i.ContactPerson,
        Status = i.Status, CreatedAt = i.CreatedAt,
        ApprovedAt = i.ApprovedAt, RejectionReason = i.RejectionReason
    };

    // Platform-admin drill-down. IgnoreQueryFilters so we read the target
    // institution's data regardless of the caller's (absent) tenant scope.
    public async Task<InstitutionDetailDto?> GetDetailAsync(Guid institutionId)
    {
        var inst = await _db.Institutions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == institutionId);
        if (inst is null) return null;

        var users = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.InstitutionId == institutionId)
            .ToListAsync();

        var examCount = await _db.Exams.IgnoreQueryFilters()
            .CountAsync(e => e.InstitutionId == institutionId);

        var admin = users.FirstOrDefault(u => u.Role == Constants.Roles.InstitutionAdmin);
        var lecturers = users.Where(u => u.Role == Constants.Roles.Lecturer).ToList();
        var students = users.Where(u => u.Role == Constants.Roles.Student).ToList();

        return new InstitutionDetailDto
        {
            Institution = ToDto(inst),
            Admin = admin is null ? null : ToUserDto(admin),
            Lecturers = lecturers.Count,
            Students = students.Count,
            Exams = examCount,
            RecentLecturers = lecturers.OrderByDescending(u => u.CreatedAt).Take(6).Select(ToUserDto).ToList(),
            RecentStudents = students.OrderByDescending(u => u.CreatedAt).Take(6).Select(ToUserDto).ToList()
        };
    }

    private static UserDto ToUserDto(User u) => new()
    {
        Id = u.Id, Name = u.Name, IdNumber = u.IdNumber, Role = u.Role,
        Department = u.Department, ProgrammeOfStudy = u.ProgrammeOfStudy,
        Level = u.Level, IsActive = u.IsActive, CreatedAt = u.CreatedAt, Email = u.Email
    };
}
