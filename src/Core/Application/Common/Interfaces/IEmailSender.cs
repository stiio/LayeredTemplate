namespace LayeredTemplate.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendEmail(string to, string subject, string htmlBody);

    Task SendEmail(string[] tos, string subject, string htmlBody);
}