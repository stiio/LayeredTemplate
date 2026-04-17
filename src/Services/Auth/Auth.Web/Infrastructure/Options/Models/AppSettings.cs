namespace LayeredTemplate.Auth.Web.Infrastructure.Options.Models;

public class AppSettings
{
    public bool UseMockEmailSender { get; set; } = false;

    public bool UseMockSmsSender { get; set; } = false;

    public bool IsPhoneConfirmationEnabled { get; set; } = false;

    public bool IsDeletePersonalDataEnabled { get; set; } = false;
}