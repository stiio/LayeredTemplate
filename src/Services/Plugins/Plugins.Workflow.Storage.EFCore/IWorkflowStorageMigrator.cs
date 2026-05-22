namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// Manual hook for applying the plugin's pending EF migrations. The default registration via
/// <c>AddEfCoreStorage(connectionString)</c> wires an <see cref="Microsoft.Extensions.Hosting.IHostedService"/>
/// that calls this on startup — consumers that want explicit migration control (CI/CD gating,
/// custom locking around it, separate "migrate-only" entrypoint, …) can disable the hosted
/// service via <c>autoMigrate: false</c> and invoke this themselves.
/// </summary>
public interface IWorkflowStorageMigrator
{
    /// <summary>
    /// Apply all pending workflow migrations under a Postgres advisory lock so concurrent app
    /// instances starting in parallel don't race the migration runner. Idempotent — no-op when
    /// nothing pending.
    /// </summary>
    Task ApplyMigrationsAsync(CancellationToken cancellationToken = default);
}
