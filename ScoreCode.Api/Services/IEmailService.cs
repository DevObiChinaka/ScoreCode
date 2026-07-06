namespace ScoreCode.Api.Services;

/// <summary>
/// Outbound email seam. For now the platform admin conveys credentials manually,
/// so the default implementation only logs — a real SMTP/provider implementation
/// can be dropped in behind this interface later without changing callers.
/// </summary>
public interface IEmailService
{
    Task SendInstitutionApprovedAsync(
        string toEmail, string institutionName, string adminIdNumber, string temporaryPassword);
}

/// <summary>No-op implementation that logs (never logs the password).</summary>
public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;
    public LoggingEmailService(ILogger<LoggingEmailService> logger) => _logger = logger;

    public Task SendInstitutionApprovedAsync(
        string toEmail, string institutionName, string adminIdNumber, string temporaryPassword)
    {
        _logger.LogInformation(
            "[Email:stub] '{Institution}' approved — would email {Email} the admin id {AdminId} and a temporary password.",
            institutionName, toEmail, adminIdNumber);
        return Task.CompletedTask;
    }
}
