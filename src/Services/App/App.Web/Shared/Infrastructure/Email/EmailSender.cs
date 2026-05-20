using LayeredTemplate.App.Shared.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LayeredTemplate.App.Shared.Infrastructure.Email;

internal sealed class EmailSender : IEmailSender
{
    private readonly SmtpSettings smtpSettings;
    private readonly ILogger<EmailSender> logger;

    public EmailSender(IOptions<SmtpSettings> smtpSettings, ILogger<EmailSender> logger)
    {
        this.smtpSettings = smtpSettings.Value;
        this.logger = logger;
    }

    public Task SendEmail(string to, string subject, string htmlBody, CancellationToken cancellationToken = default) =>
        this.SendEmail([to], subject, htmlBody, cancellationToken);

    public async Task SendEmail(string[] tos, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        try
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(this.smtpSettings.From));
            email.To.AddRange(tos.Select(MailboxAddress.Parse));
            email.Subject = subject;
            email.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(this.smtpSettings.Host, this.smtpSettings.Port, SecureSocketOptions.StartTls, cancellationToken);
            await smtp.AuthenticateAsync(this.smtpSettings.User, this.smtpSettings.Password, cancellationToken);
            var response = await smtp.SendAsync(email, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);

            this.logger.LogInformation("Email {Subject} sent to {Recipients} | response: {Response}", subject, tos, response);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Send email exception");
        }
    }
}