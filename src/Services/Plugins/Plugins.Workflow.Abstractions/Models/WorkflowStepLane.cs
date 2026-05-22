namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// Worker-pool routing hint for <see cref="Services.IWorkflowStore.ClaimPendingStepsAsync"/> /
/// <see cref="Services.IWorkflowStore.ClaimExpiredWaitingStepsAsync"/>. Lets the host run two
/// independent pools — a fast lane for sub-second actions (Transform / Condition / Switch) and
/// a long-running lane for actions that may block their worker thread for tens of seconds
/// (HttpRequest with 30-60s timeouts, slow S3 transfers, etc.). Without separation a handful of
/// long-running steps can starve the entire batch.
/// <para>
/// The store filters by <c>is_long_running</c> column (stamped at step creation from
/// <c>IActionType.IsLongRunning</c>):
/// <list type="bullet">
///   <item><see cref="Any"/> — no filter; default mode when only one pool is configured.</item>
///   <item><see cref="FastOnly"/> — claims only rows with <c>is_long_running = false</c>.</item>
///   <item><see cref="LongOnly"/> — claims only rows with <c>is_long_running = true</c>.</item>
/// </list>
/// </para>
/// </summary>
public enum WorkflowStepLane
{
    /// <summary>No filter on <c>is_long_running</c> — single-pool mode.</summary>
    Any = 0,

    /// <summary>Picks only fast steps (<c>is_long_running = false</c>).</summary>
    FastOnly = 1,

    /// <summary>Picks only long-running steps (<c>is_long_running = true</c>).</summary>
    LongOnly = 2,
}
