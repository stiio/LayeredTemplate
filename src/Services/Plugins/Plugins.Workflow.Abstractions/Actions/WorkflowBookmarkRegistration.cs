namespace LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

/// <summary>
/// A single bookmark an action registers when it suspends (via
/// <see cref="ActionExecutionResult.OnSuspend(int?, object?, System.Collections.Generic.IReadOnlyList{WorkflowBookmarkRegistration})"/>).
/// The engine persists it next to the parked step and resumes that exact step when an external
/// <c>IWorkflowSignaler.SignalAsync(tenant, <see cref="CorrelationKey"/>, payload)</c> matches.
/// </summary>
/// <param name="CorrelationKey">
/// Opaque, domain-agnostic match key. The engine treats it as a plain string and never parses it
/// — the registering action owns its shape. Signal lookup is exact-string + tenant-scoped.
/// </param>
/// <param name="ResumePort">
/// Output port to fire on the parked step when this bookmark is signalled. Must be one of the
/// action's declared <c>OutputPorts</c> — the resumer validates it before resuming.
/// </param>
public record WorkflowBookmarkRegistration(string CorrelationKey, string ResumePort)
{
    /// <summary>
    /// Max length of a persisted correlation key — tracks the <c>workflow_bookmark.correlation_key</c>
    /// column width (<c>varchar(256)</c>, see <c>WorkflowBookmarkConfiguration</c>). Actions that accept
    /// author-controlled keys (<c>WaitSignal</c> / <c>SendSignal</c>) guard against exceeding this
    /// up-front so an over-long key fails loud at execute time rather than as a DB write blow-up
    /// (<c>DbUpdateException</c> → dead-letter) at suspend / signal time.
    /// </summary>
    public const int MaxCorrelationKeyLength = 256;
}
