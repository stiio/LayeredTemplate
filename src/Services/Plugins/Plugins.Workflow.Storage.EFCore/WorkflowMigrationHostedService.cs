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
            // Log with full context, then rethrow to fail host startup. Running without the
            // workflow schema would leave the engine worker looping on "relation does not exist"
            // errors; a crashed start is the loud, orchestrator-visible signal (restart policy /
            // deploy rollback) that lets the operator inspect logs, fix, and redeploy.
            this.logger.LogError(ex, "Workflow migrations failed to apply on startup.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
