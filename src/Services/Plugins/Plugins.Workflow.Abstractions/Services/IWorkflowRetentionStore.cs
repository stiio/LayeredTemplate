namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Destructive purge slice of the workflow store. Surfaces only the <c>Purge*</c> operations,
/// isolated so <see cref="WorkflowRetentionWorker"/> and any future admin tooling don't take
/// a dependency on the engine's full read/write surface to delete old rows.
/// <para>
/// Each method commits directly — it does NOT stage in the change tracker. There's no
/// <c>SaveChangesAsync</c> to call afterwards. Implementations are expected to issue
/// <c>ExecuteDelete</c>-style bulk SQL keyed off an index, batched by <c>limit</c>; callers
/// loop until the return value is 0 to drain a backlog without locking the table for long.
/// </para>
/// </summary>
public interface IWorkflowRetentionStore
{
    /// <summary>
    /// Removes runs in terminal status (<c>Completed</c>/<c>Failed</c>) whose <c>FinishedAt</c>
    /// is older than <paramref name="olderThan"/>. Step executions cascade with the run.
    /// Returns the number of runs deleted; loop until 0 to drain a backlog without holding one
    /// big transaction. When <paramref name="tenantId"/> is provided, the purge is scoped to
    /// that tenant only; otherwise it's global.
    /// </summary>
    /// <remarks>
    /// Does NOT touch <c>Running</c> runs (stuck/in-flight runs are a separate recovery concern)
    /// or <c>workflow_definitions</c> (those are config, not runtime state).
    /// </remarks>
    Task<int> PurgeFinishedRunsAsync(
        DateTime olderThan,
        int limit,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes ALL runs (any status, any age) for <paramref name="tenantId"/>, batched by
    /// <paramref name="limit"/>. Step executions cascade with the run. Returns the number of
    /// runs deleted; loop until 0 to drain. Intended for tenant-level "delete all PHI" /
    /// right-to-erasure flows. Definitions are not touched — call
    /// <c>IWorkflowStore.DeleteDefinitionAsync</c> separately if the workspace itself is being wiped.
    /// </summary>
    /// <remarks>
    /// In-flight runs may still have a step that gets claimed mid-purge by a concurrent worker;
    /// the worker's <c>ExecuteOneAsync</c> handles a missing run by dead-lettering the step.
    /// </remarks>
    Task<int> PurgeAllForTenantAsync(
        Guid tenantId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes runs in <c>Running</c> status whose last activity (<c>UpdatedAt</c>) is older than
    /// <paramref name="olderThan"/>. Step executions cascade. Catches runs stuck because of a
    /// crashed worker, dropped tenant config, never-arriving Join inputs without configured
    /// timeout, etc. Optionally scope by <paramref name="tenantId"/>.
    /// </summary>
    Task<int> PurgeStaleRunningRunsAsync(
        DateTime olderThan,
        int limit,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes ALL runs (any status, any age) attached to <paramref name="definitionId"/> within
    /// <paramref name="tenantId"/>. Step executions cascade. Returns the number of runs deleted.
    /// Intended to be called <b>before</b> <c>IWorkflowStore.DeleteDefinitionAsync</c> when an
    /// author removes a workflow from a form / disables it / deletes the owning resource — the
    /// FK on <c>workflow_runs.definition_id</c> is <c>RESTRICT</c>, so the definition can't be
    /// dropped while runs reference it. Calling this method is the explicit decision point for
    /// "the run history (and any PHI it carries) goes with the workflow definition."
    /// </summary>
    /// <remarks>
    /// Not batched on <paramref name="limit"/> for backlog drain like the other purge methods —
    /// expected scale is small (per-form history at most), and the caller wants a single
    /// transactional sweep co-ordinated with the definition delete. <paramref name="limit"/>
    /// is still honoured as a safety cap; callers that genuinely have huge backlogs should
    /// loop manually.
    /// </remarks>
    Task<int> PurgeRunsByDefinitionAsync(
        Guid tenantId,
        Guid definitionId,
        int limit,
        CancellationToken cancellationToken = default);
}
