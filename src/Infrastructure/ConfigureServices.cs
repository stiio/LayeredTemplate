using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Infrastructure.AuthorizationHandlers;
using LayeredTemplate.Infrastructure.Data;
using LayeredTemplate.Infrastructure.Services;
using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure;

public static class ConfigureServices
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterDbContext(configuration[ConnectionStrings.DefaultConnection]!);

        services.AddScoped<IResourceAuthorizationService, ResourceAuthorizationService>();
        services.AddScoped<IAuthorizationHandler, TodoListAuthorizationHandler>();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
    }
}