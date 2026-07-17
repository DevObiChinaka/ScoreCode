using System.Net;
using System.Net.Mail;

namespace ScoreCode.Api.Services;

/// <summary>Strongly-typed <c>Email</c> configuration section.</summary>
public class EmailOptions
{
    public const string Section = "Email";

    /// <summary>When false (the default) nothing is sent and the logging stub is used.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;

    /// <summary>SMTP username — usually the full email address.</summary>
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// SMTP password. Never commit this: set it with user-secrets or the
    /// Email__Password environment variable. Gmail requires an App Password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Sender address. Defaults to <see cref="User"/> when blank.</summary>
    public string From { get; set; } = string.Empty;
    public string FromName { get; set; } = "ScoreCode";
}

/// <summary>
/// Sends mail over SMTP. Delivery failures are logged and swallowed: approving an
/// institution must not fail because a mail server was unreachable, and the admin
/// is shown the credentials on screen regardless.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(EmailOptions options, ILogger<SmtpEmailService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task SendInstitutionApprovedAsync(
        string toEmail, string institutionName, string adminIdNumber, string temporaryPassword)
    {
        var subject = $"{institutionName} is approved on ScoreCode";
        var body =
            $"Hello,\n\n" +
            $"{institutionName} has been approved on ScoreCode.\n\n" +
            $"Your administrator account:\n" +
            $"  ID number: {adminIdNumber}\n" +
            $"  Temporary password: {temporaryPassword}\n\n" +
            $"You'll be asked to set your own password the first time you sign in.\n\n" +
            $"— ScoreCode";

        await SendAsync(toEmail, subject, body);
    }

    private async Task SendAsync(string toEmail, string subject, string body)
    {
        var from = string.IsNullOrWhiteSpace(_options.From) ? _options.User : _options.From;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(from, _options.FromName),
                Subject = subject,
                Body = body
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseStartTls,
                Credentials = new NetworkCredential(_options.User, _options.Password)
            };

            await client.SendMailAsync(message);
            _logger.LogInformation("Sent '{Subject}' to {Email}.", subject, toEmail);
        }
        catch (Exception ex)
        {
            // Logged without the message body, which carries the temporary password.
            _logger.LogError(ex, "Could not email {Email} — '{Subject}' was not delivered.", toEmail, subject);
        }
    }
}
