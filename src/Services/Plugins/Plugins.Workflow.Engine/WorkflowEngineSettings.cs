using LayeredTemplate.Plugins.Workflow.Abstractions.Services;

namespace LayeredTemplate.Plugins.Workflow.Engine;

/// <summary>
/// Engine knobs configurable per consumer. Bound from configuration via the standard
/// <c>services.Configure&lt;WorkflowEngineSettings&gt;(...)</c> pattern; <c>AddWorkflowCore</c>
/// wires the binding from a default <c>"WorkflowEngineSettings"</c> section.
/// </summary>
public class WorkflowEngineSettings
{
    /// <summary>Total attempts per step (1 = no retries, fail-fast to 'dead' on first error).</summary>
    public int MaxAttempts { get; set; } = 1;

    public int PollIntervalSeconds { get; set; } = 3;

    /// <summary>
    /// Cadence of the engine's single per-process maintenance loop: the expired-waiting sweep
    /// (fires suspend deadlines — Delay, WaitSignal / RunWorkflow timeouts) and the bookmark
    /// reconciliation sweep. One loop regardless of <see cref="WorkerCount"/> — running these
    /// on every worker pass duplicated identical queries WorkerCount× for no benefit.
    /// <para>
    /// This bounds suspend-deadline granularity: a Delay of N seconds fires between N and
    /// N + this many seconds (a backlog burst drains fully within one pass). Default 5s stays
    /// close to the previous poll-bound behaviour; raise it when deadline precision doesn't
    /// matter.
    /// </para>
    /// </summary>
    public int MaintenanceIntervalSeconds { get; set; } = 5;

    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Backoff (seconds) per attempt index; last value repeats past the tail.
    /// Ignored when <see cref="MaxAttempts"/> = 1.
    /// </summary>
    public int[] BackoffSeconds { get; set; } = new[] { 30, 120, 600, 3600, 21600 };

    /// <summary>
    /// Hard cap on total step executions in one workflow run. When exceeded the run is aborted
    /// with <c>abort_reason = "step_cap"</c>. Safety net against cycles + fan-out explosion.
    /// </summary>
    public int MaxStepsPerRun { get; set; } = 200;

    /// <summary>
    /// Max times a given node may execute in one run. Further edges targeting the same node are
    /// silently skipped (logged). Catches accidental cycles without killing the whole run.
    /// <para>
    /// Bodies of legitimate loops (ForEach) are visited once per iteration, so this cap must be
    /// at least <see cref="MaxLoopIterations"/> — the default 50 leaves a 2× margin over the
    /// default 25 iterations. Nested loops multiply the visit count by the same factor; for
    /// two-level nesting bump this to <c>MaxLoopIterations²</c> or higher.
    /// </para>
    /// </summary>
    public int MaxVisitsPerNode { get; set; } = 50;

    /// <summary>
    /// Hard cap on iterations consumed by a single loop action (ForEach, …). The action's first
    /// call validates the iterable's length against this and surfaces a non-transient error if
    /// the input is bigger — the run goes Dead with a clear "loop input too big" message
    /// instead of silently chewing through thousands of records.
    /// </summary>
    public int MaxLoopIterations { get; set; } = 25;

    /// <summary>
    /// Hard cap on the depth of sub-workflow chains. Top-level runs (form submits, manual API)
    /// start at depth 0; every <c>RunWorkflow</c> child increments. Default 3 lets a parent run
    /// orchestrate two extra levels of children — <c>A → B → C → D</c> with <c>D</c> at depth 3 is
    /// fine, but anything deeper trips <c>WorkflowDispatchOutcome.NestingLimitExceeded</c>. Acts
    /// as a safety net against accidentally recursive workflows.
    /// </summary>
    public int MaxNestingLevel { get; set; } = 1;

    /// <summary>
    /// Hard cap on the number of <i>direct</i> sub-workflow runs a single run can spawn. Counts
    /// every child <c>WorkflowRun</c> with this run as <c>parent_run_id</c>, regardless of the
    /// child's status (running / completed / failed) — once a slot is taken, it stays taken so
    /// loops with a failing child can't bypass the cap. Doesn't count grand-children: each run
    /// has its own independent quota.
    /// <para>
    /// When exceeded, <c>RunWorkflow</c> fires its <c>error</c> port with reason
    /// <c>sub_run_limit_exceeded</c>. Default 3 fits a typical "fan out to a couple of
    /// integrations" workflow; bump it for orchestration-heavy graphs.
    /// </para>
    /// </summary>
    public int MaxSubRunsPerRun { get; set; } = 3;

