using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LayeredTemplate.Auth.Web.Infrastructure.Data.Contexts;

public class AuthDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Port=5432;Database=authDbName;Username=postgres;Password=postgres;", x =>
            {
                x.MigrationsHistoryTable("__ef_auth_migrations");

                x.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
                x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .UseSnakeCaseNamingConvention();

        return new AuthDbContext(optionsBuilder.Options);
    }
}