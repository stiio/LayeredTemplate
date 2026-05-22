namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Operator-driven termination of a workflow run. Sets the run to <c>Failed</c>, marks every
/// active step (<c>Pending</c>/<c>Running</c>/<c>Waiting</c>) as <c>Dead</c>, and drives the
/// sub-workflow auto-resume so a parent run waiting on this child sees the cancellation as a
/// <c>failed</c>-port outcome with <c>childAbortReason = "cancelled: ..."</c>.
/// <para>
/// Cancel is idempotent at the run level: a second call on an already-terminal run returns
/// <see cref="WorkflowCancelOutcome.AlreadyTerminal"/> without mutating state.
/// </para>
/// <para>
/// In-flight <c>Running</c> steps complete their current action invocation — we can't interrupt
/// a host call mid-flight (HTTP / DB / etc. don't expose a cooperative cancel). But once the
/// action returns, the worker's run-status guard prevents fan-out into successor steps, and the
/// step's eventual write loses to our atomic <see cref="WorkflowCancelOutcome.Cancelled"/>
/// transition for any unclaimed Pending/Waiting siblings.
/// </para>
/// </summary>
public interface IWorkflowCanceller
{
    Task<WorkflowCancelResult> CancelAsync(WorkflowCancelCommand command, CancellationToken cancellationToken);
}

public record WorkflowCancelCommand
{
    public required Guid RunId { get; init; }

    /// <summary>Tenant the caller is acting on behalf of — must match the run's stored tenant.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// Optional human-readable reason, recorded as the run's <c>AbortReason</c> and on every
    /// killed step's <c>LastError</c>. Trimmed to a safe length internally.
    /// </summary>
    public string? Reason { get; init; }
}

public enum WorkflowCancelOutcome
{
    /// <summary>Run was active (Running/Suspended) and is now Failed; active steps killed.</summary>
    Cancelled,

    /// <summary>Run id doesn't exist or belongs to a different tenant.</summary>
    NotFound,

    /// <summary>Run is already in a terminal status (Completed/Failed). No mutation; caller treats as success.</summary>
    AlreadyTerminal,
}

public class WorkflowCancelResult
{
    public WorkflowCancelOutcome Outcome { get; init; }

    public bool Succeeded => this.Outcome == WorkflowCancelOutcome.Cancelled;

    public static WorkflowCancelResult Cancelled() =>
        new() { Outcome = WorkflowCancelOutcome.Cancelled };

    public static WorkflowCancelResult NotFound() =>
        new() { Outcome = WorkflowCancelOutcome.NotFound };

    public static WorkflowCancelResult AlreadyTerminal() =>
        new() { Outcome = WorkflowCancelOutcome.AlreadyTerminal };
}
