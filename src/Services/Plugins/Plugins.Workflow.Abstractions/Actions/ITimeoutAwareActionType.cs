namespace LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

/// <summary>
/// Optional second face of an <see cref="IActionType"/> for actions that can be suspended
/// (return <c>ActionExecutionResult.Suspend(...)</c>) and need to make a decision when their
/// <see cref="ActionExecutionResult.SuspendTimeoutSeconds"/> elapses before an external
/// resume arrives.
/// <para>
/// Actions that don't implement this interface get the default treatment from the engine:
/// timed-out waiting steps go to <c>Dead</c> with a generic message. Implementing this lets
/// the action choose a graceful outcome instead — typically firing a <c>timedOut</c> port
/// so authors can wire follow-up handling.
/// </para>
/// </summary>
public interface ITimeoutAwareActionType
{
    /// <summary>
    /// Called by the engine sweeper when the step's <c>NextAttemptAt</c> elapsed while the
    /// step was still <c>Waiting</c>. Return value is processed identically to
    /// <c>ExecuteAsync</c>: a normal <see cref="ActionExecutionResult.OnPort"/> fans out
    /// successor steps, <see cref="ActionExecutionResult.OnError"/> sends the step to Dead,
    /// and (yes) re-suspending is allowed if the action wants to extend the deadline.
    /// </summary>
    Task<ActionExecutionResult> OnTimeoutAsync(
        ActionContext context, CancellationToken cancellationToken);
}
