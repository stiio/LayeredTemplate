using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Halt the run with <c>Failed</c> status. Has zero output ports — the validator's
/// <c>edge_unknown_port</c> check catches any author who tries to wire something downstream.
/// Returns a non-transient <see cref="ActionExecutionResult.OnError"/>: the step transitions
/// straight to <c>Dead</c> (no retries), <c>CheckRunCompletionAsync</c> sees the dead step
/// and marks the run <c>Failed</c>.
/// </summary>
/// <remarks>
/// Use this as the explicit "stop and fail" terminator in branches where the author wants
/// loud escalation (e.g. validation failed, required external system unavailable, integration
/// returned an unrecoverable response). For success-style early termination, just don't wire
/// further edges — the run completes naturally when no more steps remain.
/// </remarks>
public class FailRunActionType : ActionType<FailRunConfig>
{
    public const string KindName = "FailRun";

    public override string Kind => KindName;

    public override string DisplayName => "Fail run";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Array.Empty<ActionPortDescriptor>();

    public override Task<ActionExecutionResult> ExecuteAsync(
        ActionContext<FailRunConfig> context, CancellationToken cancellationToken)
    {
        var reason = context.Config.Reason?.Resolved;
        var message = string.IsNullOrWhiteSpace(reason)
            ? "Workflow halted by FailRun."
            : reason!;

        // Non-transient → no retries; goes Dead on first attempt. With Dead steps no longer firing
        // any successor edges, the run terminates cleanly here.
        return Task.FromResult(this.Error(
            error: message,
            transient: false));
    }
}

public class FailRunConfig
{
    /// <summary>Optional human-readable explanation written to <c>step.LastError</c>.</summary>
    public Expr<string>? Reason { get; set; }
}
