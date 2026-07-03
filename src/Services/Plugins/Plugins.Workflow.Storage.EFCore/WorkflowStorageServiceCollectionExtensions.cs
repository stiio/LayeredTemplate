using System.Text.RegularExpressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// implementation, (by default) a hosted service that applies pending migrations on
    /// startup, and (by default) the LISTEN/NOTIFY work push — see
    /// <see cref="WorkflowEfCoreStorageOptions.EnableListenNotify"/>. The DbContext lives in
    /// the <c>workflow</c> Postgres schema and keeps its own migration history table there —
    /// independent from the consumer's app context.
    /// </summary>
    public static IWorkflowCoreBuilder AddEfCoreStorage(
        this IWorkflowCoreBuilder builder,
        string connectionString,
        bool autoMigrate = true,
        Action<WorkflowEfCoreStorageOptions>? configure = null)
    {
        var storageOptions = new WorkflowEfCoreStorageOptions();
        configure?.Invoke(storageOptions);

        // LISTEN takes the channel as a raw identifier (it can't be parameterised), so reject
        // anything that isn't one at composition time instead of quoting at runtime.
        if (storageOptions.EnableListenNotify
            && !Regex.IsMatch(storageOptions.ListenNotifyChannel, "^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            throw new ArgumentException(
                $"ListenNotifyChannel '{storageOptions.ListenNotifyChannel}' must be a plain identifier (letters / digits / underscore, not starting with a digit).",
                nameof(configure));
        }

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
            if (storageOptions.EnableListenNotify)
            {
                // Producer half of the work push: NOTIFY on every flush that makes steps
                // claimable. The consumer half is the WorkflowWorkListener hosted below.
                opts.AddInterceptors(new WorkflowWorkNotifyInterceptor(storageOptions.ListenNotifyChannel));
            }
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

        if (storageOptions.EnableListenNotify)
        {
            // Consumer half of the work push: one LISTEN connection per process pulsing the
            // engine's IWorkflowWorkSignal (registered by AddWorkflowCore) on notifications.
            builder.Services.AddHostedService(sp => new WorkflowWorkListener(
                connectionString,
                storageOptions.ListenNotifyChannel,
                sp.GetRequiredService<IWorkflowWorkSignal>(),
                sp.GetRequiredService<ILogger<WorkflowWorkListener>>()));
        }

        return builder;
    }
}
