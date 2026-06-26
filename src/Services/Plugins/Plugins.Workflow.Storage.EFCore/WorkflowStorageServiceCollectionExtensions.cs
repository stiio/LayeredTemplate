using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// Wires the EF Core storage impl. Postgres-only by design — the engine relies on
/// vendor-specific primitives (FOR UPDATE SKIP LOCKED, jsonb concat, advisory locks) that have
/// no portable equivalent. Pretending the consumer can pick a provider would be misleading;
/// we take the connection string and own the rest.
/// <code>
/// services.AddWorkflowCore(configuration)
///         .AddEfCoreStorage(connectionString);
/// </code>
/// Pass <c>autoMigrate: false</c> if your bootstrap already runs migrations explicitly via
/// <see cref="IWorkflowStorageMigrator"/>; otherwise a hosted service applies them on startup
/// under a Postgres advisory lock so multi-instance starts don't race.
/// </summary>
public static class WorkflowStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers the plugin's <c>WorkflowDbContext</c>, the <see cref="IWorkflowStore"/>
    /// implementation, and (by default) a hosted service that applies pending migrations on
    /// startup. The DbContext lives in the <c>workflow</c> Postgres schema and keeps its own
    /// migration history table there — independent from the consumer's app context.
    /// </summary>
    public static IWorkflowCoreBuilder AddEfCoreStorage(
        this IWorkflowCoreBuilder builder,
        string connectionString,
        bool autoMigrate = true)
    {
        builder.Services.AddDbContext<WorkflowDbContext>(opts =>
        {
            opts.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(WorkflowDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", WorkflowDbContext.SchemaName);
            });
            // No naming convention package — every column/index/PK/FK is named explicitly in
            // the entity configurations. Locks the schema contract at the type level instead of
            // deriving it from a third-party convention.
            // PHI encryption converters are wired in WorkflowDbContext.OnModelCreating from its
            // ctor-injected (optional) IWorkflowDataProtector — no save interceptor needed.
        });

        // EfCoreWorkflowStore implements all three interfaces; we register the impl once as
        // scoped, then re-bind the narrower views to the same instance via factory delegate.
        // This way IWorkflowReadStore (App read handlers), IWorkflowRetentionStore (retention
        // worker), and IWorkflowStore (engine internals) all resolve to the same scoped object —
        // sharing the DbContext / change tracker / unit of work within one request.
        builder.Services.AddScoped<EfCoreWorkflowStore>();
        builder.Services.AddScoped<IWorkflowStore>(sp => sp.GetRequiredService<EfCoreWorkflowStore>());
        builder.Services.AddScoped<IWorkflowReadStore>(sp => sp.GetRequiredService<EfCoreWorkflowStore>());
        builder.Services.AddScoped<IWorkflowRetentionStore>(sp => sp.GetRequiredService<EfCoreWorkflowStore>());

        builder.Services.AddScoped<IWorkflowStorageMigrator, EfCoreWorkflowStorageMigrator>();

        if (autoMigrate)
        {
            builder.Services.AddHostedService<WorkflowMigrationHostedService>();
        }

        return builder;
    }
}
