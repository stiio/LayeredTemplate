using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.Contexts;

public static class ServicesExtensions
{
    public static void RegisterDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextPool<AuthDbContext>(options =>
        {
            options
                .UseNpgsql(connectionString, x =>
                {
                    x.MigrationsHistoryTable("__ef_auth_migrations", "auth");

                    x.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
                    x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .UseSnakeCaseNamingConvention();
        });
    }
}