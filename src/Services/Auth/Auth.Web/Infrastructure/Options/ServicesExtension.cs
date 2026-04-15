using LayeredTemplate.Auth.Web.Infrastructure.Options.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Options;

public static class ServicesExtension
{
    public static void AddAppSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));
        services.Configure<SmtpSettings>(configuration.GetSection(nameof(SmtpSettings)));
        services.Configure<ReCaptchaSettings>(configuration.GetSection(nameof(ReCaptchaSettings)));
    }
}