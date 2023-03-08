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
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options
                .UseNpgsql(connectionString, x =>
                {
                    x.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
                    x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
    }
}