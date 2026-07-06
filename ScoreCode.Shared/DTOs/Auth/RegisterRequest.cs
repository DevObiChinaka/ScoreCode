namespace ScoreCode.Shared.DTOs.Auth;

public class RegisterRequest
{
    public string MatricNumber    { get; set; } = string.Empty;
    public string ProgrammeOfStudy { get; set; } = string.Empty;
    public int    Level            { get; set; }
    public string Password         { get; set; } = string.Empty;
    public string ConfirmPassword  { get; set; } = string.Empty;
}

