namespace ScoreCode.Shared.DTOs.Users;

public class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string ProgrammeOfStudy { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;

    /// <summary>
    /// Recovery address, or null. Only ever returned to admins and to the owner
    /// — a student's email would identify them to a lecturer.
    /// </summary>
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
