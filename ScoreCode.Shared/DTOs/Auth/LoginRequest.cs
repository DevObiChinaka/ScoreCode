namespace ScoreCode.Shared.DTOs.Auth;

public class LoginRequest
{
    public string IdNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
