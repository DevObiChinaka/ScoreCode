namespace ScoreCode.Database.Entities;

/// <summary>
/// A tenant: a school / university / learning centre that registers on the
/// platform, is verified + approved by a platform admin, and under which its
/// own admins, lecturers and students operate.
/// </summary>
public class Institution
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;      // matric/staff-id prefix, e.g. "ADU"
    public string Type { get; set; } = "University";         // University | Polytechnic | College | Learning Centre
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";          // see Constants.InstitutionStatuses
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    // Navigation
    public ICollection<User> Users { get; set; } = [];
}
