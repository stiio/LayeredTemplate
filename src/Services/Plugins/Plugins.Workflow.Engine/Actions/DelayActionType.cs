using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Pauses the run for a fixed number of seconds, then fires the <c>done</c> port. Built on the
/// existing suspend / timeout-sweeper infrastructure: <c>ExecuteAsync</c> parks the step in
/// <see cref="Models.StepExecutionStatus.Waiting"/> with <c>NextAttemptAt = now + seconds</c>;
/// the engine sweeper picks it up at the deadline and calls <see cref="OnTimeoutAsync"/>, which
/// fires the success port.
/// <para>
/// Granularity is bounded by <c>WorkflowEngineSettings.PollIntervalSeconds</c> (default 3s) — a
/// 1-second delay can land anywhere between 1 and ~4 seconds depending on where the wake-up
/// falls within the sweeper cycle. Maximum delay is <c>int.MaxValue</c> seconds (~68 years).
/// </para>
/// <para>
/// Because the step is just a normal Waiting step, an operator can short-circuit it via the
/// regular resume API (<c>POST /workflow-runs/{run}/steps/{step}/resume?port=done</c>). Useful
/// for tests / manual unblocking; not a typical UX path.
/// </para>
/// </summary>
public class DelayActionType : ActionType<DelayConfig>, ITimeoutAwareActionType
{
    public const string KindName = "Delay";

    private const string PortDone = "done";

    public static readonly IReadOnlyList<ActionPortDescriptor> Ports = new[]
    {
        new ActionPortDescriptor(PortDone, "Done", ActionPortKind.Normal),
    };

    public override string Kind => KindName;

    public override string DisplayName => "Delay (wait)";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Ports;

    public override Task<ActionExecutionResult> ExecuteAsync(
        ActionContext<DelayConfig> context, CancellationToken cancellationToken)
    {
        var seconds = context.Config.Seconds;
        if (seconds <= 0)
        {
            return Task.FromResult(this.Error(
                "Delay 'seconds' must be a positive integer.",
                transient: false));
        }

        // Stamp pre-suspend metadata for trace. Authors can read it from
        // steps.<key>.requestedAt / waitSeconds before the deadline arrives.
        return Task.FromResult(this.Suspend(
            timeoutSeconds: seconds,
            initialOutputs: new
            {
                requestedAt = DateTime.UtcNow.ToString("O"),
                waitSeconds = seconds,
            }));
    }

    /// <summary>
    /// Deadline reached — fire the <c>done</c> port. For Delay this is the only path: there is
    /// no external resume signal we'd be waiting on, the timer IS the trigger.
    /// </summary>
    public Task<ActionExecutionResult> OnTimeoutAsync(
        ActionContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(this.Port(
            PortDone,
            new { firedAt = DateTime.UtcNow.ToString("O") }));
    }
}

public class DelayConfig
{
    /// <summary>
    /// Wait duration in seconds. Must be positive — non-positive values surface as a
    /// non-transient error so the run dead-letters with a clear message instead of looping.
    /// </summary>
    public int Seconds { get; set; }
}
