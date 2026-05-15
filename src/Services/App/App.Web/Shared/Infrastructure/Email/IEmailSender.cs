namespace LayeredTemplate.App.Shared.Infrastructure.Email;

public interface IEmailSender
{
    Task SendEmail(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);

    Task SendEmail(string[] tos, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
