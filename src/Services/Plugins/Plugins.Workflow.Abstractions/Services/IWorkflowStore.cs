using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Persistence boundary between the workflow engine and whatever DB / store sits underneath.
/// The engine only talks records; the store handles entity mapping, change tracking, and
/// transaction commit. Methods that take or return mutable records use <c>void</c> for
/// staging (<see cref="AddRun"/>/<see cref="UpdateStep"/>/etc.) — actual I/O happens at
/// <see cref="SaveChangesAsync"/>.
/// <para>
/// Composes <see cref="IWorkflowReadStore"/> (read-only Get/List/Find/Count) and
/// <see cref="IWorkflowRetentionStore"/> (purge methods). Engine internals depend on this
/// composite; App-side read handlers can narrow to <see cref="IWorkflowReadStore"/>;
/// the retention worker narrows to <see cref="IWorkflowRetentionStore"/>. Same EF Core
/// implementation backs all three — DI registers it once and re-binds the narrower
/// interfaces to the same instance.
/// </para>
/// </summary>
/// <remarks>
/// Why some methods aren't async: EF's change tracker is in-memory; queueing inserts/updates
/// has zero I/O. Single async flush at the end keeps transactional semantics simple
/// (caller controls when state hits the wire).
/// </remarks>
public interface IWorkflowStore : IWorkflowReadStore, IWorkflowRetentionStore
{
    // ===== Definitions (write) =====

    /// <summary>
    /// Create or replace the definition matching the (tenant, owner, trigger) key.
    /// <paramref name="displayName"/> is optional and used for human-readable picker UIs;
    /// null leaves an existing display_name unchanged on upsert.
    /// </summary>
    Task UpsertDefinitionAsync(
        Guid tenantId,
        string ownerKind,
        Guid? ownerId,
        string triggerKind,
        WorkflowGraph graph,
        string? displayName,
        CancellationToken cancellationToken);

    Task DeleteDefinitionAsync(
        Guid tenantId,
        string ownerKind,
        Guid? ownerId,
        string triggerKind,
        CancellationToken cancellationToken);

    // ===== Runs (write) =====

    /// <summary>Stage a new run for insert at next <see cref="SaveChangesAsync"/>.</summary>
    void AddRun(WorkflowRunRecord run);

    /// <summary>Apply mutable fields (Status/AbortReason/FinishedAt/StepsOutputs) of the record back to the underlying store.</summary>
    void UpdateRun(WorkflowRunRecord run);

    // ===== Steps (write) =====

    void AddStep(WorkflowStepRecord step);

    void UpdateStep(WorkflowStepRecord step);

    /// <summary>
    /// Atomically resume a <c>Waiting</c> step: set <c>Status = completed</c>, write
    /// <paramref name="outputPort"/> + <paramref name="outputs"/>, stamp <c>CompletedAt</c>.
    /// Returns the updated record on success, or null when the step was not in <c>Waiting</c>
    /// (already resumed by another caller, timed out, or the step doesn't exist) — caller
    /// should treat null as a 409-style conflict.
    /// </summary>
    /// <remarks>
    /// Implementation is expected to use a guarded <c>UPDATE … WHERE status = 'waiting'</c> so
    /// concurrent resume calls can't double-fire downstream steps. The guard executes
    /// immediately (not staged) — callers that need it atomic with their staged follow-ups
    /// (successor enqueue, run-status change) wrap the whole unit of work in
    /// <see cref="BeginTransactionAsync"/>, as <c>IWorkflowResumer</c> does.
    /// </remarks>
    Task<WorkflowStepRecord?> TryResumeWaitingStepAsync(
        Guid stepId,
        string outputPort,
        object? outputs,
        CancellationToken cancellationToken);

    // ===== Worker hot path =====

