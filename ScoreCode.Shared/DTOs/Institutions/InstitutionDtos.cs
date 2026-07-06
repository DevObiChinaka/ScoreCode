namespace ScoreCode.Shared.DTOs.Institutions;

/// <summary>Public application submitted by a school/learning-centre.</summary>
public class InstitutionApplicationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "University";
    public string Prefix { get; set; } = string.Empty;      // matric/staff-id prefix
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
}

/// <summary>Full institution record (platform-admin views).</summary>
public class InstitutionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>Minimal, public entry for the student registration dropdown.</summary>
public class InstitutionOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
}

public class RejectInstitutionRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Returned to the platform admin on approval so they can convey the initial
/// institution-admin credentials. The temporary password is shown ONCE.
/// </summary>
public class ApproveInstitutionResponse
{
    public Guid InstitutionId { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public string AdminIdNumber { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
}
