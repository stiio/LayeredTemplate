using LayeredTemplate.Auth.Web.Infrastructure.Options.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LayeredTemplate.Auth.Web.Infrastructure.Email.Services;

public class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> logger;
    private readonly SmtpSettings smtpSettings;

    public EmailSender(ILogger<EmailSender> logger, IOptions<SmtpSettings> smtpSettings)
    {
        this.logger = logger;
        this.smtpSettings = smtpSettings.Value;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        using var scope = this.logger.BeginScope(new Dictionary<string, object>()
        {
            ["RecipientEmail"] = email,
            ["EmailSubject"] = subject,
            ["EmailBody"] = htmlMessage,
            ["IsMockEmailSender"] = false,
        });

        try
        {
            this.logger.LogInformation("Sending email...");

            // create message
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(this.smtpSettings.From));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlMessage,
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(this.smtpSettings.Host, this.smtpSettings.Port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(this.smtpSettings.User, this.smtpSettings.Password);
            var response = await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);

            this.logger.LogInformation("Email sent with response: {Response}", response);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Error sending email.");
        }
    }
}