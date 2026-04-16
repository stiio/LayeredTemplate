using LayeredTemplate.Auth.Web.Infrastructure.Data.Contexts;
using LayeredTemplate.Auth.Web.Infrastructure.Locks;
using LayeredTemplate.Plugins.StartupRunner.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredTemplate.Auth.Web.Infrastructure.StartupTasks;

public class RunMigrationsTask : IStartupTask
{
    private readonly AuthDbContext dbContext;
    private readonly ILogger<RunMigrationsTask> logger;
    private readonly ILockProvider lockProvider;

    public RunMigrationsTask(AuthDbContext dbContext, ILogger<RunMigrationsTask> logger, ILockProvider lockProvider)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.lockProvider = lockProvider;
    }

    public int Order => 10;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Start applying migrations for {DbContext}...", nameof(AuthDbContext));
        var dbCreator = this.dbContext.GetService<IRelationalDatabaseCreator>();

        if (await dbCreator.ExistsAsync(cancellationToken))
        {
            await using var @lock = await this.lockProvider.AcquireLockAsync(
                "run-migrations:auth-db-context",
                timeout: TimeSpan.FromSeconds(60),
                cancellationToken: cancellationToken);

            if ((await this.dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                await this.dbContext.Database.MigrateAsync(cancellationToken);
            }
        }
        else
        {
            if ((await this.dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                await this.dbContext.Database.MigrateAsync(cancellationToken);
            }
        }

        this.logger.LogInformation("Applying migrations for {DbContext} completed.", nameof(AuthDbContext));
    }
}