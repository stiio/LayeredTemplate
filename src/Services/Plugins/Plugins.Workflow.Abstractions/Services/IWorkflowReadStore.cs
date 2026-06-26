using LayeredTemplate.Plugins.Workflow.Abstractions.Models;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Read-only slice of the workflow store. Carries every <c>Get*</c>, <c>Find*</c>, <c>List*</c>,
/// <c>Count*</c> method — i.e. operations that don't mutate state and don't need a unit of
/// work to flush. App-side handlers that just project workflow data into DTOs depend on this
/// instead of the full <see cref="IWorkflowStore"/> so their ctors don't see the write surface
/// they don't use.
/// <para>
/// Implementations must be safe to call without a preceding <c>SaveChangesAsync</c> — they
/// either issue independent queries or work against the change tracker's already-committed
/// state. EF Core's <see cref="IWorkflowStore"/> implementation satisfies this naturally because
/// every method here is <c>AsNoTracking</c>-flavoured.
/// </para>
/// </summary>
public interface IWorkflowReadStore
{
    // ===== Definitions =====

    /// <summary>Locator-shape definition lookup; returns null when nothing matches the (tenant, owner, trigger) tuple.</summary>
    Task<WorkflowDefinition?> FindDefinitionAsync(
        Guid tenantId,
        string ownerKind,
        Guid? ownerId,
        string triggerKind,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads a definition by primary key. Used by <see cref="IWorkflowRestarter"/> in
    /// <c>UseCurrentDefinition</c> mode — the restarter has only the <c>DefinitionId</c> from
    /// the old run, not the (ownerKind, ownerId, triggerKind) tuple. Returns null when the
    /// definition has been deleted; restart surfaces that as <c>DefinitionGone</c>.
    /// </summary>
    Task<WorkflowDefinition?> GetDefinitionByIdAsync(Guid definitionId, CancellationToken cancellationToken);

    /// <summary>
    /// Paged list of definitions matching <paramref name="filter"/>. Ordered by
    /// <c>CreatedAt DESC</c>. The result includes both the page slice and the total row count
    /// across all pages so admin UIs can render full paginators.
    /// </summary>
    Task<WorkflowPagedResult<WorkflowDefinition>> ListDefinitionsAsync(
        WorkflowDefinitionFilter filter,
        CancellationToken cancellationToken);

    // ===== Runs =====

    Task<WorkflowRunRecord?> GetRunAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>
    /// Lookup by trigger source — used by traces (e.g. <c>GET submissions/{id}/workflow-run</c>).
    /// <paramref name="tenantId"/> is required for defense-in-depth: even if the caller's
    /// authorisation already scoped the source to the right workspace, the store enforces the
    /// match so a stray cross-tenant <c>(triggerSourceKind, triggerSourceId)</c> collision
    /// cannot leak.
    /// </summary>
    Task<WorkflowRunRecord?> FindRunByTriggerSourceAsync(
        Guid tenantId,
        string triggerSourceKind,
        Guid triggerSourceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// All runs for a given trigger source, newest first. Used by the dashboard run-history view
    /// where a single submission can have multiple runs (e.g. one <c>SubmissionCompleted</c> +
    /// many <c>SubmissionUpdated</c> from dashboard edits). Tenant-scoped.
    /// </summary>
    Task<IReadOnlyList<WorkflowRunRecord>> ListRunsByTriggerSourceAsync(
        Guid tenantId,
        string triggerSourceKind,
        Guid triggerSourceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Paged list of runs matching <paramref name="filter"/>. Ordered by <c>CreatedAt DESC</c>
    /// (driven by the <c>(tenant_id, created_at DESC)</c> index). Result includes total count
    /// across all pages for full paginator UI.
    /// </summary>
    Task<WorkflowPagedResult<WorkflowRunRecord>> ListRunsAsync(
        WorkflowRunFilter filter,
        CancellationToken cancellationToken);

    /// <summary>
    /// Number of <i>direct</i> child runs spawned by <paramref name="parentRunId"/>. Counts
    /// every status — once a slot is taken it stays taken so a loop spawning failing children
    /// can't bypass the per-run cap. Grand-children are not counted (each run has its own quota).
    /// </summary>
    Task<int> CountChildRunsAsync(Guid parentRunId, CancellationToken cancellationToken);

    /// <summary>
    /// Cross-tenant existence check: did ANY run (any tenant, any status, dry or real) reference
    /// <paramref name="definitionId"/>? Deliberately <b>not</b> tenant-scoped — system-workflow
    /// runs execute under the operators' workspace tenants, not the definition's owning tenant
    /// (ADR-028 §4), so a tenant-filtered query would falsely report zero. Used as the delete
    /// guard for system workflows: a RESTRICT FK on <c>workflow_runs.definition_id</c> would
    /// otherwise turn the delete into a 500 instead of a clean 4xx.
    /// </summary>
    Task<bool> AnyRunsForDefinitionAsync(Guid definitionId, CancellationToken cancellationToken);

    // ===== Steps =====

    Task<IReadOnlyList<WorkflowStepRecord>> GetStepsForRunAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>
    /// Single-step lookup by id. Used by the resume path so the API can validate ownership /
    /// status before mutating. Returns null when the step doesn't exist.
    /// </summary>
    Task<WorkflowStepRecord?> GetStepAsync(Guid stepId, CancellationToken cancellationToken);
}
