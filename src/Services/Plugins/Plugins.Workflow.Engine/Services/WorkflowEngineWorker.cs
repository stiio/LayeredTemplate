using System.Diagnostics;
using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;
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
    /// <see cref="ExecuteOneAsync(WorkflowStepRecord, IWorkflowStore, IActionTypeRegistry, IWorkflowFanOut, WorkflowStepLane, CancellationToken)"/>,
    /// and flushes. A fresh scope per step gives per-request-style scoped-service lifetimes and
    /// full failure isolation: on any error the scope is simply disposed unsaved, leaving the row
    /// 'running' for the release path to revert. Returns true when the step's outcome was durably
    /// persisted (or the row vanished and there is nothing to release).
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
            var registry = scope.ServiceProvider.GetRequiredService<IActionTypeRegistry>();
            var fanOut = scope.ServiceProvider.GetRequiredService<IWorkflowFanOut>();
            var resolver = scope.ServiceProvider.GetRequiredService<IExpressionResolver>();

            var step = await store.GetStepAsync(stepId, ct);
            if (step is null)
            {
                // Vanished between claim and load (tenant purge / external delete). Nothing to
                // execute and no row to revert — treat as processed.
                return true;
            }

            await this.ExecuteOneAsync(step, store, registry, fanOut, lane, ct, resolver);
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
            // ExecuteOneAsync's inner action try/catch, …). Scope disposed unsaved; the release
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
            var fanOut = scope.ServiceProvider.GetRequiredService<IWorkflowFanOut>();

            var step = await store.GetStepAsync(stepId, CancellationToken.None);
            if (step is null) return true;

            var timeoutResult = ActionExecutionResult.OnError(
                lane == WorkflowStepLane.LongOnly
                    ? $"Step did not complete within the long-lane action timeout ({timeoutSeconds}s). "
                        + "Raise LongLaneActionTimeoutSeconds (or set it to 0) if the action legitimately runs longer."
                    : $"Step did not complete within the fast-lane action timeout ({timeoutSeconds}s). "
                        + "Actions that legitimately run this long should declare IsLongRunning = true.",
                transient: true);
            await this.ApplyResultAsync(step, timeoutResult, store, fanOut, CancellationToken.None);
            await store.SaveChangesAsync(CancellationToken.None);
            this.logger.LogWarning(
                "Step {StepId} hit the {Lane}-lane action timeout ({Timeout}s) on attempt {Attempt}/{Max}; timeout outcome applied.",
                stepId, FormatLane(lane), timeoutSeconds, step.AttemptCount, this.settings.MaxAttempts);
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.RunMaintenancePassAsync(stoppingToken);

                if (this.settings.BookmarkSweepIntervalSeconds > 0 && DateTime.UtcNow >= nextBookmarkSweep)
                {
                    // Own lightweight scope — set-based DELETE, no action code involved. Deletes
                    // bookmarks whose target step is no longer Waiting (resumed elsewhere,
                    // timed-out, dead-lettered, cancelled). This is what makes bookmark cleanup
                    // CORRECT regardless of path; the eager delete-on-resume in the signaler is
                    // just an optimization. No-ops when nothing's stale.
                    await using var bookmarkScope = this.scopeFactory.CreateAsyncScope();
                    var bookmarkStore = bookmarkScope.ServiceProvider.GetRequiredService<IWorkflowStore>();
                    await this.SweepResolvedBookmarksAsync(bookmarkStore, stoppingToken);
                    nextBookmarkSweep = DateTime.UtcNow.AddSeconds(this.settings.BookmarkSweepIntervalSeconds);
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
    /// dispatch. Bookmark reconciliation is NOT part of the pass — the loop runs it on its own
    /// rarer cadence (<see cref="WorkflowEngineSettings.BookmarkSweepIntervalSeconds"/>).
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
            var registry = scope.ServiceProvider.GetRequiredService<IActionTypeRegistry>();
            var fanOut = scope.ServiceProvider.GetRequiredService<IWorkflowFanOut>();
            var resolver = scope.ServiceProvider.GetRequiredService<IExpressionResolver>();

            await this.HandleExpiredStepAsync(stepId, store, registry, fanOut, ct, resolver);
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
    /// Compensating write after timeout handling didn't complete. The sweep's claim flipped the
    /// step Waiting → Running via committed raw SQL; without this revert the row would be stuck
    /// in 'running' FOREVER — its run sits in Suspended (which the stale-running fail
    /// deliberately skips) and no claim path ever touches non-pending rows. Best-effort in a
    /// fresh scope; if the revert itself fails (DB fully down), the wedge is logged loud for an
    /// operator.
    /// </summary>
    /// <param name="failure">
    /// The handler failure message, or null for a shutdown interruption. A failure consumes an
    /// attempt (so a deterministically-broken timeout hook dead-letters at MaxAttempts instead
    /// of retrying forever) and re-parks with backoff; a shutdown re-parks immediately with no
    /// attempt consumed.
    /// </param>
    private async Task TryRevertExpiredStepAsync(Guid stepId, string? failure)
    {
        try
        {
            await using var scope = this.scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IWorkflowStore>();
            var fanOut = scope.ServiceProvider.GetRequiredService<IWorkflowFanOut>();
            await this.RevertExpiredStepCoreAsync(stepId, failure, store, fanOut);
        }
        catch (Exception revertEx)
        {
            this.logger.LogError(revertEx,
                "Failed to park expired step {StepId} back to waiting — row stays 'running' until an operator intervenes (no automatic reaper covers a running step under a suspended run).",
                stepId);
        }
    }

    /// <summary>Core of the expired-step revert — internal seam for the test harness.</summary>
    internal async Task RevertExpiredStepCoreAsync(
        Guid stepId, string? failure, IWorkflowStore store, IWorkflowFanOut fanOut)
    {
        var step = await store.GetStepAsync(stepId, CancellationToken.None);
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
            store.UpdateStep(step);
            await store.SaveChangesAsync(CancellationToken.None);
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
            store.UpdateStep(step);
            await fanOut.CheckRunCompletionAsync(step, CancellationToken.None);
            await store.SaveChangesAsync(CancellationToken.None);
            this.logger.LogError(
                "Expired step {StepId} dead-lettered after {Attempts}/{Max} failed timeout-handling attempt(s): {Failure}",
                stepId, step.AttemptCount, this.settings.MaxAttempts, failure);
            return;
        }

        step.Status = StepExecutionStatus.Waiting;
        step.NextAttemptAt = DateTime.UtcNow.Add(this.BackoffFor(step.AttemptCount));
        store.UpdateStep(step);
        await store.SaveChangesAsync(CancellationToken.None);
        this.logger.LogWarning(
            "Expired step {StepId} parked back to waiting (attempt {Attempt}/{Max}); timeout handling retries at {NextAttemptAt:o}.",
            stepId, step.AttemptCount, this.settings.MaxAttempts, step.NextAttemptAt);
    }

    /// <summary>
    /// Internal for the test harness — runs ONE timeout-sweep pass (claim expired waiting steps →
    /// per-action <c>OnStepTimedOutAsync</c> → <see cref="ApplyResultAsync"/>) on the supplied
    /// store without spinning up a scope factory + hosted service. Production callers go through
    /// <see cref="RunMaintenancePassAsync"/>, which wraps each step in its own scope. Mirrors the
    /// <see cref="ExecuteOneAsync"/> test seam.
    /// </summary>
    internal async Task SweepExpiredWaitingStepsOnceAsync(
        IWorkflowStore store,
        IActionTypeRegistry registry,
        IWorkflowFanOut fanOut,
        CancellationToken ct,
        IExpressionResolver? resolver = null)
    {
        var expiredIds = await store.ClaimExpiredWaitingStepIdsAsync(this.settings.BatchSize, ct);
        foreach (var stepId in expiredIds)
        {
            if (ct.IsCancellationRequested) break;
            await this.HandleExpiredStepAsync(stepId, store, registry, fanOut, ct, resolver);
        }

        // Same shutdown remainder-revert as the production pass (see RunMaintenancePassAsync).
        if (ct.IsCancellationRequested)
        {
            foreach (var stepId in expiredIds)
            {
                await this.RevertExpiredStepCoreAsync(stepId, failure: null, store, fanOut);
            }
        }
    }

    /// <summary>
    /// Core of one expired step's timeout handling — shared by the production per-scope path and
    /// the test seam. Tracked-loads the claimed step and routes it through its action's timeout
    /// policy; stages mutations on <paramref name="store"/> without flushing.
    /// </summary>
    private async Task HandleExpiredStepAsync(
        Guid stepId,
        IWorkflowStore store,
        IActionTypeRegistry registry,
        IWorkflowFanOut fanOut,
        CancellationToken ct,
        IExpressionResolver? resolver = null)
    {
        var step = await store.GetStepAsync(stepId, ct);
        if (step is null) return;

        // Per-action policy: every action's OnStepTimedOutAsync decides the outcome. Suspending
        // actions override it to fire a graceful port (Delay → done, WaitSignal / RunWorkflow →
        // timedOut); the base default raises a non-transient OnError, landing the step in Dead
        // with a generic message.
        var actionType = registry.TryGet(step.Kind);
        if (actionType is null)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Step '{step.Kind}' timed out while waiting and the action kind is unknown.";
            step.CompletedAt = DateTime.UtcNow;
            store.UpdateStep(step);
            await fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        await this.HandleTimeoutGracefullyAsync(step, store, fanOut, actionType, ct, resolver);
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
        CancellationToken ct,
        IExpressionResolver? resolver = null)
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
            if (resolver is not null)
            {
                // Same late transient resolution as the execute path — a timeout hook may read
                // config. The timeout fire is one-shot (no retry bookkeeping), so a resolution
                // failure lands in the catch below: Dead with the reason recorded.
                await resolver.ResolveTransientAsync(
                    configObj,
                    () => ExpressionModelBuilder.Build(run.StaticContext, run.StepsOutputs),
                    ExpressionModelBuilder.EvaluationContextForRun(run),
                    ct);
            }

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

    /// <param name="resolver">
    /// Execute-time half of the two-phase expression resolution — materialises transient config
    /// fields just before the action runs. Optional (trailing) so test-harness callers that
    /// exercise configs without transient fields don't have to wire an engine stack; the
    /// production path always passes the scope's resolver.
    /// </param>
    internal async Task ExecuteOneAsync(
        WorkflowStepRecord step,
        IWorkflowStore store,
        IActionTypeRegistry registry,
        IWorkflowFanOut fanOut,
        WorkflowStepLane lane,
        CancellationToken ct,
        IExpressionResolver? resolver = null)
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
                if (resolver is not null)
                {
                    // Late resolution of transient config fields (secrets / heavy payloads) —
                    // deliberately left unresolved at enqueue and never persisted. Inside this
                    // try on purpose: a resolution failure (secret-store blip, bad expression)
                    // flows through the same catch as an action exception — transient error,
                    // retry / dead-letter path. Model factory is only invoked when the config
                    // actually has a transient leaf.
                    await resolver.ResolveTransientAsync(
                        configObj,
                        () => ExpressionModelBuilder.Build(run.StaticContext, run.StepsOutputs),
                        ExpressionModelBuilder.EvaluationContextForRun(run),
                        ct);
                }

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
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Lane timeout or shutdown drain — the outcome policy lives in ProcessBatchAsync,
                // which can see stoppingToken and distinguish the two (timeout ⇒ count the
                // attempt; shutdown ⇒ release the claim). Tag the span while the action activity
                // is still in scope, then let the cancellation propagate.
                actionActivity?.SetStatus(ActivityStatusCode.Error, "Cancelled (lane timeout or shutdown drain)");
                actionActivity?.SetTag(WorkflowTags.ActionResultType, "Cancelled");
                throw;
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

        // CT.None: the action has run — its outcome (fired port, outputs, run mutations) must be
        // applied and staged even if the lane deadline or shutdown drain fires right after the
        // action body returns. Cancelling bookkeeping here would discard a computed result and
        // re-run the action's side effects on the next claim.
        await this.ApplyResultAsync(step, result, store, fanOut, CancellationToken.None);
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
            // Non-transient errors (e.g. FailRun) skip retries — exhausted immediately.
            var exhausted = !result.IsTransient || step.AttemptCount >= this.settings.MaxAttempts;

            if (exhausted && !string.IsNullOrEmpty(result.RetryExhaustedPort))
            {
                // Author-declared fallback branch: attempts are spent (or the failure was
                // deterministic), but the action told the engine where the run should go in
                // that case — complete the step on the fallback port instead of dead-lettering
                // the whole run. LastError stays on the row, so the trace shows the failed
                // attempts AND the branch taken; Outputs carry the LAST attempt's error
                // payload, merged into steps_outputs like any completion so the fallback
                // branch can read them via steps.<key>.*.
                step.Status = StepExecutionStatus.Completed;
                step.OutputPort = result.RetryExhaustedPort;
                step.Outputs = ToJsonElement(result.Outputs);
                step.CompletedAt = DateTime.UtcNow;
                store.UpdateStep(step);
                this.logger.LogWarning(
                    "Step failed {AttemptCount}/{MaxAttempts} attempt(s) (transient={Transient}); taking fallback port '{Port}': {Error}",
                    step.AttemptCount,
                    this.settings.MaxAttempts,
                    result.IsTransient,
                    result.RetryExhaustedPort,
                    result.Error);
                await fanOut.EnqueueNextStepAsync(step, result.RetryExhaustedPort, ct);
                await fanOut.CheckRunCompletionAsync(step, ct);
            }
            else if (exhausted)
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
                // failure must wire to an Error-kind port the action returns explicitly (or
                // use RetryExhaustedPort, which takes the branch above instead of Dead).
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
