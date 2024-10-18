using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LayeredTemplate.App.Infrastructure.Data.Context;

internal class ApplicationDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Port=5432;Database=appDbName;Username=postgres;Password=postgres;", x =>
            {
                x.MigrationsHistoryTable("__ef_backend_migrations");

                x.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
                x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .UseSnakeCaseNamingConvention();

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}