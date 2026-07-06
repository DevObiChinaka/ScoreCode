namespace ScoreCode.Shared.DTOs.Users;

public class UpdateStudentProfileRequest
{
    public string? MatricNumber { get; set; }
    public string? ProgrammeOfStudy { get; set; }
    public string? Level { get; set; }
    public string? Department { get; set; }
}
