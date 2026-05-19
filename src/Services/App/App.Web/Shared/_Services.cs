using System.Reflection;
using FluentValidation;
using LayeredTemplate.App.Shared.Infrastructure.Email;
using LayeredTemplate.App.Shared.Infrastructure.Locks;
using LayeredTemplate.App.Shared.Options;

namespace LayeredTemplate.App.Shared;

public static class SharedServices
{
    public static void AddSharedServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));
        services.Configure<SmtpSettings>(configuration.GetSection(nameof(SmtpSettings)));

        services.AddSingleton<ILockProvider, PostgresLockProvider>();

        services.AddScoped<Auth.ICurrentUserService, Auth.CurrentUserService>();

        if (configuration.GetValue<bool>("MOCK_EMAIL_SENDER"))
        {
            services.AddScoped<IEmailSender, EmailSenderMock>();
        }
        else
        {
            services.AddScoped<IEmailSender, EmailSender>();
        }

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true, lifetime: ServiceLifetime.Singleton);
    }
}