    /// <summary>
    /// Atomically claim a batch of <c>Pending</c> steps whose <c>NextAttemptAt &lt;= now</c>,
    /// transitioning each to <c>Running</c> and incrementing <c>AttemptCount</c>. Postgres
    /// uses <c>FOR UPDATE SKIP LOCKED</c>; other backends use their equivalent.
    /// Returns the claimed step IDS (empty if nothing pending) — ids only, because the claim
    /// runs in a short-lived scope and each step is then loaded (<see cref="IWorkflowReadStore.GetStepAsync"/>)
    /// and executed in its OWN per-step DI scope. The claim UPDATE commits at the DB level
    /// immediately (raw SQL), so it survives the claiming scope's disposal.
    /// <para>
    /// <paramref name="lane"/> filters by the row's <c>is_long_running</c> column so two worker
    /// pools can run side by side without interfering. <see cref="WorkflowStepLane.Any"/> is the
    /// single-pool default; the engine flips to <see cref="WorkflowStepLane.FastOnly"/> +
    /// <see cref="WorkflowStepLane.LongOnly"/> only when <c>LongRunningWorkerCount &gt; 0</c>.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> ClaimPendingStepIdsAsync(
        int batchSize,
        WorkflowStepLane lane,
        CancellationToken cancellationToken);

    /// <summary>Total executions for the run (saved + locally-staged). Used for max-steps-per-run cap.</summary>
    Task<int> CountStepsForRunAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>
    /// Visit count for ONE node in the run (saved + locally-staged). Used for the
    /// max-visits-per-node cap — fan-out only ever needs the enqueue target's count, so this
    /// stays a targeted <c>COUNT</c> instead of a full per-node histogram of the run.
    /// </summary>
    Task<int> CountVisitsForNodeAsync(Guid runId, string nodeId, CancellationToken cancellationToken);

    /// <summary>
    /// Summary of step states for a run, used by the run-completion check after every step
    /// completion. Must stay cheap regardless of run history: rows tracked by the current scope
    /// (freshly-flipped statuses inside the worker batch) are classified from the tracker, the
    /// untracked remainder is aggregated store-side into the three flags without transferring
    /// rows.
    /// </summary>
    /// <param name="excludingStepId">
    /// Step id whose status is ignored when computing <see cref="WorkflowRunStepStateSummary.HasOngoing"/>
    /// — typically the just-finished step the caller is finalising. <see cref="WorkflowRunStepStateSummary.HasDead"/>
    /// includes it (so a just-Dead step correctly flips the run to Failed).
    /// </param>
    Task<WorkflowRunStepStateSummary> GetStepStateSummaryAsync(
        Guid runId,
        Guid excludingStepId,
        CancellationToken cancellationToken);

    // ===== Suspend / timeout =====

    /// <summary>
    /// Atomically claims expired Waiting steps — those whose <c>NextAttemptAt</c> has passed
    /// while still in <c>Waiting</c> status — by flipping them to <c>Running</c> in a single
    /// SQL with <c>FOR UPDATE SKIP LOCKED</c>. Mirrors the
    /// <see cref="ClaimPendingStepIdsAsync"/> pattern (ids only — each expired step is then
    /// handled in its own per-step DI scope) so multi-process setups never see two sweepers
    /// fire <c>OnStepTimedOutAsync</c> for the same step. No lane filter — the engine's single
    /// maintenance loop sweeps timeouts for both lanes.
    /// <para>
    /// Caller (engine maintenance loop) drives the timeout outcome via the action's
    /// <c>OnStepTimedOutAsync</c> and then routes through the regular
    /// <c>ApplyResultAsync</c>, which terminates the step (<c>Completed</c>/<c>Dead</c>)
    /// just like an ordinary execution.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> ClaimExpiredWaitingStepIdsAsync(
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reverts steps that were claimed (status <c>running</c>, attempt count incremented) but
    /// the worker never got to actually execute — typically because a host shutdown signal
    /// arrived mid-batch. Resets each row to <c>pending</c> and decrements <c>attempt_count</c>
    /// so the upcoming retry isn't penalized for a non-attempt.
    /// <para>
    /// Guarded by <c>WHERE status = 'running'</c>: rows that have already moved on (cancel
    /// flipped them to Dead, another worker picked them up somehow, etc.) are left alone.
    /// Returns the count of rows actually released so the caller can log realistic numbers.
    /// </para>
    /// </summary>
    Task<int> ReleaseClaimedStepsAsync(
        IReadOnlyList<Guid> stepIds,
        CancellationToken cancellationToken);

    // ===== Bookmarks (generic signal-wait) =====

    /// <summary>
    /// Stage one bookmark row per registration for insert at the next <see cref="SaveChangesAsync"/>.
    /// Called by the worker in the suspend branch so the bookmarks persist on the SAME flush as the
    /// step's transition to <c>Waiting</c> — there's never a window where the step is parked but its
    /// bookmarks are missing (or vice versa). Each row freezes the exact <c>(RunId, StepId)</c> the
    /// suspend happened on plus the registration's <c>CorrelationKey</c> / <c>ResumePort</c>; a
    /// later <c>IWorkflowSignaler.SignalAsync</c> resumes that exact frozen step, never a re-derived one.
    /// </summary>
    void AddBookmarks(WorkflowStepRecord step, IReadOnlyList<WorkflowBookmarkRegistration> registrations);

    /// <summary>
    /// Find every bookmark in <paramref name="tenantId"/> matching <paramref name="correlationKey"/>
    /// (exact string). Tenant-scoped — MANDATORY: a key in another tenant must never surface here.
    /// Used by the signaler fan-out; the matched bookmarks drive per-step resumes.
    /// </summary>
    Task<IReadOnlyList<WorkflowBookmarkRecord>> FindBookmarksAsync(
        Guid tenantId, string correlationKey, CancellationToken cancellationToken);

    /// <summary>
    /// Hard-delete the bookmark rows with the given ids. Eager cleanup the signaler issues after a
    /// resume / stale outcome; correctness of cleanup ultimately rests on
    /// <see cref="SweepResolvedBookmarksAsync"/>, not on this optimization. No-op on empty input.
    /// </summary>
    Task<int> DeleteBookmarksAsync(IReadOnlyList<Guid> bookmarkIds, CancellationToken cancellationToken);

    /// <summary>
    /// Reconciliation backstop: delete every bookmark whose target step is no longer in
    /// <c>Waiting</c> — resumed, timed-out, dead-lettered, or cancelled by any path. A single
    /// set-based <c>DELETE … USING workflow_step_executions</c>. Run on the same cadence as the
    /// timeout sweeper. This — not the eager delete-on-resume — is what makes bookmark cleanup
    /// CORRECT regardless of which path retired the step. Returns the count deleted.
    /// </summary>
    Task<int> SweepResolvedBookmarksAsync(int limit, CancellationToken cancellationToken);

    // ===== Atomic commit =====

    /// <summary>
    /// Opens an explicit storage transaction so a unit of work that mixes immediate SQL
    /// (<see cref="TryResumeWaitingStepAsync"/>'s guard) with staged mutations and a final
    /// <see cref="SaveChangesAsync"/> commits atomically. Returns <c>null</c> when a transaction
    /// is already active on this scope's store — the caller is then a nested participant: its
    /// statements and flush join the ambient transaction and the ambient owner commits.
    /// Disposing the handle without <see cref="IWorkflowStoreTransaction.CommitAsync"/> rolls
    /// the transaction back.
    /// </summary>
    Task<IWorkflowStoreTransaction?> BeginTransactionAsync(CancellationToken cancellationToken);

    /// <summary>Flush staged inserts/updates. Call once per logical unit-of-work.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Handle for an explicit storage transaction from <see cref="IWorkflowStore.BeginTransactionAsync"/>.
/// Commit explicitly; disposing an uncommitted handle rolls the transaction back.
/// </summary>
public interface IWorkflowStoreTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
