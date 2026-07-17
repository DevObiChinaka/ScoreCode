namespace ScoreCode.Shared.DTOs.Auth;

/// <summary>Raises a password-reset request for an admin to action.</summary>
public class ForgotPasswordRequest
{
    public string IdNumber { get; set; } = string.Empty;
}
