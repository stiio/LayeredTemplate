using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Infrastructure.Data;
using LayeredTemplate.Infrastructure.Extensions;
using LayeredTemplate.Infrastructure.Mocks.Services;
using LayeredTemplate.Infrastructure.Services;
using LayeredTemplate.Shared.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure;

public static class ConfigureServices
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterDbContext(configuration[ConnectionStrings.DefaultConnection]!);

        services.ConfigureAuthentication(configuration);
        services.ConfigureAuthorization();
        services.ConfigureAwsServices(configuration);

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserManager, UserManager>();

        if (configuration.GetValue<bool>("MOCK_USER_POOL"))
        {
            services.AddScoped<IUserPoolService, UserPoolServiceMock>();
        }
        else
        {
            services.AddScoped<IUserPoolService, UserPoolService>();
        }

        if (configuration.GetValue<bool>("MOCK_EMAIL_SENDER"))
        {
            services.AddScoped<IEmailSender, EmailSenderMock>();
        }
        else
        {
            services.AddScoped<IEmailSender, EmailSender>();
        }

        services.AddSingleton<IQueueService, QueueServiceMemory>();
    }
}