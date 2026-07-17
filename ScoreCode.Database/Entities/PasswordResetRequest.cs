namespace ScoreCode.Database.Entities;

/// <summary>
/// A "forgot password" request. Users have no email on file, so a request is
/// routed to an admin (their institution admin; the platform admin for
/// institution admins) who resets the password and hands over the temporary one.
/// </summary>
public class PasswordResetRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";   // see Constants.ResetRequestStatuses
    public DateTime? HandledAt { get; set; }

    // Tenant — mirrors the requesting user's institution (null for platform admins)
    public Guid? InstitutionId { get; set; }
    public Institution? Institution { get; set; }

    public User User { get; set; } = null!;
}
