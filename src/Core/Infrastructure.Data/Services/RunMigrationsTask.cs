using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Infrastructure.Data.Services;

internal class RunMigrationsTask<TDbContext> : IStartupTask
    where TDbContext : DbContext
{
    private readonly TDbContext context;
    private readonly ILogger<RunMigrationsTask<TDbContext>> logger;
    private readonly ILockProvider lockProvider;

    public RunMigrationsTask(
        TDbContext context,
        ILogger<RunMigrationsTask<TDbContext>> logger,
        ILockProvider lockProvider)
    {
        this.context = context;
        this.logger = logger;
        this.lockProvider = lockProvider;
    }

    public int Order => 1;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var dbContextName = typeof(TDbContext).Name;
        var dbCreator = this.context.GetService<IRelationalDatabaseCreator>();

        this.logger.LogInformation("Start applying migrations for {DbContext}...", dbContextName);

        if (await dbCreator.ExistsAsync(cancellationToken))
        {
            await using var @lock = await this.lockProvider.AcquireLockAsync($"migrations:{dbContextName}", cancellationToken: cancellationToken);

            if ((await this.context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                await this.context.Database.MigrateAsync(cancellationToken);
            }

            await @lock.DisposeAsync();
        }
        else
        {
            if ((await this.context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                await this.context.Database.MigrateAsync(cancellationToken);
            }
        }

        this.logger.LogInformation("Applying migrations for {DbContext} completed.", dbContextName);
    }
}