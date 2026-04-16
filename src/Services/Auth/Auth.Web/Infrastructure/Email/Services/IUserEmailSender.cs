using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;

namespace LayeredTemplate.Auth.Web.Infrastructure.Email.Services;

public interface IUserEmailSender
{
    Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink);

    Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink);
}