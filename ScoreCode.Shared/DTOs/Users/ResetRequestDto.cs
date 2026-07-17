namespace ScoreCode.Shared.DTOs.Users;

/// <summary>A pending "forgot password" request shown to an admin.</summary>
public class ResetRequestDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string IdNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
}
