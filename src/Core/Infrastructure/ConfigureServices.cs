using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Application.Features.Users.Services;
using LayeredTemplate.Infrastructure.Data;
using LayeredTemplate.Infrastructure.Extensions;
using LayeredTemplate.Infrastructure.Mocks.Services;
using LayeredTemplate.Infrastructure.Services.Common;
using LayeredTemplate.Infrastructure.Services.Users;
using LayeredTemplate.Shared.Constants;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayeredTemplate.Infrastructure;

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