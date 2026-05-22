namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// Cheap summary of step states for one run — what the run-completion check needs to decide
/// run.Status transitions:
/// <list type="bullet">
///   <item><see cref="HasPendingOrRunning"/> — at least one step is actively progressing.
///   Drives the run to <c>Running</c>.</item>
///   <item><see cref="HasWaiting"/> — at least one step is parked on an external signal
///   (Approve, Delay, RunWorkflow wait-for-completion). When there's no
///   <see cref="HasPendingOrRunning"/>, drives the run to <c>Suspended</c>.</item>
///   <item><see cref="HasDead"/> — at least one step ended in <c>Dead</c>; when no other steps
///   remain active, drives the run to <c>Failed</c> (otherwise <c>Completed</c>).</item>
/// </list>
/// </summary>
public readonly record struct WorkflowRunStepStateSummary(
    bool HasPendingOrRunning,
    bool HasWaiting,
    bool HasDead)
{
    /// <summary>True if any step is still active (Pending, Running, or Waiting).</summary>
    public bool HasOngoing => this.HasPendingOrRunning || this.HasWaiting;
}
