namespace ScoreCode.Api.Services;

/// <summary>
/// Outbound email seam. For now the platform admin conveys credentials manually,
/// so the default implementation only logs — a real SMTP/provider implementation
/// can be dropped in behind this interface later without changing callers.
/// </summary>
public interface IEmailService
{
    /// <summary>True when mail can actually be delivered; false for the logging stub.</summary>
    bool IsEnabled { get; }

    Task SendInstitutionApprovedAsync(
        string toEmail, string institutionName, string adminIdNumber, string temporaryPassword);

    /// <summary>Send a one-time password-reset code.</summary>
    Task SendPasswordResetCodeAsync(string toEmail, string idNumber, string code, int expiryMinutes);
}

/// <summary>No-op implementation that logs (never logs the password).</summary>
public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;
    public LoggingEmailService(ILogger<LoggingEmailService> logger) => _logger = logger;

    /// <summary>Nothing is delivered, so callers must route around email.</summary>
    public bool IsEnabled => false;

    public Task SendInstitutionApprovedAsync(
        string toEmail, string institutionName, string adminIdNumber, string temporaryPassword)
    {
        _logger.LogInformation(
            "[Email:stub] '{Institution}' approved — would email {Email} the admin id {AdminId} and a temporary password.",
            institutionName, toEmail, adminIdNumber);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(string toEmail, string idNumber, string code, int expiryMinutes)
    {
        // The code is deliberately not logged — it would be a reset token in plain text.
        _logger.LogInformation(
            "[Email:stub] would email {Email} a password-reset code for {IdNumber}.", toEmail, idNumber);
        return Task.CompletedTask;
    }
}
