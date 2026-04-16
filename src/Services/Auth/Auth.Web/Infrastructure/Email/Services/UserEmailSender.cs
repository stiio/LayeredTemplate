using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;

namespace LayeredTemplate.Auth.Web.Infrastructure.Email.Services;

public class UserEmailSender : IUserEmailSender
{
    private readonly IEmailSender emailSender;

    public UserEmailSender(IEmailSender emailSender)
    {
        this.emailSender = emailSender;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        return this.emailSender.SendEmailAsync(
            email,
            "Confirm your email",
            $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        return this.emailSender.SendEmailAsync(
            email,
            "Reset your password",
            $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        return this.emailSender.SendEmailAsync(
            email,
            "Reset your password",
            $"Please reset your password using the following code: {resetCode}");
    }
}