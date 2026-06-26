using System.Diagnostics;
using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Picks up pending workflow step executions, dispatches them via the registered IActionType,
/// merges outputs into the run context, and enqueues successor steps based on workflow edges.
/// All persistence goes through <see cref="IWorkflowStore"/>; edge-walking goes through
/// <see cref="IWorkflowFanOut"/> so the resume API can reuse the same logic.
/// <para>
/// Two-pool routing: when <see cref="WorkflowEngineSettings.LongRunningWorkerCount"/> is &gt; 0
/// the engine spawns a dedicated pool that only claims rows with <c>is_long_running = true</c>;
/// the regular pool then filters them out. Without this split, a handful of slow HTTP steps
/// can hold every worker thread and starve fast Transform / Condition steps. See
/// <see cref="WorkflowStepLane"/> for the filter semantics.
/// </para>
/// <para>
/// Graceful shutdown: <see cref="ProcessBatchAsync"/> calls <c>SaveChangesAsync</c> after each
/// step (not once per batch) so a SIGTERM mid-batch loses at most the currently-executing
/// step. The action's cancellation token is a separate "drain" token that fires only after
/// <see cref="WorkflowEngineSettings.ShutdownDrainSeconds"/> elapses past the stop signal —
/// in-flight HTTP calls get to finish naturally rather than being yanked out mid-flight.
/// </para>
/// </summary>
internal class WorkflowEngineWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IHostApplicationLifetime lifetime;
    private readonly ILogger<WorkflowEngineWorker> logger;
    private readonly WorkflowEngineSettings settings;

    public WorkflowEngineWorker(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<WorkflowEngineWorker> logger,
        IOptions<WorkflowEngineSettings> settings)
    {
        this.scopeFactory = scopeFactory;
        this.lifetime = lifetime;
        this.logger = logger;
        this.settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Hold off polling until the host has finished bringing up every IHostedService /
        // IStartupTask — most importantly the EF migration runner, but this also covers any
        // consumer-side warmup (cache priming, external connections, etc.).
        try
        {
            await HostStartupBarrier.WaitAsync(this.lifetime, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var fastCount = Math.Max(1, this.settings.WorkerCount);
        var longCount = Math.Max(0, this.settings.LongRunningWorkerCount);

        // Lane assignment depends on whether a dedicated long pool was requested:
        //   longCount = 0  → fast pool runs in Any mode (claims everything; legacy behaviour).
        //   longCount > 0  → fast pool runs FastOnly, long pool runs LongOnly. Strict separation.
        var fastLane = longCount > 0 ? WorkflowStepLane.FastOnly : WorkflowStepLane.Any;

        this.logger.LogInformation(
            "WorkflowEngineWorker starting (fast={Fast}/{FastLane}, long={Long}, poll={Poll}s, batch={Batch}, maxAttempts={Max}, fastTimeout={FastTimeout}s, drain={Drain}s)",
            fastCount, fastLane, longCount, this.settings.PollIntervalSeconds, this.settings.BatchSize,
            this.settings.MaxAttempts, this.settings.FastLaneActionTimeoutSeconds, this.settings.ShutdownDrainSeconds);

        // Spawn N fast loops + M long loops in-process. Each loop runs its own ProcessBatchAsync
        // with a fresh DI scope; FOR UPDATE SKIP LOCKED on Postgres-side claim guarantees no two
        // loops ever take the same step. Task.WhenAll holds until shutdown — host SIGTERM cancels
        // the token, every loop's catch returns, all tasks complete, ExecuteAsync returns.
        var loops = new List<Task>(capacity: fastCount + longCount);
        for (int i = 0; i < fastCount; i++)
        {
            int workerId = i;
            loops.Add(this.WorkerLoopAsync(workerId, fastLane, stoppingToken));
        }
        for (int i = 0; i < longCount; i++)
        {
            // Long pool worker IDs continue the numbering for unambiguous log scopes.
            int workerId = fastCount + i;
            loops.Add(this.WorkerLoopAsync(workerId, WorkflowStepLane.LongOnly, stoppingToken));
        }
        await Task.WhenAll(loops);
    }

    /// <summary>
    /// One independent worker loop. <paramref name="workerId"/> is purely cosmetic — appears in
    /// logs to disambiguate which loop did what. Loops don't coordinate with each other; their
    /// only synchronisation is the database row-lock on <c>ClaimPendingStepsAsync</c> /
    /// <c>ClaimExpiredWaitingStepsAsync</c>.
    /// </summary>
    /// <param name="lane">Which subset of pending rows this loop is allowed to claim.</param>
    /// <param name="stoppingToken">
    /// Stop signal. The loop drops out of the polling cycle (no new claims) when it fires;
    /// the in-flight step still gets a per-step cancellation budget — see
    /// <see cref="ProcessBatchAsync"/>.
    /// </param>
    private async Task WorkerLoopAsync(
        int workerId,
        WorkflowStepLane lane,
        CancellationToken stoppingToken)
    {
        using var loopScope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["WorkerId"] = workerId,
            ["Lane"] = lane.ToString(),
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await this.ProcessBatchAsync(lane, stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(this.settings.PollIntervalSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "WorkflowEngineWorker loop error; continuing");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(this.settings.PollIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task<int> ProcessBatchAsync(
        WorkflowStepLane lane,
        CancellationToken stoppingToken)
    {
        // Single shared scope for the whole batch — sweep, claim, and per-step execute all share
        // one DbContext. Claim's tracked load populates Local with the just-claimed entities so
        // UpdateStep / UpdateRun find them in-memory without an extra Find roundtrip.
        // (Per-step scopes were introduced when WorkflowConcurrencyException isolation mattered;
        // since concurrency tokens were dropped, that complexity is gone — shared scope is
        // simpler and lets FanOut's per-run graph cache live across the whole batch.)
        await using var scope = this.scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkflowStore>();
        var registry = scope.ServiceProvider.GetRequiredService<IActionTypeRegistry>();
        var fanOut = scope.ServiceProvider.GetRequiredService<IWorkflowFanOut>();

        // Sweep expired waiting steps before claiming — every action's OnStepTimedOutAsync gets
        // a chance to fire a graceful timeout port; the base default sends the step Dead with a
        // generic message (non-transient). Either way the run-completion check unblocks affected
        // runs.
        await this.ExpireStaleWaitingStepsAsync(store, registry, fanOut, lane, stoppingToken);
        // Commit timeout outcomes as their own logical unit. Critical for the empty-claim case:
        // without this save, a batch that finds expired waiting steps but no pending claims
        // would discard the timeout mutations on scope dispose.
        await store.SaveChangesAsync(stoppingToken);

        // Reconciliation backstop for signal-wait bookmarks — on the same cadence as the timeout
        // sweep. Deletes bookmarks whose target step is no longer Waiting (resumed elsewhere,
        // timed-out, dead-lettered, cancelled). This is what makes bookmark cleanup CORRECT
        // regardless of path; the eager delete-on-resume in the signaler is just an optimization.
        // Set-based DELETE — independent of lane (no lane column on bookmarks); cheap to issue on
        // every fast/long worker pass since it no-ops when nothing's stale. Best-effort: a failure
        // here must not abort the batch's real work, so it's swept once per pass and logged.
        await this.SweepResolvedBookmarksAsync(store, stoppingToken);

        // Don't claim more work after shutdown — sweep was best-effort, but new claims would
        // just need to be released right back.
        if (stoppingToken.IsCancellationRequested) return 0;

        var claimed = await store.ClaimPendingStepsAsync(this.settings.BatchSize, lane, stoppingToken);
        if (claimed.Count == 0) return 0;

        // processed = step ids whose save committed successfully. Anything left over at the
        // end = "claimed but not consumed", released back to pending below.
        var processed = new HashSet<Guid>();

        foreach (var step in claimed)
        {
            // Shutdown signal between steps: stop here. Whatever's left in `claimed` after the
            // break gets released back to pending — the next worker startup picks them up
            // immediately rather than waiting for stale-purge.
            if (stoppingToken.IsCancellationRequested) break;

            // Per-step cancellation token: lane-specific deadline.
            //   Fast / Any lane: hard upfront budget = FastLaneActionTimeoutSeconds. A stuck
            //     fast action (HTTP without timeout, infinite loop) can't camp on the worker.
            //   Long lane: no upfront budget — slow operations are why this lane exists.
            // Both lanes additionally honour shutdown: when stoppingToken fires, ShutdownDrainSeconds
            // is scheduled on the same CTS so the action gets that grace before being force-cancelled.
            using var stepCts = new CancellationTokenSource();
            if (lane != WorkflowStepLane.LongOnly)
            {
                stepCts.CancelAfter(TimeSpan.FromSeconds(this.settings.FastLaneActionTimeoutSeconds));
            }
            using var stopReg = stoppingToken.Register(() =>
                stepCts.CancelAfter(TimeSpan.FromSeconds(this.settings.ShutdownDrainSeconds)));

            try
            {
                await this.ExecuteOneAsync(step, store, registry, fanOut, lane, stepCts.Token);
                await store.SaveChangesAsync(stepCts.Token);
                processed.Add(step.Id);
            }
            catch (OperationCanceledException) when (stepCts.IsCancellationRequested)
            {
                // stepCts fired mid-flight: either fast-lane upfront timeout elapsed or shutdown
                // drain budget ran out. With shared scope, any in-tracker mutations from this
                // step are still pending; we must clear them so the NEXT step's SaveChanges
                // doesn't try to flush this step's failed write again. Detach the step entry
                // (and any same-run step entries that fan-out may have staged).
                this.logger.LogWarning(
                    "Step {StepId} cancelled mid-flight (lane timeout or shutdown drain elapsed); will be released back to pending.",
                    step.Id);
                store.DiscardPendingChanges();
            }
            catch (Exception ex)
            {
                // Any other unhandled error during step processing (DB connection blip, OOM,
                // unhandled exception that escaped ExecuteOneAsync's inner action try/catch, etc).
                // Same cleanup as the OCE branch — drop tracker mutations so the rest of the
                // batch can save cleanly. Side effects already issued by the action are
                // at-least-once: next claim retries them, matching the general engine contract.
                this.logger.LogError(ex,
                    "Unhandled error processing step {StepId}; will be released back to pending.",
                    step.Id);
                store.DiscardPendingChanges();
            }
        }

        // Release path: anything in `claimed` but not in `processed` was claimed via raw SQL
        // (status='running', attempt_count++) but never actually executed — the worker either
        // bailed on a shutdown signal before reaching it OR hit an exception mid-step. Reset
        // to 'pending' and decrement the count so the next claim sees a clean retry slot.
        if (processed.Count < claimed.Count)
        {
            var toRelease = claimed
                .Where(s => !processed.Contains(s.Id))
                .Select(s => s.Id)
                .ToList();

            try
            {
                await using var releaseScope = this.scopeFactory.CreateAsyncScope();
                var releaseStore = releaseScope.ServiceProvider.GetRequiredService<IWorkflowStore>();
                // CT.None on purpose: this UPDATE must complete even after stoppingToken fires,
                // otherwise we'd leave rows in 'running' (the very state we set out to undo).
                // The query is a single row-locked UPDATE; cost is bounded.
                var released = await releaseStore.ReleaseClaimedStepsAsync(toRelease, CancellationToken.None);
                this.logger.LogInformation(
                    "Released {Released}/{Total} claimed-but-unprocessed steps back to pending.",
                    released, toRelease.Count);
            }
            catch (Exception ex)
            {
                // Release itself failed (host already tore down the connection pool, transient
                // DB error, …). Graceful degradation: log loud, swallow — those rows stay in
                // 'running' and the stale-running purge sweeper picks them up later. Better
                // than letting the exception kill the whole worker loop on shutdown.
                this.logger.LogError(ex,
                    "Failed to release {Total} claimed-but-unprocessed steps back to pending; rows stay in 'running' until stale-purge.",
                    toRelease.Count);
            }
        }

        return processed.Count;
    }

    /// <summary>
    /// Internal for the test harness — runs ONE timeout-sweep pass (claim expired waiting steps →
    /// per-action <c>OnStepTimedOutAsync</c> → <see cref="ApplyResultAsync"/>) without spinning up a
    /// scope factory + hosted service. Production callers go through <see cref="ProcessBatchAsync"/>,
    /// which wraps this in the lane / save / release plumbing. Mirrors the <see cref="ExecuteOneAsync"/>
    /// test seam.
    /// </summary>
    internal Task SweepExpiredWaitingStepsOnceAsync(
        IWorkflowStore store,
        IActionTypeRegistry registry,
        IWorkflowFanOut fanOut,
        CancellationToken ct)
        => this.ExpireStaleWaitingStepsAsync(store, registry, fanOut, WorkflowStepLane.Any, ct);

    private async Task ExpireStaleWaitingStepsAsync(
        IWorkflowStore store,
        IActionTypeRegistry registry,
        IWorkflowFanOut fanOut,
        WorkflowStepLane lane,
        CancellationToken ct)
    {
        // Atomic claim — Waiting → Running with FOR UPDATE SKIP LOCKED. Replaces the previous
        // read-only listing that wasn't multi-worker-safe (two loops could see the same expired
        // row and double-fire OnTimeoutAsync). Step is now logically "claimed" by us; the
        // ApplyResultAsync below moves it to a terminal state.
        var expired = await store.ClaimExpiredWaitingStepsAsync(this.settings.BatchSize, lane, ct);
        if (expired.Count == 0) return;

        foreach (var step in expired)
        {
            // Per-action policy: every action's OnStepTimedOutAsync decides the outcome. Suspending
            // actions override it to fire a graceful port (Delay → done, WaitForm / task-actions →
            // timedOut + task expiry); the base default raises a non-transient OnError, landing the
            // step in Dead with a generic message — same outcome as the pre-ADR-027 "no timeout
            // policy" branch, now expressed as the base virtual rather than an absent interface.
            var actionType = registry.TryGet(step.Kind);
            if (actionType is null)
            {
                step.Status = StepExecutionStatus.Dead;
                step.LastError = $"Step '{step.Kind}' timed out while waiting and the action kind is unknown.";
                step.CompletedAt = DateTime.UtcNow;
                store.UpdateStep(step);
                await fanOut.CheckRunCompletionAsync(step, ct);
                continue;
            }

            await this.HandleTimeoutGracefullyAsync(step, store, fanOut, actionType, lane, ct);
        }

        this.logger.LogWarning("Swept {Count} expired waiting step(s)", expired.Count);
    }

    /// <summary>
    /// Reconciliation pass for the generic signal-wait bookmarks. A bookmark is valid only while
    /// its step is Waiting; the moment the step leaves Waiting by ANY path, the bookmark is stale.
    /// One set-based DELETE bounded by <see cref="WorkflowEngineSettings.BatchSize"/>. Best-effort:
    /// swallow + log on failure so a transient DB blip here never aborts the batch's real work.
    /// </summary>
    private async Task SweepResolvedBookmarksAsync(IWorkflowStore store, CancellationToken ct)
    {
        try
        {
            var deleted = await store.SweepResolvedBookmarksAsync(this.settings.BatchSize, ct);
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
        IWorkflowStore store,
        IWorkflowFanOut fanOut,
        IActionType actionType,
        WorkflowStepLane lane,
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
        activity?.SetTag(WorkflowTags.StepLane, FormatLane(lane));

        var run = await store.GetRunAsync(step.RunId, ct);
        if (run is null)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Run {step.RunId} not found while expiring waiting step.";
            step.CompletedAt = DateTime.UtcNow;
            store.UpdateStep(step);
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
            store.UpdateStep(step);
            await fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        // Mirror WorkflowResumer.BuildContextAsync so the timeout context matches the resume/execute
        // shape — NodeKey + StepsOutputs populated (graph is cached on the scope's fan-out, so cheap).
        // No slice-A timeout override reads these, but keeping the context uniform avoids a future
        // footgun for a timeout handler that wants the node key or prior outputs.
        var graph = await fanOut.GetGraphAsync(run, ct);
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
        };

        ActionExecutionResult result;
        try
        {
            result = await actionType.OnStepTimedOutAsync(context, ct);
        }
        catch (Exception ex)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"OnStepTimedOutAsync threw: {ex.Message}";
            step.CompletedAt = DateTime.UtcNow;
            store.UpdateStep(step);
            await fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        await this.ApplyResultAsync(step, result, store, fanOut, ct);
    }

    /// <summary>
    /// Internal for the test harness — exposes the per-step dispatch logic without spinning up a
    /// scope factory + hosted service. Production callers go through <see cref="ProcessBatchAsync"/>.
    /// </summary>
    internal async Task ExecuteOneAsync(
        WorkflowStepRecord step,
        IWorkflowStore store,
        IActionTypeRegistry registry,
        IWorkflowFanOut fanOut,
        CancellationToken ct)
    {
        // Test-harness overload — reuses the lane-aware path with WorkflowStepLane.Any so tests
        // that don't care about lane semantics get the original single-pool behaviour.
        await this.ExecuteOneAsync(step, store, registry, fanOut, WorkflowStepLane.Any, ct);
    }

    internal async Task ExecuteOneAsync(
        WorkflowStepRecord step,
        IWorkflowStore store,
        IActionTypeRegistry registry,
        IWorkflowFanOut fanOut,
        WorkflowStepLane lane,
        CancellationToken ct)
    {
        // Outer scope covers everything we know up-front (step + tenant). Inner scope (after the
        // run loads) adds run-level fields. Serilog enrichers / Seq pick these up automatically;
        // every log line below carries the structured fields without per-call repetition.
        using var stepScope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["StepId"] = step.Id,
            ["RunId"] = step.RunId,
            ["TenantId"] = step.TenantId,
            ["Kind"] = step.Kind,
            ["AttemptCount"] = step.AttemptCount,
            ["Lane"] = lane.ToString(),
        });

        using var stepActivity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.step.execute", ActivityKind.Internal);
        stepActivity?.SetTag(WorkflowTags.RunId, step.RunId);
        stepActivity?.SetTag(WorkflowTags.StepId, step.Id);
        stepActivity?.SetTag(WorkflowTags.TenantId, step.TenantId);
        stepActivity?.SetTag(WorkflowTags.StepKind, step.Kind);
        stepActivity?.SetTag(WorkflowTags.StepAttempt, step.AttemptCount);
        stepActivity?.SetTag(WorkflowTags.StepLane, FormatLane(lane));

        var actionType = registry.TryGet(step.Kind);
        if (actionType is null)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Unknown action kind '{step.Kind}'.";
            step.CompletedAt = DateTime.UtcNow;
            store.UpdateStep(step);
            await fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        var run = await store.GetRunAsync(step.RunId, ct);
        if (run is null)
        {
            // Defensive: dispatching with no run means we'd hand the action a zero TenantId,
            // which custom actions could mistake for "no scoping". Refuse to run and dead-letter.
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Run {step.RunId} not found — refusing to dispatch step.";
            step.CompletedAt = DateTime.UtcNow;
            store.UpdateStep(step);
            return;
        }

        // Run already terminal — typically means an operator cancel or FailRun fired between
        // the claim SQL and our load here. Don't invoke the action: it may have side effects
        // (HTTP, email, DB write) that we shouldn't trigger on a closed run. Mark the step
        // dead-by-association and bail.
        if (run.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Run already terminal ({run.Status}); step skipped.";
            step.CompletedAt = DateTime.UtcNow;
            store.UpdateStep(step);
            return;
        }

        object configObj;
        try
        {
            // step.ResolvedConfig is JsonElement on the record; .Deserialize is the typed
            // overload, no string round-trip. Options must match StepExecutionBuilder's
            // serialize path (camelCase + enum-as-string) for the round-trip to be symmetric.
            configObj = step.ResolvedConfig.Deserialize(actionType.ConfigType, WorkflowJsonOptions.Default)
                ?? Activator.CreateInstance(actionType.ConfigType)!;
        }
        catch (Exception ex)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Could not deserialize resolved config: {ex.Message}";
            step.CompletedAt = DateTime.UtcNow;
            store.UpdateStep(step);
            await fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        // Resolve node-key + steps_outputs snapshot so state-aware actions (ForEach, …) can read
        // their own previous outputs without a separate query. Graph is cached by FanOut for the
        // scope's lifetime — repeated calls within a batch hit the cache instead of re-parsing
        // the snapshot.
        var graph = await fanOut.GetGraphAsync(run, ct);
        var nodeKey = ResolveNodeKey(graph, step.NodeId);
        // run.StepsOutputs is JsonElement on the record now — no per-step parse.
        var stepsOutputsJson = run.StepsOutputs;

        // Run-aware scope: layered on top of stepScope so action-side log calls carry both.
        using var runScope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["NodeKey"] = nodeKey,
            ["DefinitionId"] = run.DefinitionId,
            ["IsDryRun"] = run.IsDryRun,
            ["NestingLevel"] = run.NestingLevel,
        });
        stepActivity?.SetTag(WorkflowTags.StepNodeKey, nodeKey);
        stepActivity?.SetTag(WorkflowTags.DefinitionId, run.DefinitionId);
        stepActivity?.SetTag(WorkflowTags.IsDryRun, run.IsDryRun);
        stepActivity?.SetTag(WorkflowTags.NestingLevel, run.NestingLevel);

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
            StepsOutputs = stepsOutputsJson,
        };

        ActionExecutionResult result;
        // Child span wraps the action invocation specifically — I/O latency (HTTP / S3 / DB
        // inside the action) is visible separately from the surrounding step plumbing.
        using (var actionActivity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.action.execute", ActivityKind.Internal))
        {
            actionActivity?.SetTag(WorkflowTags.ActionKind, step.Kind);
            actionActivity?.SetTag(WorkflowTags.StepLane, FormatLane(lane));
            try
            {
                result = await actionType.ExecuteAsync(context, ct);
                actionActivity?.SetTag(WorkflowTags.ActionResultType, ClassifyResult(result));
                if (result.OutputPort is not null)
                {
                    actionActivity?.SetTag(WorkflowTags.StepOutputPort, result.OutputPort);
                }
                if (result.Error is not null)
                {
                    actionActivity?.SetStatus(ActivityStatusCode.Error, result.Error);
                }
            }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            {
                // Drain budget exhausted (or hard-cancel from the host). Surface as a transient
                // error so the next worker startup retries the step. Without this branch the
                // exception would propagate out of ProcessBatchAsync and the row stays in
                // 'running' status until stale-purge.
                this.logger.LogWarning(
                    "Action {Kind} cancelled during shutdown drain; will retry on restart.", step.Kind);
                actionActivity?.SetStatus(ActivityStatusCode.Error, "Cancelled during shutdown drain");
                actionActivity?.SetTag(WorkflowTags.ActionResultType, "Cancelled");
                result = ActionExecutionResult.OnError(ex.Message, transient: true);
            }
            catch (Exception ex)
            {
                // Unhandled exception → record the message and let the retry / dead-letter path
                // handle it. No port is fired (Dead steps don't enqueue successors any more).
                this.logger.LogError(ex, "Action {Kind} threw an unhandled exception.", step.Kind);
                actionActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                actionActivity?.SetTag(WorkflowTags.ActionResultType, "Exception");
                result = ActionExecutionResult.OnError(ex.Message);
            }
        }

        if (result.OutputPort is not null)
        {
            stepActivity?.SetTag(WorkflowTags.StepOutputPort, result.OutputPort);
        }
        if (result.Error is not null)
        {
            stepActivity?.SetStatus(ActivityStatusCode.Error, result.Error);
        }

        await this.ApplyResultAsync(step, result, store, fanOut, ct);
    }

    /// <summary>
    /// String form of the lane for trace tags. Stable values — dashboards / alerts can compare
    /// directly without parsing the enum. Keep in sync with the enum.
    /// </summary>
    private static string FormatLane(WorkflowStepLane lane) => lane switch
    {
        WorkflowStepLane.Any => "any",
        WorkflowStepLane.FastOnly => "fast",
        WorkflowStepLane.LongOnly => "long",
        _ => "unknown",
    };

    /// <summary>
    /// Tag-friendly label for what flavour of <see cref="ActionExecutionResult"/> the action
    /// returned. Lets dashboards split by suspended-vs-fired-vs-terminated without having to
    /// pattern-match raw fields.
    /// </summary>
    private static string ClassifyResult(ActionExecutionResult result) =>
        result.IsSuspended ? "Suspended"
        : result.TerminatesRun ? "TerminatesRun"
        : result.Error is not null ? "Error"
        : result.OutputPort is not null ? "OnPort"
        : "None";

    /// <summary>
    /// Extracts the node's user-facing key from the parsed graph. Falls back to the node id
    /// when the graph is missing the entry (legacy runs from before keys were mandatory, or
    /// snapshot parse failures the FanOut cache already logged).
    /// </summary>
    private static string ResolveNodeKey(WorkflowGraph? graph, string nodeId)
    {
        var node = graph?.Nodes.FirstOrDefault(n => n.Id == nodeId);
        return string.IsNullOrWhiteSpace(node?.Key) ? nodeId : node.Key;
    }

    private static JsonElement SafeParseJson(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return JsonDocument.Parse("{}").RootElement;
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(raw);
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement;
        }
    }

    /// <summary>
    /// Common landing for both <c>Execute</c> and <c>OnTimeout</c> results — branches on
    /// Suspend / Terminate / Error / success and updates the step accordingly.
    /// </summary>
    private async Task ApplyResultAsync(
        WorkflowStepRecord step,
        ActionExecutionResult result,
        IWorkflowStore store,
        IWorkflowFanOut fanOut,
        CancellationToken ct)
    {
        // Suspend: park the step in Waiting, optionally with a deadline. NextAttemptAt is the
        // sweeper's hook; DateTime.MaxValue keeps the sweeper from ever picking it up.
        if (result.IsSuspended)
        {
            step.Status = StepExecutionStatus.Waiting;
            step.NextAttemptAt = result.SuspendTimeoutSeconds is { } t
                ? DateTime.UtcNow.AddSeconds(t)
                : DateTime.MaxValue;
            step.OutputPort = null;
            step.Outputs = ToJsonElement(result.Outputs);
            step.LastError = null;
            store.UpdateStep(step);
            // Persist any bookmarks the action registered on the SAME pending batch as the step's
            // Waiting transition — the choke point's single flush makes "step parked" and "bookmarks
            // live" atomic. Empty / null = no signal-wait, regular suspend (Approve / Delay / …).
            if (result.Bookmarks is { Count: > 0 } bookmarks)
            {
                store.AddBookmarks(step, bookmarks);
                // Correlation-key PHI hardening: log HASHED keys, never raw. A generic WaitSignal key
                // is author-controlled and could carry PHI; the stable hash lets ops match this
                // suspend to the later SignalAsync (same key → same hash) without exposing the value.
                this.logger.LogInformation(
                    "Step {StepId} suspended with {Count} bookmark(s): {KeyHashes}",
                    step.Id,
                    bookmarks.Count,
                    bookmarks.Select(b => CorrelationKeyLog.Hash(b.CorrelationKey)).ToArray());
            }
            // Drive run.Status → Suspended (single-port engine: this Waiting step is now the only
            // active one). CheckRunCompletion is the single source of truth for run state.
            await fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        // Successful early termination (FinishRun): step is Completed with the return payload
        // stamped on its outputs (for trace), and the run flips to Completed with the same
        // payload on run.ReturnValue (canonical slot the sub-workflow auto-resume reads).
        // No successor edges fire — the action declares no output ports.
        if (result.TerminatesRun)
        {
            var serializedReturn = ToJsonElement(result.ReturnValue);

            step.Status = StepExecutionStatus.Completed;
            step.OutputPort = null;
            step.Outputs = serializedReturn;
            step.CompletedAt = DateTime.UtcNow;
            step.LastError = null;
            store.UpdateStep(step);

            var run = await store.GetRunAsync(step.RunId, ct);
            if (run is not null)
            {
                // Flip to Completed unless run is already terminal (Completed/Failed) — Suspended
                // is fine to override (the FinishRun terminator preempts whatever Waiting step was
                // there). ALWAYS run the parent-resume path: TryResumeWaitingStepAsync atomically
                // guards on Waiting status, so a duplicate resume is a safe no-op.
                if (run.Status is not (WorkflowRunStatus.Completed or WorkflowRunStatus.Failed))
                {
                    run.Status = WorkflowRunStatus.Completed;
                    run.FinishedAt = DateTime.UtcNow;
                    run.ReturnValue = serializedReturn;
                    store.UpdateRun(run);
                }

                await fanOut.OnRunFinalizedAsync(step.RunId, ct);
            }
            return;
        }

        if (!string.IsNullOrEmpty(result.Error))
        {
            step.LastError = result.Error;
            // Non-transient errors (e.g. FailRun) skip retries — straight to Dead.
            if (!result.IsTransient || step.AttemptCount >= this.settings.MaxAttempts)
            {
                step.Status = StepExecutionStatus.Dead;
                step.OutputPort = null;
                step.Outputs = ToJsonElement(result.Outputs);
                step.CompletedAt = DateTime.UtcNow;
                store.UpdateStep(step);
                this.logger.LogError(
                    "Step dead-lettered after {AttemptCount}/{MaxAttempts} attempt(s) (transient={Transient}): {Error}",
                    step.AttemptCount,
                    this.settings.MaxAttempts,
                    result.IsTransient,
                    result.Error);
                // Dead steps don't fire any successor edges — branches that should run on
                // failure must wire to an Error-kind port the action returns explicitly.
                await fanOut.CheckRunCompletionAsync(step, ct);
            }
            else
            {
                // Retry.
                step.Status = StepExecutionStatus.Pending;
                step.NextAttemptAt = DateTime.UtcNow.Add(this.BackoffFor(step.AttemptCount));
                store.UpdateStep(step);
                this.logger.LogWarning(
                    "Step transient error on attempt {AttemptCount}/{MaxAttempts}, retrying at {NextAttemptAt:o}: {Error}",
                    step.AttemptCount,
                    this.settings.MaxAttempts,
                    step.NextAttemptAt,
                    result.Error);
            }
            return;
        }

        step.Status = StepExecutionStatus.Completed;
        step.OutputPort = result.OutputPort;
        step.Outputs = ToJsonElement(result.Outputs);
        step.CompletedAt = DateTime.UtcNow;
        step.LastError = null;
        store.UpdateStep(step);

        await fanOut.EnqueueNextStepAsync(step, result.OutputPort, ct);
        await fanOut.CheckRunCompletionAsync(step, ct);
    }

    /// <summary>
    /// Converts an action's <see cref="ActionExecutionResult.Outputs"/> / <c>ReturnValue</c>
    /// (loose <see cref="object"/> from the contract) into the JsonElement the record stores.
    /// Null in → null out so the column stays unset for actions that didn't produce a payload.
    /// Goes through <see cref="WorkflowJsonOptions.Default"/> so camelCase + enum-as-string is
    /// applied consistently (action authors return anonymous types whose property names should
    /// surface to consumers in camelCase).
    /// </summary>
    private static JsonElement? ToJsonElement(object? value) =>
        value is null ? null : JsonSerializer.SerializeToElement(value, WorkflowJsonOptions.Default);

    private TimeSpan BackoffFor(int attemptIndex)
    {
        var backoff = this.settings.BackoffSeconds;
        if (backoff.Length == 0) return TimeSpan.FromSeconds(30);
        var idx = Math.Min(attemptIndex - 1, backoff.Length - 1);
        return TimeSpan.FromSeconds(backoff[Math.Max(0, idx)]);
    }
}
