using LayeredTemplate.Infrastructure.Data.Context;
using LayeredTemplate.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Infrastructure.Data.Services;

internal class RunMigrationsTask : IStartupTask
{
    private readonly ApplicationDbContext context;
    private readonly ILogger<RunMigrationsTask> logger;

    public RunMigrationsTask(ApplicationDbContext context, ILogger<RunMigrationsTask> logger)
    {
        this.context = context;
        this.logger = logger;
    }

    public int Order => 10;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if ((await this.context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            await this.context.Database.MigrateAsync(cancellationToken);
        }

        this.logger.LogInformation("Migrations Complete.");
    }
}