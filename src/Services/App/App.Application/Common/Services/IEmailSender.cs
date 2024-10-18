namespace LayeredTemplate.App.Application.Common.Services;

public interface IEmailSender
{
    Task SendEmail(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);

    Task SendEmail(string[] tos, string subject, string htmlBody, CancellationToken cancellationToken = default);
}