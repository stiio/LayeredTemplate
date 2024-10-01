using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Shared.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LayeredTemplate.Infrastructure.Services.Common;

internal class EmailSender : IEmailSender
{
    private readonly SmtpSettings smtpSettings;
    private readonly ILogger<EmailSender> logger;

    public EmailSender(
        IOptions<SmtpSettings> smtpSettings,
        ILogger<EmailSender> logger)
    {
        this.smtpSettings = smtpSettings.Value;
        this.logger = logger;
    }

    public Task SendEmail(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        return this.SendEmail(new[] { to }, subject, htmlBody, cancellationToken);
    }

    public async Task SendEmail(string[] tos, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        try
        {
            // create message
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(this.smtpSettings.From));
            email.To.AddRange(tos.Select(MailboxAddress.Parse));
            email.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody,
            };

            email.Body = bodyBuilder.ToMessageBody();

            // send email
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(this.smtpSettings.Host, this.smtpSettings.Port, SecureSocketOptions.StartTls, cancellationToken);
            await smtp.AuthenticateAsync(this.smtpSettings.User, this.smtpSettings.Password, cancellationToken);
            var response = await smtp.SendAsync(email, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);

            this.logger.LogInformation("Email {subject} send to {@tos} | response: {response}", subject, tos, response);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Send email exception");
        }
    }
}