    /// <summary>
    /// Maximum number of nodes a single workflow definition may contain. Validator rejects
    /// graphs above this cap with <c>graph_too_large</c>. Stops authors from saving 10k-node
    /// monsters that would re-deserialize on every step and bloat <c>workflow_runs.workflow_snapshot</c>.
    /// </summary>
    public int MaxNodesPerGraph { get; set; } = 200;

    /// <summary>
    /// Hard cap on the number of characters a single Liquid render may emit. Throws if a
    /// template's output exceeds this — typical user templates fit within a few KB, generous
    /// upper bound 256 KB covers complex marketing emails / webhook JSON bodies. Going higher
    /// risks per-render heap pressure under <see cref="BatchSize"/>-fold parallel execution
    /// (peak memory ≈ <c>BatchSize × MaxLiquidOutputChars × 2</c> bytes for UTF-16 storage).
    /// </summary>
    public int MaxLiquidOutputChars { get; set; } = 256 * 1024;

    /// <summary>
    /// Number of concurrent worker loops the engine runs inside a single host process. Each
    /// loop independently calls <c>ClaimPendingStepIdsAsync</c> with its own DI scope, so they
    /// scale linearly without coordinating: Postgres' <c>FOR UPDATE SKIP LOCKED</c> prevents
    /// double-claims at the row level.
    /// <para>
    /// Effective worst-case parallelism is <c>WorkerCount × BatchSize</c> in-flight steps. Each
    /// active step holds one DB connection from the pool through its scope, so set
    /// <c>WorkerCount × BatchSize</c> well below your Postgres connection pool size — a 3-worker
    /// × 10-batch config peaks at ~30 connections, comfortable on the default 100-pool.
    /// </para>
    /// <para>
    /// Default 1 keeps single-process throughput at the original ≈ <c>BatchSize / PollIntervalSeconds</c>
    /// steps/sec. Bump this when steps are I/O-bound and the queue grows faster than one loop
    /// can drain it.
    /// </para>
    /// <para>
    /// When <see cref="LongRunningWorkerCount"/> &gt; 0, these workers run in fast-only lane and
    /// long-running rows are picked up by the dedicated long pool — see that property's remarks.
    /// </para>
    /// </summary>
    public int WorkerCount { get; set; } = 1;

    /// <summary>
    /// Optional dedicated worker pool for long-running actions (those whose
    /// <c>IActionType.IsLongRunning</c> is true — typically HTTP requests with multi-second
    /// timeouts, slow S3 transfers, etc). When greater than zero:
    /// <list type="bullet">
    ///   <item>The fast pool (<see cref="WorkerCount"/>) only claims rows with <c>is_long_running = false</c>.</item>
    ///   <item>This pool only claims rows with <c>is_long_running = true</c>.</item>
    ///   <item>Both pools share the same Postgres claim guarantees (FOR UPDATE SKIP LOCKED).</item>
    /// </list>
    /// Default 0: no separation — fast workers claim everything (unchanged from pre-lane behaviour,
    /// fully backward-compatible). Set this when a single 60-second HTTP step routinely starves
    /// the rest of the queue.
    /// <para>
    /// Total worst-case parallelism becomes <c>(WorkerCount + LongRunningWorkerCount) × BatchSize</c>;
    /// budget Postgres connections accordingly.
    /// </para>
    /// </summary>
    public int LongRunningWorkerCount { get; set; } = 0;

    /// <summary>
    /// Grace period (seconds) granted to the <i>currently in-flight action</i> after a host
    /// shutdown signal. Per-action — not per-batch. When SIGTERM fires, the worker:
    /// <list type="number">
    ///   <item>schedules the in-flight action's cancellation token to cancel after this many seconds;</item>
    ///   <item>finishes the current step (action gets up to <c>ShutdownDrainSeconds</c> to finish naturally; if it doesn't, the cancel forces it to surface a transient error);</item>
    ///   <item>releases any further already-claimed-but-untouched steps back to <c>pending</c> so the next worker startup picks them up immediately;</item>
    ///   <item>exits the polling loop without claiming a new batch.</item>
    /// </list>
    /// Pair with <c>HostOptions.ShutdownTimeout</c> on the host (default 30s) — set it to
    /// <c>ShutdownDrainSeconds + a small buffer</c> so the host actually waits for the engine
    /// to drain before terminating the process.
    /// </summary>
    public int ShutdownDrainSeconds { get; set; } = 45;

