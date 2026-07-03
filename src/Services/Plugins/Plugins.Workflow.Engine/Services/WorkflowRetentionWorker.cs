using System.Diagnostics;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Low-frequency background worker that calls <see cref="IWorkflowRetentionStore.PurgeFinishedRunsAsync"/>
/// and <see cref="IWorkflowRetentionStore.FailStaleRunningRunsAsync"/> on a configurable schedule.
/// Always registered; effectively dormant unless at least one of
/// <see cref="WorkflowRetentionSettings.EnableFinishedPurge"/> /
/// <see cref="WorkflowRetentionSettings.EnableStaleFail"/> is set in
/// <see cref="WorkflowEngineSettings.Retention"/>.
/// <para>
/// Scoped per sweep: each iteration creates its own DI scope for the store, so a long-running
/// process doesn't accumulate change-tracker entries between sweeps. Sweep loops over batches
/// (<see cref="WorkflowRetentionSettings.BatchSize"/>) until the backlog is drained — useful
/// when retention is first turned on against a system with months of accumulated history.
/// </para>
/// </summary>
internal class WorkflowRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IHostApplicationLifetime lifetime;
    private readonly ILogger<WorkflowRetentionWorker> logger;
    private readonly WorkflowEngineSettings settings;

    public WorkflowRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<WorkflowRetentionWorker> logger,
        IOptions<WorkflowEngineSettings> settings)
    {
        this.scopeFactory = scopeFactory;
        this.lifetime = lifetime;
        this.logger = logger;
        this.settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retention = this.settings.Retention;

        // Both knobs off → worker doesn't even wait for ApplicationStarted, just exits. The
        // service descriptor stays in the host's collection for symmetry but does no work.
        if (!retention.EnableFinishedPurge && !retention.EnableStaleFail)
        {
            this.logger.LogDebug("Workflow retention disabled — worker idle.");
            return;
        }

        try
        {
            await HostStartupBarrier.WaitAsync(this.lifetime, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(retention.SweepIntervalSeconds);
        this.logger.LogInformation(
            "Workflow retention worker enabled — sweep every {Interval} (finishedPurge={Finished} >{FinishedDays}d, staleFail={Stale} >{StaleDays}d, batch={Batch})",
            interval,
            retention.EnableFinishedPurge,
            retention.FinishedRunRetentionDays,
            retention.EnableStaleFail,
            retention.StaleRunningRetentionDays,
            retention.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.SweepOnceAsync(retention, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Retention sweep failed; retrying after {Interval}", interval);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SweepOnceAsync(WorkflowRetentionSettings retention, CancellationToken ct)
    {
        await using var scope = this.scopeFactory.CreateAsyncScope();
        // Narrow dependency: retention only purges. Pulling IWorkflowRetentionStore instead of
        // the full IWorkflowStore documents intent and keeps the worker decoupled from the
        // engine's read/write surface that it doesn't touch.
        var store = scope.ServiceProvider.GetRequiredService<IWorkflowRetentionStore>();

        var now = DateTime.UtcNow;
        var totalFinished = 0;
        var totalStale = 0;

        if (retention.EnableFinishedPurge)
        {
            var threshold = now - TimeSpan.FromDays(retention.FinishedRunRetentionDays);
            // Drain backlog: keep deleting until a sub-batch returns less than the cap, meaning
            // we've cleared everything older than the threshold. Bounds single transaction size.
            while (!ct.IsCancellationRequested)
            {
                var deleted = await store.PurgeFinishedRunsAsync(
                    olderThan: threshold,
                    limit: retention.BatchSize,
                    cancellationToken: ct);
                totalFinished += deleted;
                if (deleted < retention.BatchSize) break;
            }
        }

        if (retention.EnableStaleFail)
        {
            var threshold = now - TimeSpan.FromDays(retention.StaleRunningRetentionDays);
            // Suspended runs are excluded by status — see FailStaleRunningRunsAsync impl. Only
            // genuinely-orphaned Running runs (worker died mid-flight) are caught. Two-phase:
            // they're FAILED here (abort_reason = "stale: …", trace preserved for the incident
            // window); the finished purge above deletes them on its own schedule like any other
            // failed run.
            while (!ct.IsCancellationRequested)
            {
                var failed = await store.FailStaleRunningRunsAsync(
                    olderThan: threshold,
                    limit: retention.BatchSize,
                    cancellationToken: ct);
                totalStale += failed;
                if (failed < retention.BatchSize) break;
            }
        }

        // Log + emit a span only on actual work — silent sweep is the steady-state happy path.
        // No-op sweeps would otherwise spam the trace pipeline with empty boxes every interval.
        if (totalFinished > 0 || totalStale > 0)
        {
            this.logger.LogInformation(
                "Retention sweep purged {Finished} finished run(s) and failed {Stale} stale-running run(s)",
                totalFinished, totalStale);

            using var activity = WorkflowActivitySource.Instance.StartActivity(
                "workflow.retention.sweep", ActivityKind.Internal);
            activity?.SetTag(WorkflowTags.RetentionFinishedPurged, totalFinished);
            activity?.SetTag(WorkflowTags.RetentionStaleFailed, totalStale);
        }
    }
}
