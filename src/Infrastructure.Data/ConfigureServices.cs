using System.Reflection;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Infrastructure.Data.Context;
using LayeredTemplate.Infrastructure.Data.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure.Data;

public static class ConfigureServices
{
    public static void RegisterDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<BaseEntitySaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options
                .UseNpgsql(connectionString, x =>
                {
                    x.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
                    x.EnableRetryOnFailure(5);
                })
                .UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
    }
}