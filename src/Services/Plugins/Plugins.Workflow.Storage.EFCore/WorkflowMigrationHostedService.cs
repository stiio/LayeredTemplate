using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// Runs <see cref="IWorkflowStorageMigrator.ApplyMigrationsAsync"/> on startup. Registered by
/// <c>AddEfCoreStorage(...)</c> when <c>autoMigrate</c> is true (the default). Consumers that
/// prefer to control migrations from their own startup pipeline pass <c>autoMigrate: false</c>
/// and call <see cref="IWorkflowStorageMigrator"/> themselves.
/// </summary>
internal class WorkflowMigrationHostedService : IHostedService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<WorkflowMigrationHostedService> logger;

    public WorkflowMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkflowMigrationHostedService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = this.scopeFactory.CreateAsyncScope();
            var migrator = scope.ServiceProvider.GetRequiredService<IWorkflowStorageMigrator>();
            await migrator.ApplyMigrationsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Surface the failure but don't crash the host — without retries the app can't recover
            // automatically anyway, and the operator wants a chance to inspect logs / fix and
            // restart. The hosted services after us will still try to run; the worker won't make
            // progress until migrations are present, which is the desired loud-fail behaviour.
            this.logger.LogError(ex, "Workflow migrations failed to apply on startup.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
