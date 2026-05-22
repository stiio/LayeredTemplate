using System.Reflection;
using LayeredTemplate.App.Setup.StartupTasks;
using LayeredTemplate.App.Shared.Db;
using LayeredTemplate.App.Shared.Db.Interceptors;
using LayeredTemplate.App.Shared.Options;
using LayeredTemplate.Plugins.StartupRunner;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.App.Setup;

public static class ConfigureDb
{
    /// <summary>
    /// Wires the single <see cref="AppDbContext"/>, snake_case naming, migrations history,
    /// query splitting, and DataProtection key persistence into the same DB.
    /// </summary>
    public static IServiceCollection AddAppDb(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration[ConnectionStringKeys.WriteDb]
            ?? throw new InvalidOperationException($"Configuration key '{ConnectionStringKeys.WriteDb}' is required.");

        services.AddDbContextPool<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, x =>
                {
                    x.MigrationsHistoryTable("__ef_backend_migrations");
                    x.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
                    x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .UseSnakeCaseNamingConvention();

            options.AddInterceptors(new BaseEntitySaveChangesInterceptor());
        });

        services.AddStartupTask<RunMigrationsTask>();

        return services;
    }
}
