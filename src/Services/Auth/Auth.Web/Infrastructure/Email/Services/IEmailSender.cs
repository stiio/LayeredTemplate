namespace LayeredTemplate.Auth.Web.Infrastructure.Email.Services;

public interface IEmailSender
{
    Task SendEmailAsync(string email, string subject, string htmlMessage);
}