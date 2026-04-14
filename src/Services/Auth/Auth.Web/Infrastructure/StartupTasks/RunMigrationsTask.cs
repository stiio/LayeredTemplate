using LayeredTemplate.Auth.Web.Infrastructure.Identity.Contexts;
using LayeredTemplate.Plugins.StartupRunner.Services;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Auth.Web.Infrastructure.StartupTasks;

public class RunMigrationsTask : IStartupTask
{
    private readonly AuthDbContext dbContext;
    private readonly ILogger<RunMigrationsTask> logger;

    public RunMigrationsTask(AuthDbContext dbContext, ILogger<RunMigrationsTask> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    public int Order => 10;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Start applying migrations for {DbContext}...", nameof(AuthDbContext));

        if ((await this.dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            await this.dbContext.Database.MigrateAsync(cancellationToken);
        }

        this.logger.LogInformation("Applying migrations for {DbContext} completed.", nameof(AuthDbContext));
    }
}