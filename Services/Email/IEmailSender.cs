namespace StartupConnect.Services.Email;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);

/// <summary>
/// Sends transactional email. Implementations: <see cref="SmtpEmailSender"/> (real SMTP) and
/// <see cref="DevOutboxEmailSender"/> (logs + writes to the dev outbox, viewable at /Dev/Outbox).
/// Build messages with <see cref="EmailTemplates"/> so they share the branded layout.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
