using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Default <see cref="IWorkflowWorkSignal"/>: one latching auto-reset event ("generation") per
/// lane. Registered as a singleton — worker loops wait on it, storage listeners pulse it.
/// <para>
/// Latching closes the classic lost-wakeup race: a pulse landing between a worker's empty
/// claim and its wait call completes the CURRENT generation's task, so the wait observes it
/// and returns instantly instead of sleeping a full poll interval. Wake-all semantics: every
/// waiter of a generation wakes (extra claims are cheap — SKIP LOCKED just returns empty).
/// </para>
/// <para>
/// Why coalesced pulses are safe: a pulse is only ever sent AFTER its work is committed, and
/// re-arm happens INSIDE the wait before it returns — so a pulse that no-ops onto an
/// already-completed generation always has its work visible to the claim that follows the
/// woken wait. A waiter re-arms only the lanes it waited on: a FastOnly waiter must not steal
/// a latched long pulse from the long pool.
/// </para>
/// <para>
/// With nobody pulsing (no push-capable storage registered) waits always run to their timeout
/// — behaviour degrades to plain interval polling by construction.
/// </para>
/// </summary>
internal sealed class WorkflowWorkSignal : IWorkflowWorkSignal
{
    private TaskCompletionSource fastGeneration = NewGeneration();
    private TaskCompletionSource longGeneration = NewGeneration();

    public void Pulse(WorkflowStepLane lane)
    {
        if (lane is WorkflowStepLane.FastOnly or WorkflowStepLane.Any)
        {
            this.fastGeneration.TrySetResult();
        }

        if (lane is WorkflowStepLane.LongOnly or WorkflowStepLane.Any)
        {
            this.longGeneration.TrySetResult();
        }
    }

    public async Task WaitForWorkAsync(WorkflowStepLane lane, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        // Snapshot generations up front: any pulse from this point on completes these exact
        // instances, so nothing sent after the preceding (empty) claim can be missed.
        var fast = this.fastGeneration;
        var @long = this.longGeneration;

        var signalTask = lane switch
        {
            WorkflowStepLane.FastOnly => fast.Task,
            WorkflowStepLane.LongOnly => @long.Task,
            WorkflowStepLane.Any => Task.WhenAny(fast.Task, @long.Task),
            _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unknown WorkflowStepLane value."),
        };

        // Linked CTS releases the Task.Delay timer early when the signal wins; a cancelled
        // Task.Delay ends Canceled (not Faulted), so abandoning it raises nothing.
        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(maxWait, delayCts.Token);
        await Task.WhenAny(signalTask, delayTask);
        delayCts.Cancel();

        cancellationToken.ThrowIfCancellationRequested();

        // Re-arm consumed lanes BEFORE returning (see class remarks for why the ordering
        // matters) — but only lanes this waiter actually waited on. CAS: with several
        // concurrent waiters of one generation only the first swap wins; the rest observe the
        // already re-armed field and leave it alone.
        if ((lane is WorkflowStepLane.FastOnly or WorkflowStepLane.Any) && fast.Task.IsCompleted)
        {
            Interlocked.CompareExchange(ref this.fastGeneration, NewGeneration(), fast);
        }

        if ((lane is WorkflowStepLane.LongOnly or WorkflowStepLane.Any) && @long.Task.IsCompleted)
        {
            Interlocked.CompareExchange(ref this.longGeneration, NewGeneration(), @long);
        }
    }

    // RunContinuationsAsynchronously: Pulse is called from I/O callbacks (e.g. the Npgsql
    // notification handler) — inline continuations would run worker-loop code on that thread.
    private static TaskCompletionSource NewGeneration() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
