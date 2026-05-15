using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LayeredTemplate.App.Shared.Db;

/// <summary>
/// Used by EF Core tools (<c>dotnet ef migrations add ...</c>) when the host can't be started.
/// Hard-coded connection string; only the schema model is needed for migration generation.
/// </summary>
internal sealed class AppDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Port=5432;Database=appDbName;Username=postgres;Password=postgres;", x =>
            {
                x.MigrationsHistoryTable("__ef_backend_migrations");
                x.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .UseSnakeCaseNamingConvention();

        return new AppDbContext(optionsBuilder.Options);
    }
}
