namespace ScoreCode.Shared.DTOs.Auth;

public class RegisterRequest
{
    public Guid   InstitutionId    { get; set; }   // chosen from the register dropdown
    public string MatricNumber    { get; set; } = string.Empty;
    public string ProgrammeOfStudy { get; set; } = string.Empty;
    public string Level            { get; set; } = string.Empty;  // "100".."500" or "Postgrad"
    /// <summary>Optional; used only to email a password-reset code.</summary>
    public string? Email          { get; set; }
    public string Password         { get; set; } = string.Empty;
    public string ConfirmPassword  { get; set; } = string.Empty;
}

