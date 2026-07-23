using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Hosted orchestrator of the engine's background work: spawns the worker loops that claim
/// pending step executions and the single maintenance loop, and owns everything scoping- and
/// lifetime-related — claims, DI scopes, cancellation budgets, flushes, release-on-failure.
/// What happens to a step INSIDE a scope lives elsewhere: per-step dispatch in
/// <see cref="WorkflowStepExecutor"/>, maintenance work items in
/// <see cref="WorkflowMaintenanceSweeper"/>.
/// <para>
/// Two-pool routing: when <see cref="WorkflowEngineSettings.LongRunningWorkerCount"/> is &gt; 0
/// the engine spawns a dedicated pool that only claims rows with <c>is_long_running = true</c>;
/// the regular pool then filters them out. Without this split, a handful of slow HTTP steps
/// can hold every worker thread and starve fast Transform / Condition steps. See
/// <see cref="WorkflowStepLane"/> for the filter semantics.
/// </para>
/// <para>
/// Per-step DI scope model: the claim runs in a short-lived scope (its UPDATE … RETURNING
/// commits immediately), then every claimed step is loaded, executed, and flushed in its OWN
/// scope — the same lifetime an action would see inside a web request. That gives isolation
/// for consumer scoped services (an action's scoped tenant context can't leak into the next
/// step, which may belong to another run / tenant), failure isolation for free (on any error
/// the scope is disposed unsaved — no cross-step change-tracker pollution), and a fresh
/// identity map per step (an operator cancel committed mid-batch is visible to the very next
/// step instead of being stale-cached until the batch ends).
/// </para>
/// <para>
/// Graceful shutdown: each step's scope flushes on completion, so a SIGTERM mid-batch loses at
/// most the currently-executing step. The action's cancellation token is a separate "drain"
/// token that fires only after <see cref="WorkflowEngineSettings.ShutdownDrainSeconds"/>
/// elapses past the stop signal — in-flight HTTP calls get to finish naturally rather than
/// being yanked out mid-flight.
/// </para>
/// <para>
/// A single per-process maintenance loop owns the expired-waiting timeout sweep
/// (<see cref="WorkflowEngineSettings.MaintenanceIntervalSeconds"/>) and the bookmark
/// reconciliation sweep (<see cref="WorkflowEngineSettings.BookmarkSweepIntervalSeconds"/>,
/// deliberately much rarer — pure hygiene); worker loops only claim and execute. Running the
/// sweeps on every worker pass duplicated identical queries WorkerCount× for no benefit.
/// </para>
/// </summary>
internal class WorkflowEngineWorker : BackgroundService
{
    /// <summary>
    /// How often the maintenance loop probes for stuck-running rows. Deliberately a constant,
    /// not a knob: the RECOVERY latency is governed by
    /// <see cref="WorkflowEngineSettings.StuckStepRecoverySeconds"/> (hour-scale) — a 5-minute
    /// detection granularity on top adds nothing worth configuring, and the probe is a single
    /// query against the tiny running-partial index.
    /// </summary>
    private const int StuckRecoveryCheckIntervalSeconds = 300;

    private readonly IServiceScopeFactory scopeFactory;
    private readonly IHostApplicationLifetime lifetime;
    private readonly IWorkflowWorkSignal workSignal;
    private readonly ILogger<WorkflowEngineWorker> logger;
    private readonly WorkflowEngineSettings settings;

    public WorkflowEngineWorker(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        IWorkflowWorkSignal workSignal,
        ILogger<WorkflowEngineWorker> logger,
        IOptions<WorkflowEngineSettings> settings)
    {
        this.scopeFactory = scopeFactory;
        this.lifetime = lifetime;
        this.workSignal = workSignal;
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
            "WorkflowEngineWorker starting (fast={Fast}/{FastLane}, long={Long}, poll={Poll}s, maintenance={Maintenance}s, batch={Batch}, maxAttempts={Max}, fastTimeout={FastTimeout}s, drain={Drain}s)",
            fastCount, fastLane, longCount, this.settings.PollIntervalSeconds, this.settings.MaintenanceIntervalSeconds,
            this.settings.BatchSize, this.settings.MaxAttempts, this.settings.FastLaneActionTimeoutSeconds, this.settings.ShutdownDrainSeconds);

