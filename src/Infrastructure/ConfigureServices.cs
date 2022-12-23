using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Infrastructure.Data;
using LayeredTemplate.Infrastructure.Extensions;
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
        services.AddScoped<IUserPoolService, UserPoolService>();
    }
}