namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// State machine values for <see cref="WorkflowRunRecord.Status"/>.
/// <list type="bullet">
///   <item><description><c>Running</c> — at least one step is <c>Pending</c> or <c>Running</c>.</description></item>
///   <item><description><c>Suspended</c> — at least one step is <c>Waiting</c> (Approve / Delay / RunWorkflow wait-for-completion / etc.) and no other step is actively progressing. The run is parked on an external signal.</description></item>
///   <item><description><c>Completed</c> — terminal success. All steps reached a non-active status and none ended in <c>Dead</c>.</description></item>
///   <item><description><c>Failed</c> — terminal failure. At least one step is <c>Dead</c>, or a <c>FailRun</c> action explicitly aborted the run.</description></item>
/// </list>
/// Run-side transitions are driven by <c>WorkflowFanOut.CheckRunCompletionAsync</c> off the
/// authoritative <c>step_executions</c> state. Operators see <c>Suspended</c> on dashboards
/// without drilling into individual steps to know which runs are awaiting human input.
/// </summary>
public static class WorkflowRunStatus
{
    public const string Running = "running";
    public const string Suspended = "suspended";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