        // Spawn N fast loops + M long loops + 1 maintenance loop in-process. Each loop runs with
        // its own DI scope; FOR UPDATE SKIP LOCKED on Postgres-side claim guarantees no two
        // loops ever take the same step. Task.WhenAll holds until shutdown — host SIGTERM cancels
        // the token, every loop's catch returns, all tasks complete, ExecuteAsync returns.
        var loops = new List<Task>(capacity: fastCount + longCount + 1);
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
        loops.Add(this.MaintenanceLoopAsync(stoppingToken));
        await Task.WhenAll(loops);
    }

    /// <summary>
    /// One independent worker loop. <paramref name="workerId"/> is purely cosmetic — appears in
    /// logs to disambiguate which loop did what. Loops don't coordinate with each other; their
    /// only synchronisation is the database row-lock on <c>ClaimPendingStepIdsAsync</c> /
    /// <c>ClaimExpiredWaitingStepIdsAsync</c>.
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
                    // Idle: sleep until a work pulse (push-capable storage — e.g. the EF Core
                    // plugin's LISTEN/NOTIFY listener) or the fallback poll interval, whichever
                    // fires first. Without a pulser this is exactly the old fixed-interval poll.
                    await this.workSignal.WaitForWorkAsync(
                        lane, TimeSpan.FromSeconds(this.settings.PollIntervalSeconds), stoppingToken);
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
                    // Deliberately a plain delay, not WaitForWorkAsync: after an unexpected
                    // loop error this is a backoff, and work pulses must not be able to turn a
                    // persistently-failing loop hot.
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
        // Claim pending step ids in a short-lived scope. The claim UPDATE … RETURNING commits at
        // the DB level immediately (raw SQL), so the claim survives disposing this scope; each id
        // is then loaded + processed in its OWN scope below (see the class doc for why).
        IReadOnlyList<Guid> claimedIds;
        await using (var claimScope = this.scopeFactory.CreateAsyncScope())
        {
            var claimStore = claimScope.ServiceProvider.GetRequiredService<IWorkflowStore>();
            claimedIds = await claimStore.ClaimPendingStepIdsAsync(this.settings.BatchSize, lane, stoppingToken);
        }

        if (claimedIds.Count == 0) return 0;

        // processed = step ids whose own-scope save committed. Anything left over at the end =
        // "claimed but not consumed", released back to pending below.
        var processed = new HashSet<Guid>();

        foreach (var stepId in claimedIds)
        {
            // Shutdown signal between steps: stop here. Whatever's left in `claimedIds` after
            // the break gets released back to pending — the next worker startup picks them up
            // immediately rather than waiting for stale-purge.
            if (stoppingToken.IsCancellationRequested) break;

            // Per-step cancellation token: lane-specific deadline.
            //   Fast / Any lane: hard upfront budget = FastLaneActionTimeoutSeconds. A stuck
            //     fast action (HTTP without timeout, infinite loop) can't camp on the worker.
            //   Long lane: generous optional budget = LongLaneActionTimeoutSeconds (0 = none) —
            //     slow operations are why this lane exists, but "slow" must not mean "hung
            //     forever on a dead socket".
            // Both lanes additionally honour shutdown: when stoppingToken fires, ShutdownDrainSeconds
            // is scheduled on the same CTS so the action gets that grace before being force-cancelled.
            using var stepCts = new CancellationTokenSource();
            if (lane != WorkflowStepLane.LongOnly)
            {
                stepCts.CancelAfter(TimeSpan.FromSeconds(this.settings.FastLaneActionTimeoutSeconds));
            }
            else if (this.settings.LongLaneActionTimeoutSeconds > 0)
            {
                stepCts.CancelAfter(TimeSpan.FromSeconds(this.settings.LongLaneActionTimeoutSeconds));
            }
            using var stopReg = stoppingToken.Register(() =>
                stepCts.CancelAfter(TimeSpan.FromSeconds(this.settings.ShutdownDrainSeconds)));

            if (await this.ProcessClaimedStepAsync(stepId, lane, stepCts.Token, stoppingToken))
            {
                processed.Add(stepId);
            }
        }

        // Release path: anything in `claimedIds` but not in `processed` was claimed via raw SQL
        // (status='running', attempt_count++) but its outcome never committed — the worker either
        // bailed on a shutdown signal before reaching it OR its own-scope processing failed.
        // Reset to 'pending' and decrement the count so the next claim sees a clean retry slot.
        if (processed.Count < claimedIds.Count)
        {
            var toRelease = claimedIds.Where(id => !processed.Contains(id)).ToList();

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
                    "Failed to release {Total} claimed-but-unprocessed steps back to pending; rows stay in 'running' — reaped only when Retention.EnableStaleFail (opt-in) fails their runs, or by operator intervention.",
                    toRelease.Count);
            }
        }

        return processed.Count;
    }

    /// <summary>
    /// Processes a single claimed step in its OWN DI scope: loads the step, runs it through
    /// <see cref="WorkflowStepExecutor.ExecuteAsync"/>, and flushes. A fresh scope per step gives
    /// per-request-style scoped-service lifetimes and full failure isolation: on any error the
    /// scope is simply disposed unsaved, leaving the row 'running' for the release path to
    /// revert. Returns true when the step's outcome was durably persisted (or the row vanished
    /// and there is nothing to release).
    /// </summary>
    private async Task<bool> ProcessClaimedStepAsync(
        Guid stepId,
        WorkflowStepLane lane,
        CancellationToken ct,
        CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = this.scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IWorkflowStore>();
            var executor = scope.ServiceProvider.GetRequiredService<WorkflowStepExecutor>();

            var step = await store.GetStepAsync(stepId, ct);
            if (step is null)
            {
                // Vanished between claim and load (tenant purge / external delete). Nothing to
                // execute and no row to revert — treat as processed.
                return true;
            }

            await executor.ExecuteAsync(step, lane, ct);
            // CT.None on purpose: the action has already run (side effects possibly issued);
            // this flush persists its computed outcome. Cancelling here — lane deadline landing
            // between action completion and save, or shutdown drain — would discard the outcome
            // and force a duplicate execution on the next claim. Single bounded flush; same
            // rationale as the release path.
            await store.SaveChangesAsync(CancellationToken.None);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
        {
            // Lane action timeout (shutdown not involved). The step's scope has been
            // disposed with whatever it staged — but do NOT just release the claim: release
            // refunds the attempt_count bump while next_attempt_at stays in the past, so a
            // deterministically-slow action would be re-claimed immediately (claim orders by
            // next_attempt_at — the timed-out step always sorts first) and spin this worker
            // forever, one timeout budget per revolution. Count the attempt instead, in a
            // fresh scope: the standard transient-error outcome retries with backoff and
            // dead-letters once MaxAttempts is exhausted.
            return await this.TryApplyLaneTimeoutOutcomeAsync(stepId, lane);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown drain elapsed mid-step. The scope's DbContext is disposed with its
            // tracked mutations — nothing persisted; return false so the release path reverts
            // the claim (attempt bump refunded): a routine deploy must not consume attempts —
            // with MaxAttempts = 1 a counted shutdown cancellation would dead-letter perfectly
            // healthy steps.
            this.logger.LogWarning(
                "Step {StepId} cancelled by shutdown drain; will be released back to pending.",
                stepId);
            return false;
        }
        catch (Exception ex)
        {
            // Any other unhandled error (DB connection blip, OOM, an exception that escaped
            // the executor's inner action try/catch, …). Scope disposed unsaved; the release
            // path reverts the claim. Side effects already issued by the action are
            // at-least-once: the next claim retries them, matching the general engine contract.
            this.logger.LogError(ex,
                "Unhandled error processing step {StepId}; will be released back to pending.",
                stepId);
            return false;
        }
    }

    /// <summary>
    /// Persists the lane-timeout outcome for <paramref name="stepId"/> in a fresh scope
    /// (the step's own scope died with the cancellation): standard transient-error semantics —
    /// backoff retry, dead-letter at the attempts cap. Returns true when the outcome committed;
    /// false falls back to the release path so the invariant "claimed but not consumed ⇒
    /// released" survives even a DB blip here.
    /// </summary>
    private async Task<bool> TryApplyLaneTimeoutOutcomeAsync(Guid stepId, WorkflowStepLane lane)
    {
        var timeoutSeconds = lane == WorkflowStepLane.LongOnly
            ? this.settings.LongLaneActionTimeoutSeconds
            : this.settings.FastLaneActionTimeoutSeconds;
        try
        {
            await using var scope = this.scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IWorkflowStore>();
            var executor = scope.ServiceProvider.GetRequiredService<WorkflowStepExecutor>();

            var step = await store.GetStepAsync(stepId, CancellationToken.None);
            if (step is null) return true;

            var timeoutResult = ActionExecutionResult.OnError(
                lane == WorkflowStepLane.LongOnly
                    ? $"Step did not complete within the long-lane action timeout ({timeoutSeconds}s). "
                        + "Raise LongLaneActionTimeoutSeconds (or set it to 0) if the action legitimately runs longer."
                    : $"Step did not complete within the fast-lane action timeout ({timeoutSeconds}s). "
                        + "Actions that legitimately run this long should declare IsLongRunning = true.",
                transient: true);
            await executor.ApplyResultAsync(step, timeoutResult, CancellationToken.None);
            await store.SaveChangesAsync(CancellationToken.None);
            this.logger.LogWarning(
                "Step {StepId} hit the {Lane}-lane action timeout ({Timeout}s) on attempt {Attempt}/{Max}; timeout outcome applied.",
                stepId, WorkflowStepExecutor.FormatLane(lane), timeoutSeconds, step.AttemptCount, this.settings.MaxAttempts);
            return true;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex,
                "Failed to persist lane-timeout outcome for step {StepId}; releasing claim instead.",
                stepId);
            return false;
        }
    }

    /// <summary>
    /// Single per-process maintenance loop: expired-waiting timeout sweep + bookmark
    /// reconciliation on their own (rarer) cadence, so N worker loops don't each repeat the
    /// same queries every poll. Timeout handlers run here regardless of the step's lane —
    /// <c>OnStepTimedOutAsync</c> hooks are quick decision code, not action bodies, and a
    /// dedicated loop means even a slow custom hook never blocks step workers (it only delays
    /// other timeout firings, which are sweep-granular anyway).
    /// </summary>
    private async Task MaintenanceLoopAsync(CancellationToken stoppingToken)
    {
        using var loopScope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["WorkerId"] = "maintenance",
        });

        // Bookmark reconciliation runs on its own, much rarer cadence than the timeout sweep:
        // it's pure hygiene (the Waiting-guard, not bookmark existence, is what prevents wrong
        // resumes; the signaler eagerly deletes what it consumes), so there is no reason to pay
        // a set-based DELETE every pass. First due immediately — catches backlog from downtime.
        var nextBookmarkSweep = DateTime.UtcNow;

        // Stuck-running crash recovery: also its own cadence — the threshold is measured in an
        // hour, a 5-minute detection granularity on top of it costs nothing, and the check is a
        // single probe of the tiny running-partial index. First due immediately: a restart
        // right after a crash is exactly when abandoned rows are most likely.
        var nextStuckRecovery = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.RunMaintenancePassAsync(stoppingToken);

                if (this.settings.BookmarkSweepIntervalSeconds > 0 && DateTime.UtcNow >= nextBookmarkSweep)
                {
                    // Own lightweight scope — set-based DELETE, no action code involved.
                    await using var bookmarkScope = this.scopeFactory.CreateAsyncScope();
                    var sweeper = bookmarkScope.ServiceProvider.GetRequiredService<WorkflowMaintenanceSweeper>();
                    await sweeper.SweepResolvedBookmarksAsync(stoppingToken);
                    nextBookmarkSweep = DateTime.UtcNow.AddSeconds(this.settings.BookmarkSweepIntervalSeconds);
                }

                if (this.settings.StuckStepRecoverySeconds > 0 && DateTime.UtcNow >= nextStuckRecovery)
                {
                    await using var recoveryScope = this.scopeFactory.CreateAsyncScope();
                    var sweeper = recoveryScope.ServiceProvider.GetRequiredService<WorkflowMaintenanceSweeper>();
                    await sweeper.RecoverStuckRunningStepsOnceAsync(stoppingToken);
                    nextStuckRecovery = DateTime.UtcNow.AddSeconds(StuckRecoveryCheckIntervalSeconds);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Workflow maintenance pass failed; retrying next interval.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(this.settings.MaintenanceIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// One maintenance pass: drain expired-waiting steps in BatchSize chunks until dry (a burst
    /// of simultaneously-expiring Delays clears within one pass instead of one chunk per
    /// interval). Ids are claimed in a short-lived scope (the claim SQL commits immediately);
    /// each timeout handler then runs in its OWN scope — <c>OnStepTimedOutAsync</c> is
    /// consumer-extensible code and gets the same scoped-service isolation as a regular action
    /// dispatch. The same claim → handle → revert sequence exists single-scope on
    /// <see cref="WorkflowMaintenanceSweeper.SweepExpiredOnceAsync"/> — keep the two in sync.
    /// </summary>
    private async Task RunMaintenancePassAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<Guid> expiredIds;
            await using (var claimScope = this.scopeFactory.CreateAsyncScope())
            {
                var claimStore = claimScope.ServiceProvider.GetRequiredService<IWorkflowStore>();
                expiredIds = await claimStore.ClaimExpiredWaitingStepIdsAsync(this.settings.BatchSize, stoppingToken);
            }

            if (expiredIds.Count == 0) break;
            this.logger.LogInformation("Sweeping {Count} expired waiting step(s)", expiredIds.Count);

            foreach (var stepId in expiredIds)
            {
                if (stoppingToken.IsCancellationRequested) break;
                await this.ProcessExpiredStepAsync(stepId, stoppingToken);
            }

            // Shutdown mid-batch: the claim already committed Waiting → Running for EVERY id in
            // this chunk — anything the loop didn't finish must be parked back, mirroring the
            // worker's claimed-but-unprocessed release. Replaying the FULL id list is safe and
            // needs no bookkeeping: the revert only touches rows still in 'running', so steps
            // that completed, failed-and-reverted, or were interrupted mid-handling (their own
            // swallow path leaves them 'running' too) all end up in exactly one state.
            if (stoppingToken.IsCancellationRequested)
            {
                foreach (var stepId in expiredIds)
                {
                    await this.TryRevertExpiredStepAsync(stepId, failure: null);
                }
                return;
            }

            if (expiredIds.Count < this.settings.BatchSize) break;
        }
    }

    /// <summary>
    /// Handles one claimed expired step in its OWN DI scope and flushes the outcome. A failure
    /// disposes the scope unsaved and leaves the row 'running' until the stale-running purge —
    /// the same recovery story as a worker crash mid-step.
    /// </summary>
    private async Task ProcessExpiredStepAsync(Guid stepId, CancellationToken ct)
    {
        try
        {
            await using var scope = this.scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IWorkflowStore>();
            var sweeper = scope.ServiceProvider.GetRequiredService<WorkflowMaintenanceSweeper>();

            await sweeper.HandleExpiredStepAsync(stepId, ct);
            // Commit the timeout outcome as its own unit. (A parent auto-resume triggered inside
            // the handler has already committed via the resumer's own transaction.)
            await store.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown mid-handling — swallow. RunMaintenancePassAsync reverts the WHOLE
            // claimed batch (this in-flight step included) right after its loop breaks, so a
            // single revert path covers both "never started" and "interrupted midway".
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex,
                "Failed to process expired waiting step {StepId}; parking it back to waiting for a retry.",
                stepId);
            await this.TryRevertExpiredStepAsync(stepId, failure: ex.Message);
        }
    }

    /// <summary>
    /// Runs <see cref="WorkflowMaintenanceSweeper.RevertExpiredStepAsync"/> in a fresh scope
    /// (the failed handler's scope died with its staged junk). Best-effort: if the revert itself
    /// fails (DB fully down), the wedge is logged loud for an operator.
    /// </summary>
    private async Task TryRevertExpiredStepAsync(Guid stepId, string? failure)
    {
        try
        {
            await using var scope = this.scopeFactory.CreateAsyncScope();
            var sweeper = scope.ServiceProvider.GetRequiredService<WorkflowMaintenanceSweeper>();
            await sweeper.RevertExpiredStepAsync(stepId, failure);
        }
        catch (Exception revertEx)
        {
            this.logger.LogError(revertEx,
                "Failed to park expired step {StepId} back to waiting — row stays 'running' until an operator intervenes (no automatic reaper covers a running step under a suspended run).",
                stepId);
        }
    }
}
