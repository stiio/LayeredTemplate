using System.Reflection;
using LayeredTemplate.App.Application.Common.Services;
using LayeredTemplate.App.Infrastructure.Data.Context;
using LayeredTemplate.App.Infrastructure.Data.Interceptors;
using LayeredTemplate.App.Infrastructure.Data.Services;
using LayeredTemplate.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.App.Infrastructure.Data;

public static class ConfigureServices
{
    public static void RegisterDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextPool<ApplicationDbContext>(options =>
        {
            options
                .UseNpgsql(connectionString, x =>
                {
                    x.MigrationsHistoryTable("__ef_backend_migrations");

                    x.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
                    x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .UseSnakeCaseNamingConvention();

            options.AddInterceptors(new BaseEntitySaveChangesInterceptor());
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IApplicationDbConnection>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddDataProtection()
            .PersistKeysToDbContext<ApplicationDbContext>();

        services.AddStartupTask<RunMigrationsTask<ApplicationDbContext>>();
    }
}