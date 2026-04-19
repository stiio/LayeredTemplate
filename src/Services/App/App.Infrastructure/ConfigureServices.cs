using LayeredTemplate.App.Application.Common.Services;
using LayeredTemplate.App.Application.Features.Users.Services;
using LayeredTemplate.App.Infrastructure.Authorization;
using LayeredTemplate.App.Infrastructure.Data;
using LayeredTemplate.App.Infrastructure.Email;
using LayeredTemplate.App.Infrastructure.Locks;
using LayeredTemplate.App.Infrastructure.Users.Services;
using LayeredTemplate.App.Shared;
using LayeredTemplate.App.Shared.Constants;
using LayeredTemplate.Plugins.StartupRunner;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayeredTemplate.App.Infrastructure;

public static class ConfigureServices
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        services.AddPluginOptions(configuration);
        services.AddPluginStartupRunner();

        services.RegisterDbContext(configuration[ConnectionStrings.WriteDbConnection]!);

        services.AddDataProtection()
            .SetApplicationName("LayeredTemplate.App")
            .PersistKeysToAppDbContext();

        services.ConfigureAuthentication(configuration);
        services.ConfigureAuthorization();

        services.AddSingleton<ILockProvider, PostgresLockProvider>();

        services.AddScoped<ICurrentUserService, CurrentUserService>();

        if (configuration.GetValue<bool>("MOCK_EMAIL_SENDER"))
        {
            services.AddScoped<IEmailSender, EmailSenderMock>();
        }
        else
        {
            services.AddScoped<IEmailSender, EmailSender>();
        }

        services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromMinutes(1);
        });

        services.AddHealthChecks()
            .AddNpgSql(configuration[ConnectionStrings.WriteDbConnection]!);
    }
}