using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// Default <see cref="IWorkflowStorageMigrator"/>. Wraps <c>Database.MigrateAsync</c> in a
/// Postgres session-scoped advisory lock so multi-instance startups serialise on the migration
/// runner — the second instance waits for the first to finish, then sees no pending migrations
/// and returns.
/// </summary>
internal class EfCoreWorkflowStorageMigrator : IWorkflowStorageMigrator
{
    /// <summary>
    /// Stable 64-bit lock key for the workflow plugin's migration runner. Picked from the upper
    /// half of long.MaxValue to avoid collisions with any consumer-side advisory locks (e.g.
    /// the consumer's own migration lock provider).
    /// </summary>
    private const long MigrationLockKey = 7_3737_9382_7300_0001L;

    private readonly WorkflowDbContext context;
    private readonly ILogger<EfCoreWorkflowStorageMigrator> logger;

    public EfCoreWorkflowStorageMigrator(
        WorkflowDbContext context,
        ILogger<EfCoreWorkflowStorageMigrator> logger)
    {
        this.context = context;
        this.logger = logger;
    }

    public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        // pg_advisory_lock blocks until obtained — paired siblings will queue here.
        await this.context.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_lock({MigrationLockKey});",
            cancellationToken);

        try
        {
            var pending = (await this.context.Database
                .GetPendingMigrationsAsync(cancellationToken))
                .ToList();
            if (pending.Count == 0)
            {
                this.logger.LogDebug("No pending workflow migrations.");
                return;
            }

            this.logger.LogInformation(
                "Applying {Count} pending workflow migration(s): {Migrations}",
                pending.Count, string.Join(", ", pending));

            await this.context.Database.MigrateAsync(cancellationToken);

            this.logger.LogInformation("Workflow migrations applied.");
        }
        finally
        {
            // Best-effort unlock with CT.None — if the caller's token is already cancelled
            // (shutdown mid-migration) we still want to release the advisory lock so the
            // next instance doesn't wait for connection recycling. Postgres will reclaim the
            // session-scoped lock on connection close anyway, but unlocking explicitly avoids
            // a stale handoff.
            try
            {
                await this.context.Database.ExecuteSqlRawAsync(
                    $"SELECT pg_advisory_unlock({MigrationLockKey});",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to release workflow migration advisory lock — Postgres will reclaim it on connection close.");
            }
        }
    }
}
