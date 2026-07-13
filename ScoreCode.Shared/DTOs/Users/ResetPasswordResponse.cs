namespace ScoreCode.Shared.DTOs.Users;

/// <summary>Returned when an admin resets a user's password. Shown once.</summary>
public class ResetPasswordResponse
{
    public string TemporaryPassword { get; set; } = string.Empty;
}
