using LayeredTemplate.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Infrastructure.Data.Services;

internal class RunMigrationsTask<TDbContext> : IStartupTask
    where TDbContext : DbContext
{
    private readonly TDbContext context;
    private readonly ILogger<TDbContext> logger;

    public RunMigrationsTask(
        TDbContext context,
        ILogger<TDbContext> logger)
    {
        this.context = context;
        this.logger = logger;
    }

    public int Order => 1;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var dbContextName = typeof(TDbContext).Name;

        this.logger.LogInformation("Start applying migrations for {DbContext}...", dbContextName);

        if ((await this.context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            await this.context.Database.MigrateAsync(cancellationToken);
        }

        this.logger.LogInformation("Applying migrations for {DbContext} completed.", dbContextName);
    }
}