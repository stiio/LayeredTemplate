using LayeredTemplate.App.Shared.Infrastructure.Locks;
using LayeredTemplate.Plugins.StartupRunner.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredTemplate.App.Shared.Db;

/// <summary>
/// Applies pending EF Core migrations on startup, under a distributed advisory lock so only one
/// pod runs migrations in a multi-instance rollout. Other instances wait, then skip when the lock
/// is released (pending list is re-checked under the lock).
/// </summary>
internal sealed class RunMigrationsTask : IStartupTask
{
    private readonly AppDbContext context;
    private readonly ILogger<RunMigrationsTask> logger;
    private readonly ILockProvider lockProvider;

    public RunMigrationsTask(
        AppDbContext context,
        ILogger<RunMigrationsTask> logger,
        ILockProvider lockProvider)
    {
        this.context = context;
        this.logger = logger;
        this.lockProvider = lockProvider;
    }

    public int Order => 1;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var dbCreator = this.context.GetService<IRelationalDatabaseCreator>();

        this.logger.LogInformation("Start applying migrations...");

        if (await dbCreator.ExistsAsync(cancellationToken))
        {
            await using var @lock = await this.lockProvider.AcquireLockAsync(
                LockKey.Migrations(nameof(AppDbContext)),
                cancellationToken: cancellationToken);

            if ((await this.context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                await this.context.Database.MigrateAsync(cancellationToken);
            }
        }
        else
        {
            if ((await this.context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                await this.context.Database.MigrateAsync(cancellationToken);
            }
        }

        this.logger.LogInformation("Applying migrations completed.");
    }
}
