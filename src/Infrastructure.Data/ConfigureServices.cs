using System.Reflection;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Infrastructure.Data.Context;
using LayeredTemplate.Infrastructure.Data.Interceptors;
using LayeredTemplate.Infrastructure.Data.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure.Data;

public static class ConfigureServices
{
    public static void RegisterDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextPool<ApplicationDbContext>(options =>
        {
            options
                .UseNpgsql(connectionString, x =>
                {
                    x.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
                    x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .UseSnakeCaseNamingConvention();

            options.AddInterceptors(new BaseEntitySaveChangesInterceptor());
        });

        services.AddDbContextFactory<ApplicationDbContext>();

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IApplicationDbConnection>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IApplicationDbContextFactory, ApplicationDbContextFactory>();

        services.AddDataProtection()
            .PersistKeysToDbContext<ApplicationDbContext>();
    }
}