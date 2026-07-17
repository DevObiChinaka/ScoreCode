namespace ScoreCode.Shared.DTOs.Auth;

/// <summary>
/// Starts a password reset. If the account has an email on file a one-time code
/// is sent there; otherwise an administrator is asked to reset it.
/// </summary>
public class ForgotPasswordRequest
{
    public string IdNumber { get; set; } = string.Empty;
}

/// <summary>Completes a reset using the code that was emailed.</summary>
public class ResetWithOtpRequest
{
    public string IdNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
