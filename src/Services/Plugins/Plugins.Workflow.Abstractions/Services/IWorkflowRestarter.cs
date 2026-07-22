namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Manual replay of a previously-executed workflow run. Creates a <i>new</i> run with the same
/// static context (variables + trigger metadata) as the old one; the old run is not modified
/// — the chain is implicit through the shared <c>TriggerSourceId</c> ordering in
/// <see cref="IWorkflowStore.ListRunsAsync"/> (trigger-source filters).
/// <para>
/// Two modes — see <see cref="WorkflowRestartMode"/>: <c>UseSnapshot</c> replays the exact
/// graph the old run was started against (frozen in <c>workflow_runs.workflow_snapshot</c>);
/// <c>UseCurrentDefinition</c> re-fetches the live <c>WorkflowDefinition</c> by Id, so a
/// post-incident graph fix can be replayed against the original input.
/// </para>
/// <para>
/// Restart is always top-level: <c>NestingLevel=0</c>, no <c>ParentRunId</c>/<c>ParentStepId</c>.
/// If the old run was a sub-workflow child, restart does NOT re-attach to whatever parent step
/// previously waited on it — that parent has long since moved on. The new run stands alone.
/// </para>
/// <para>
/// No restriction on the source run's status: terminal (<c>Completed</c>/<c>Failed</c>) and
/// active (<c>Running</c>/<c>Suspended</c>) runs can both be restarted. The old run keeps
/// running independently. Operators who want to abort the old run before starting a fresh
/// one chain Cancel + Restart explicitly.
/// </para>
/// </summary>
public interface IWorkflowRestarter
{
    Task<WorkflowRestartResult> RestartAsync(WorkflowRestartCommand command, CancellationToken cancellationToken);
}

public record WorkflowRestartCommand
{
    public required Guid RunId { get; init; }

    /// <summary>Tenant the caller is acting on behalf of — must match the run's stored tenant.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Snapshot vs current-definition. Default: snapshot (preserves original behaviour).</summary>
    public WorkflowRestartMode Mode { get; init; } = WorkflowRestartMode.UseSnapshot;

    /// <summary>
    /// Optional actor id stamped on the new run as <c>ActorUserId</c> — typically the user
    /// who clicked "Restart". Different from the original run's actor; that's preserved on the
    /// untouched original record.
    /// </summary>
    public Guid? ActorUserId { get; init; }
}

public enum WorkflowRestartMode
{
    /// <summary>
    /// Replay the graph frozen in <c>workflow_runs.workflow_snapshot</c>. Bit-for-bit replay
    /// of the original workflow logic, regardless of subsequent edits to the
    /// <see cref="Models.WorkflowDefinition"/>.
    /// </summary>
    UseSnapshot,

    /// <summary>
    /// Re-fetch the live <c>WorkflowDefinition</c> by id and replay against the current graph.
    /// If the definition was deleted, restart returns <see cref="WorkflowRestartOutcome.DefinitionGone"/>.
    /// </summary>
    UseCurrentDefinition,
}

public enum WorkflowRestartOutcome
{
    /// <summary>New run was created. <see cref="WorkflowRestartResult.NewRunId"/> is set.</summary>
    Started,

    /// <summary>Old run id doesn't exist or belongs to a different tenant.</summary>
    NotFound,

    /// <summary>Mode = <c>UseCurrentDefinition</c> but the definition row was deleted.</summary>
    DefinitionGone,

    /// <summary>Mode = <c>UseSnapshot</c> but <c>workflow_snapshot</c> failed to parse.</summary>
    SnapshotMalformed,

    /// <summary>Resolved graph has no nodes / no start node — nothing to run.</summary>
    EmptyGraph,
}

public class WorkflowRestartResult
{
    public WorkflowRestartOutcome Outcome { get; init; }

    public Guid? NewRunId { get; init; }

    public Guid? OldRunId { get; init; }

    public bool Started => this.Outcome == WorkflowRestartOutcome.Started;

    public static WorkflowRestartResult NotFound() =>
        new() { Outcome = WorkflowRestartOutcome.NotFound };

    public static WorkflowRestartResult DefinitionGone(Guid oldRunId) =>
        new() { Outcome = WorkflowRestartOutcome.DefinitionGone, OldRunId = oldRunId };

    public static WorkflowRestartResult SnapshotMalformed(Guid oldRunId) =>
        new() { Outcome = WorkflowRestartOutcome.SnapshotMalformed, OldRunId = oldRunId };

    public static WorkflowRestartResult EmptyGraph(Guid oldRunId) =>
        new() { Outcome = WorkflowRestartOutcome.EmptyGraph, OldRunId = oldRunId };

    public static WorkflowRestartResult StartedAt(Guid oldRunId, Guid newRunId) =>
        new() { Outcome = WorkflowRestartOutcome.Started, OldRunId = oldRunId, NewRunId = newRunId };
}
