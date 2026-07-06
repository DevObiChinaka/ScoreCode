using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ScoreCode.Database;
using ScoreCode.Database.Entities;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs.Institutions;

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
}
