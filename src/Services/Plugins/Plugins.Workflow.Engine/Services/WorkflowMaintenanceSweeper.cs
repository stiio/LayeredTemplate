using System.Diagnostics;
using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Scoped home of the maintenance-loop work items: the expired-waiting timeout sweep (per-action
/// <c>OnStepTimedOutAsync</c> policy + the compensating revert when handling fails), stuck-running
/// crash recovery, and resolved-bookmark reconciliation.
/// <para>
/// Division of labour with <see cref="WorkflowEngineWorker"/>: the worker's maintenance loop owns
/// cadence and DI-scope wrapping (each expired step is handled in its own scope, same isolation
/// as a regular action dispatch); every method here works against THIS scope's store and stages /
/// commits only its own outcome. Timeout results land through
/// <see cref="WorkflowStepExecutor.ApplyResultAsync"/> so execute and timeout share one result
/// state machine.
/// </para>
/// </summary>
internal class WorkflowMaintenanceSweeper
{
    private readonly IWorkflowStore store;
    private readonly IActionTypeRegistry registry;
    private readonly IWorkflowFanOut fanOut;
    private readonly IExpressionResolver resolver;
    private readonly WorkflowStepExecutor executor;
    private readonly WorkflowEngineSettings settings;
    private readonly ILogger<WorkflowMaintenanceSweeper> logger;

    public WorkflowMaintenanceSweeper(
        IWorkflowStore store,
        IActionTypeRegistry registry,
        IWorkflowFanOut fanOut,
        IExpressionResolver resolver,
        WorkflowStepExecutor executor,
        IOptions<WorkflowEngineSettings> settings,
        ILogger<WorkflowMaintenanceSweeper> logger)
    {
        this.store = store;
        this.registry = registry;
        this.fanOut = fanOut;
        this.resolver = resolver;
        this.executor = executor;
        this.settings = settings.Value;
        this.logger = logger;
    }

    /// <summary>
    /// One expired step's timeout handling: tracked-loads the claimed step and routes it through
    /// its action's timeout policy. Stages mutations on the scoped store without flushing — the
    /// caller commits (or disposes the scope unsaved on failure and reverts via
    /// <see cref="RevertExpiredStepAsync"/>).
    /// </summary>
    internal async Task HandleExpiredStepAsync(Guid stepId, CancellationToken ct)
    {
        var step = await this.store.GetStepAsync(stepId, ct);
        if (step is null) return;

        // Per-action policy: every action's OnStepTimedOutAsync decides the outcome. Suspending
        // actions override it to fire a graceful port (Delay → done, WaitSignal / RunWorkflow →
        // timedOut); the base default raises a non-transient OnError, landing the step in Dead
        // with a generic message.
        var actionType = this.registry.TryGet(step.Kind);
        if (actionType is null)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Step '{step.Kind}' timed out while waiting and the action kind is unknown.";
            step.CompletedAt = DateTime.UtcNow;
            this.store.UpdateStep(step);
            await this.fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        await this.HandleTimeoutGracefullyAsync(step, actionType, ct);
    }

    /// <summary>
    /// Compensating write after timeout handling didn't complete. The sweep's claim flipped the
    /// step Waiting → Running via committed raw SQL; without this revert the row would be stuck
    /// in 'running' FOREVER — its run sits in Suspended (which the stale-running fail
    /// deliberately skips) and no claim path ever touches non-pending rows. Commits its own
    /// outcome; run it in a FRESH scope (the failed handler's scope died with its staged junk).
    /// </summary>
    /// <param name="failure">
    /// The handler failure message, or null for a shutdown interruption. A failure consumes an
    /// attempt (so a deterministically-broken timeout hook dead-letters at MaxAttempts instead
    /// of retrying forever) and re-parks with backoff; a shutdown re-parks immediately with no
    /// attempt consumed.
    /// </param>
    internal async Task RevertExpiredStepAsync(Guid stepId, string? failure)
    {
        var step = await this.store.GetStepAsync(stepId, CancellationToken.None);
        if (step is null || step.Status != StepExecutionStatus.Running)
        {
            // Vanished, or some other path already progressed it — nothing to revert.
            return;
        }

        if (failure is null)
        {
            // Shutdown flavour: immediate re-park, the next startup's sweep re-claims it.
            step.Status = StepExecutionStatus.Waiting;
            step.NextAttemptAt = DateTime.UtcNow;
            this.store.UpdateStep(step);
            await this.store.SaveChangesAsync(CancellationToken.None);
            this.logger.LogInformation(
                "Expired step {StepId} parked back to waiting after shutdown interrupted its timeout handling.",
                stepId);
            return;
        }

        step.AttemptCount += 1;
        step.LastError = $"Timeout handling failed: {failure}";

        if (step.AttemptCount >= this.settings.MaxAttempts)
        {
            step.Status = StepExecutionStatus.Dead;
            step.CompletedAt = DateTime.UtcNow;
            this.store.UpdateStep(step);
            await this.fanOut.CheckRunCompletionAsync(step, CancellationToken.None);
            await this.store.SaveChangesAsync(CancellationToken.None);
            this.logger.LogError(
                "Expired step {StepId} dead-lettered after {Attempts}/{Max} failed timeout-handling attempt(s): {Failure}",
                stepId, step.AttemptCount, this.settings.MaxAttempts, failure);
            return;
        }

        step.Status = StepExecutionStatus.Waiting;
        step.NextAttemptAt = DateTime.UtcNow.Add(this.executor.BackoffFor(step.AttemptCount));
        this.store.UpdateStep(step);
        await this.store.SaveChangesAsync(CancellationToken.None);
        this.logger.LogWarning(
            "Expired step {StepId} parked back to waiting (attempt {Attempt}/{Max}); timeout handling retries at {NextAttemptAt:o}.",
            stepId, step.AttemptCount, this.settings.MaxAttempts, step.NextAttemptAt);
    }

    /// <summary>
    /// Runs ONE full timeout-sweep pass (claim expired waiting steps → per-action
    /// <c>OnStepTimedOutAsync</c> → apply result, with the same shutdown remainder-revert) entirely
    /// on this scope's store. The production pass (<c>WorkflowEngineWorker.RunMaintenancePassAsync</c>)
    /// runs the same claim → handle → revert sequence but wraps each step in its own DI scope and
    /// flush — keep the two in sync. This single-scope composition is what the test suite drives.
    /// </summary>
    internal async Task SweepExpiredOnceAsync(CancellationToken ct)
    {
        var expiredIds = await this.store.ClaimExpiredWaitingStepIdsAsync(this.settings.BatchSize, ct);
        foreach (var stepId in expiredIds)
        {
            if (ct.IsCancellationRequested) break;
            await this.HandleExpiredStepAsync(stepId, ct);
        }

        // Shutdown mid-batch: the claim already committed Waiting → Running for EVERY id in this
        // chunk — anything the loop didn't finish must be parked back. Replaying the FULL id list
        // is safe and needs no bookkeeping: the revert only touches rows still in 'running'.
        if (ct.IsCancellationRequested)
        {
            foreach (var stepId in expiredIds)
            {
                await this.RevertExpiredStepAsync(stepId, failure: null);
            }
        }
    }

    /// <summary>
    /// One stuck-running recovery pass. Returns steps abandoned in 'running' by crashed workers
    /// (updated_at older than <see cref="WorkflowEngineSettings.StuckStepRecoverySeconds"/>) to
    /// 'pending', draining in BatchSize chunks until dry. The crashed attempt stays counted: the
    /// first SOFT failure after recovery dead-letters via MaxAttempts. A pure hard-crash loop
    /// (the step kills the process every time, so ApplyResult's cap check never runs) is
    /// rate-limited by the threshold and ultimately terminated by Retention.EnableStaleFail at
    /// the run level.
    /// </summary>
    internal async Task RecoverStuckRunningStepsOnceAsync(CancellationToken ct)
    {
        var threshold = DateTime.UtcNow.AddSeconds(-this.settings.StuckStepRecoverySeconds);
        while (!ct.IsCancellationRequested)
        {
            var recovered = await this.store.ReclaimStuckRunningStepIdsAsync(threshold, this.settings.BatchSize, ct);
            if (recovered.Count == 0) break;

            this.logger.LogWarning(
                "Recovered {Count} step(s) stuck in 'running' for over {Threshold}s (worker crash suspected); returned to pending: {StepIds}",
                recovered.Count, this.settings.StuckStepRecoverySeconds, recovered);

            if (recovered.Count < this.settings.BatchSize) break;
        }
    }

    /// <summary>
    /// Reconciliation pass for the generic signal-wait bookmarks. A bookmark is valid only while
    /// its step is Waiting; the moment the step leaves Waiting by ANY path, the bookmark is stale.
    /// One set-based DELETE bounded by <see cref="WorkflowEngineSettings.BatchSize"/>. Best-effort:
    /// swallow + log on failure so a transient DB blip here never aborts the loop's real work.
    /// </summary>
    internal async Task SweepResolvedBookmarksAsync(CancellationToken ct)
    {
        try
        {
            var deleted = await this.store.SweepResolvedBookmarksAsync(this.settings.BatchSize, ct);
            if (deleted > 0)
            {
                this.logger.LogInformation("Reconciliation swept {Count} resolved bookmark(s).", deleted);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown — fine, next startup re-sweeps.
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Bookmark reconciliation sweep failed; will retry next pass.");
        }
    }

    private async Task HandleTimeoutGracefullyAsync(
        WorkflowStepRecord step,
        IActionType actionType,
        CancellationToken ct)
    {
        using var sweepScope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["StepId"] = step.Id,
            ["RunId"] = step.RunId,
            ["TenantId"] = step.TenantId,
            ["Kind"] = step.Kind,
            ["Phase"] = "deadline_sweep",
        });
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.step.timeout", ActivityKind.Internal);
        activity?.SetTag(WorkflowTags.RunId, step.RunId);
        activity?.SetTag(WorkflowTags.StepId, step.Id);
        activity?.SetTag(WorkflowTags.TenantId, step.TenantId);
        activity?.SetTag(WorkflowTags.StepKind, step.Kind);

        var run = await this.store.GetRunAsync(step.RunId, ct);
        if (run is null)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Run {step.RunId} not found while expiring waiting step.";
            step.CompletedAt = DateTime.UtcNow;
            this.store.UpdateStep(step);
            return;
        }

        object configObj;
        var configType = actionType.ConfigType;
        try
        {
            configObj = step.ResolvedConfig.Deserialize(configType, WorkflowJsonOptions.Default)
                ?? Activator.CreateInstance(configType)!;
        }
        catch (Exception ex)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Could not deserialize resolved config on timeout: {ex.Message}";
            step.CompletedAt = DateTime.UtcNow;
            this.store.UpdateStep(step);
            await this.fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        // Mirror WorkflowResumer.BuildContextAsync so the timeout context matches the resume/execute
        // shape — NodeKey + StepsOutputs populated (graph is cached on the scope's fan-out, so cheap).
        // No timeout override reads these today, but keeping the context uniform avoids a future
        // footgun for a timeout handler that wants the node key or prior outputs.
        var graph = await this.fanOut.GetGraphAsync(run, ct);
        var node = graph?.Nodes.FirstOrDefault(n => n.Id == step.NodeId);
        var nodeKey = string.IsNullOrWhiteSpace(node?.Key) ? step.NodeId : node.Key;

        var context = new ActionContext
        {
            Config = configObj,
            RunId = step.RunId,
            StepExecutionId = step.Id,
            TenantId = run.TenantId,
            DefinitionId = run.DefinitionId,
            ActorUserId = run.ActorUserId,
            TriggerSourceKind = run.TriggerSourceKind,
            TriggerSourceId = run.TriggerSourceId,
            IsDryRun = run.IsDryRun,
            NodeKey = nodeKey,
            StepsOutputs = run.StepsOutputs,
            // For a timeout hook these are the suspend-time initial outputs — same channel.
            PriorAttemptOutputs = step.Outputs,
            AttemptCount = step.AttemptCount,
        };

        ActionExecutionResult result;
        try
        {
            // Same late transient resolution as the execute path — a timeout hook may read
            // config. The timeout fire is one-shot (no retry bookkeeping), so a resolution
            // failure lands in the catch below: Dead with the reason recorded.
            await this.resolver.ResolveTransientAsync(
                configObj,
                () => ExpressionModelBuilder.Build(run.StaticContext, run.StepsOutputs),
                ExpressionModelBuilder.EvaluationContextForRun(run),
                ct);

            result = await actionType.OnStepTimedOutAsync(context, ct);
        }
        catch (Exception ex)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"OnStepTimedOutAsync threw: {ex.Message}";
            step.CompletedAt = DateTime.UtcNow;
            this.store.UpdateStep(step);
            await this.fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        await this.executor.ApplyResultAsync(step, result, ct);
    }
}
