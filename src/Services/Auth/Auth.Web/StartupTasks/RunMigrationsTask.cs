using LayeredTemplate.Auth.Web.Data;
using LayeredTemplate.Plugins.StartupRunner.Services;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Auth.Web.StartupTasks;

public class RunMigrationsTask : IStartupTask
{
    private readonly ApplicationDbContext dbContext;
    private readonly ILogger<RunMigrationsTask> logger;

    public RunMigrationsTask(ApplicationDbContext dbContext, ILogger<RunMigrationsTask> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    public int Order => 10;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Start applying migrations for {DbContext}...", nameof(ApplicationDbContext));

        if ((await this.dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            await this.dbContext.Database.MigrateAsync(cancellationToken);
        }

        this.logger.LogInformation("Applying migrations for {DbContext} completed.", nameof(ApplicationDbContext));
    }
}