using LayeredTemplate.App.Application.Common.Services;
using LayeredTemplate.App.Application.Features.Users.Services;
using LayeredTemplate.App.Infrastructure.Data;
using LayeredTemplate.App.Infrastructure.Extensions;
using LayeredTemplate.App.Infrastructure.Mocks.Services;
using LayeredTemplate.App.Infrastructure.Services.Common;
using LayeredTemplate.App.Infrastructure.Services.Users;
using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayeredTemplate.App.Infrastructure;

public static class ConfigureServices
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        services.RegisterDbContext(configuration[ConnectionStrings.DefaultConnection]!);
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
            .AddNpgSql(configuration[ConnectionStrings.DefaultConnection]!);
    }
}