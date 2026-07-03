using LayeredTemplate.Plugins.Workflow.Abstractions.Models;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Wake-up channel between "something just made steps claimable" and the worker loops' idle
/// wait. Purely a latency optimisation: a pulse means "claim now instead of waiting out the
/// poll interval" — it carries no work items and gives no delivery guarantees. A lost pulse
/// costs at most one poll interval of latency (the fallback poll finds the work); a spurious
/// pulse costs one empty claim query. Correctness never depends on it — the claim query over
/// the database remains the single source of truth.
/// <para>
/// The engine registers a default in-process implementation and waits on it whenever a claim
/// comes back empty. A storage plugin with a push primitive (e.g. the EF Core plugin's
/// Postgres LISTEN/NOTIFY listener) calls <see cref="Pulse"/> when new work is committed;
/// without any pulser the wait always runs to its timeout and behaviour is plain interval
/// polling.
/// </para>
/// </summary>
public interface IWorkflowWorkSignal
{
    /// <summary>
    /// Wakes waiters whose lane overlaps <paramref name="lane"/>:
    /// <see cref="WorkflowStepLane.FastOnly"/> wakes FastOnly + Any waiters,
    /// <see cref="WorkflowStepLane.LongOnly"/> wakes LongOnly + Any,
    /// <see cref="WorkflowStepLane.Any"/> wakes everyone. A pulse with nobody waiting is
    /// latched: the next <see cref="WaitForWorkAsync"/> on an overlapping lane returns
    /// immediately — this closes the lost-wakeup race between a worker's empty claim and its
    /// wait call. Must be cheap and non-blocking; callers pulse from I/O callbacks.
    /// </summary>
    void Pulse(WorkflowStepLane lane);

    /// <summary>
    /// Waits until work may be available for <paramref name="lane"/> or until
    /// <paramref name="maxWait"/> elapses, whichever comes first. Returning says nothing about
    /// actual work being present — the caller claims to find out. Throws
    /// <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> fires.
    /// </summary>
    Task WaitForWorkAsync(WorkflowStepLane lane, TimeSpan maxWait, CancellationToken cancellationToken);
}