    /// <summary>
    /// Hard per-action timeout (seconds) for the <i>fast lane</i>. Each fast-lane action gets
    /// at most this long to run before its cancellation token fires and the action surfaces a
    /// transient error. Protects against bugs / runaway calls that would otherwise hold a
    /// fast-pool worker indefinitely and starve the queue. The default 30s comfortably covers
    /// every reasonable Transform/Condition/Switch/Delay-tick — actions whose body legitimately
    /// runs longer should be tagged <c>IsLongRunning = true</c> instead of bumping this.
    /// <para>
    /// Long lane has no per-action timeout — its whole purpose is to host slow operations
    /// (HTTP with multi-second response times, S3 transfers). The only deadline a long-lane
    /// action sees is <see cref="ShutdownDrainSeconds"/> when shutdown begins.
    /// </para>
    /// </summary>
    public int FastLaneActionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Optional automatic retention of finished and stale runs. Disabled by default (both
    /// sub-flags false). When enabled the engine spawns a low-frequency background worker that
    /// runs <see cref="IWorkflowStore.PurgeFinishedRunsAsync"/> /
    /// <see cref="IWorkflowStore.FailStaleRunningRunsAsync"/> on a sweep schedule.
    /// </summary>
    public WorkflowRetentionSettings Retention { get; set; } = new();
}

/// <summary>
/// Knobs for the optional <c>WorkflowRetentionWorker</c>. All destructive — opt-in per flag.
/// </summary>
public class WorkflowRetentionSettings
{
    /// <summary>
    /// When true, periodically purges <c>Completed</c> / <c>Failed</c> runs older than
    /// <see cref="FinishedRunRetentionDays"/>. Step-execution rows cascade-delete with the run.
    /// Default false — destructive, opt-in.
    /// </summary>
    public bool EnableFinishedPurge { get; set; } = false;

    /// <summary>
    /// When true, periodically marks <c>Running</c> runs whose <c>UpdatedAt</c> is older than
    /// <see cref="StaleRunningRetentionDays"/> as <c>Failed</c> with
    /// <c>abort_reason = "stale: …"</c> — orphans from worker / pod crashes. Two-phase by
    /// design: the run and its step history stay inspectable for the incident window instead of
    /// silently vanishing; the finished purge (<see cref="EnableFinishedPurge"/>) deletes them
    /// later like any other failed run. Runs in the <c>Suspended</c> status (parked on Delay /
    /// WaitSignal / RunWorkflow wait) are <i>not</i> touched: the dedicated status keeps them
    /// out of this scan. Default false.
    /// </summary>
    public bool EnableStaleFail { get; set; } = false;

    /// <summary>
    /// How often the retention worker sweeps. Default 12h — purge isn't latency-sensitive,
    /// running it rarely keeps DB write amplification minimal. Each sweep drains the backlog
    /// in <see cref="BatchSize"/>-sized chunks until exhausted, so a long-quiet system catches
    /// up quickly when retention is first enabled.
    /// </summary>
    public int SweepIntervalSeconds { get; set; } = 12 * 3600;

    /// <summary>
    /// <c>Completed</c> / <c>Failed</c> runs whose <c>FinishedAt</c> is older than this are
    /// purged. Default 30 days — typical incident-investigation window.
    /// </summary>
    public int FinishedRunRetentionDays { get; set; } = 30;

    /// <summary>
    /// <c>Running</c> runs whose <c>UpdatedAt</c> is older than this are marked <c>Failed</c>
    /// (see <see cref="EnableStaleFail"/>). Default 7 days — a healthy run advances within
    /// seconds; a week of inactivity strongly implies the worker that owned it died mid-flight
    /// without a final state transition.
    /// </summary>
    public int StaleRunningRetentionDays { get; set; } = 7;

    /// <summary>
    /// Per-iteration batch size. Caps single transaction lock duration; sweep loops over
    /// batches until the backlog is drained.
    /// </summary>
    public int BatchSize { get; set; } = 1000;
}
