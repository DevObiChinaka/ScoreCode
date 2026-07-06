namespace ScoreCode.Shared.DTOs.Users;

public class CreateLecturerRequest
{
    public string Name { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
