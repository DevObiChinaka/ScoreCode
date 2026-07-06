namespace ScoreCode.Shared.DTOs.Auth;

public class RegisterRequest
{
    public Guid   InstitutionId    { get; set; }   // chosen from the register dropdown
    public string MatricNumber    { get; set; } = string.Empty;
    public string ProgrammeOfStudy { get; set; } = string.Empty;
    public int    Level            { get; set; }
    public string Password         { get; set; } = string.Empty;
    public string ConfirmPassword  { get; set; } = string.Empty;
}

