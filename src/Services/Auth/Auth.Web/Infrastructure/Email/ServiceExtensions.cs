using LayeredTemplate.Auth.Web.Infrastructure.Email.Services;
using LayeredTemplate.Auth.Web.Infrastructure.Options.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Email;

public static class ServiceExtensions
{
    public static void AddAppEmailServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        var appSettings = configuration.GetSection(nameof(AppSettings)).Get<AppSettings>();
        services.AddScoped<IUserEmailSender, UserEmailSender>();
        if (appSettings!.UseMockEmailSender)
        {
            services.AddSingleton<IEmailSender, MockEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, EmailSender>();
        }
    }
}