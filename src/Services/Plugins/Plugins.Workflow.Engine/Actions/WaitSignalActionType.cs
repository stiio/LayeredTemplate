using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Generic engine-level wait primitive: suspends the run until an external
/// <c>IWorkflowSignaler.SignalAsync(tenant, key, payload)</c> matches ANY of the configured
/// correlation keys, or (optionally) a timeout elapses. The domain-agnostic layer beneath adapters
/// like the App's <c>WaitForm</c> — the keys here are opaque strings the workflow author chooses
/// (a webhook id, a payment intent, …); the engine never interprets them.
/// <para>
/// <see cref="ExecuteAsync"/> resolves each <see cref="WaitSignalKey"/> to a non-empty correlation
/// key, dedups, and suspends with ONE bookmark per distinct key on the <c>signaled</c> port. This is
/// <b>wait-for-ANY</b> (the bookmark mechanism — the first matching signal resumes the step;
/// wait-for-ALL / barrier is explicitly out of scope per ADR-025). A config that resolves to ZERO
/// keys can't wait on anything, so it fails loud (non-transient <see cref="ActionExecutionResult.OnError"/>)
/// rather than parking forever on no bookmarks.
/// </para>
/// <para>
/// <b>Timeout</b>: <see cref="WaitSignalConfig.TimeoutSeconds"/> is optional and UNSET means wait
/// indefinitely — unlike WaitForm's finite 30-day default. This is the low-level primitive, so the
/// author opts into a deadline explicitly (mirrors <c>Approve</c>, not WaitForm). When set, the
/// engine sweeper fires <see cref="OnStepTimedOutAsync"/> → the <c>timedOut</c> port on the deadline.
/// </para>
/// </summary>
public class WaitSignalActionType : ActionType<WaitSignalConfig>
{
    public const string KindName = "WaitSignal";

    private const string PortSignaled = "signaled";
    private const string PortTimedOut = "timedOut";

    public static readonly IReadOnlyList<ActionPortDescriptor> Ports = new[]
    {
        new ActionPortDescriptor(PortSignaled, "Signaled", ActionPortKind.Normal),
        new ActionPortDescriptor(PortTimedOut, "Timed out", ActionPortKind.Error),
    };

    public override string Kind => KindName;

    public override string DisplayName => "Wait for signal";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Ports;

    public override Task<ActionExecutionResult> ExecuteAsync(
        ActionContext<WaitSignalConfig> context, CancellationToken cancellationToken)
    {
        // Resolve each key expression, drop empties/blanks, and dedup (a duplicate key would register
        // a redundant bookmark that the first signal resumes anyway). Trim so whitespace-padded
        // expressions match the signaler's exact-string lookup.
        var keys = (context.Config.Keys ?? new List<WaitSignalKey>())
            .Select(k => k.Key?.Resolved?.Trim())
            .Where(k => !string.IsNullOrEmpty(k))
            .Select(k => k!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // No keys → nothing to wait ON. Fail loud (non-transient) instead of silently suspending
        // with zero bookmarks, which would park the run forever with no way to ever resume it.
        if (keys.Count == 0)
        {
            return Task.FromResult(ActionExecutionResult.OnError(
                "WaitSignal requires at least one non-empty correlation key; none resolved.",
                transient: false));
        }

        // Guard the persisted column width: a key longer than the workflow_bookmark.correlation_key
        // varchar(256) would blow up with a DbUpdateException at suspend time → dead-letter. Fail loud
        // (non-transient) here instead, so the author sees a clear message rather than a DB stack trace.
        if (keys.Any(k => k.Length > WorkflowBookmarkRegistration.MaxCorrelationKeyLength))
        {
            return Task.FromResult(ActionExecutionResult.OnError(
                $"WaitSignal correlation key must not exceed {WorkflowBookmarkRegistration.MaxCorrelationKeyLength} characters.",
                transient: false));
        }

        // Stamp pre-suspend metadata for trace. keyCount only — the keys themselves are opaque and
        // potentially PHI-bearing, so they are NOT echoed into the run's outputs.
        var initialOutputs = new
        {
            waitingFor = "signal",
            keyCount = keys.Count,
            requestedAt = DateTime.UtcNow.ToString("O"),
        };

        // TimeoutSeconds: unset / null / non-positive resolution = wait indefinitely (the
        // author opts into a deadline).
        var timeout = context.Config.TimeoutSeconds?.Resolved is { } configured && configured > 0
            ? configured
            : (int?)null;

        // Wait-for-ANY: one bookmark per distinct key, all on the same `signaled` port. A signal on
        // any one of them resumes this exact frozen step.
        return Task.FromResult(ActionExecutionResult.OnSuspend(
            timeoutSeconds: timeout,
            initialOutputs: initialOutputs,
            bookmarks: keys.Select(k => new WorkflowBookmarkRegistration(k, PortSignaled)).ToList()));
    }

    /// <summary>
    /// A signal matched one of the registered keys — fan-out resumed this exact frozen step. Fixed
    /// port: ALWAYS <c>signaled</c> (every bookmark registered that port; the caller's
    /// <paramref name="port"/> is ignored). The signal's payload arrives as <paramref name="payload"/>
    /// and is echoed onto the step's outputs under <c>steps.&lt;key&gt;.*</c>.
    /// </summary>
    public override Task<ActionExecutionResult> OnStepResumedAsync(
        ActionContext context, JsonElement? payload, string? port, CancellationToken cancellationToken)
        => Task.FromResult(this.Port(PortSignaled, payload));

    /// <summary>
    /// No signal arrived before the configured deadline — fire the <c>timedOut</c> port so authors
    /// can wire an escalation / fallback branch instead of going Dead.
    /// </summary>
    public override Task<ActionExecutionResult> OnStepTimedOutAsync(
        ActionContext context, CancellationToken cancellationToken)
        => Task.FromResult(this.Port(
            PortTimedOut,
            new { timedOutAt = DateTime.UtcNow.ToString("O") }));
}

public class WaitSignalConfig
{
    /// <summary>
    /// Correlation keys to wait on. Each entry resolves to ONE opaque key; a signal on ANY of them
    /// resumes the step (wait-for-any). At least one must resolve non-empty or the action errors
    /// (can't wait on nothing). Same typed-list shape as <c>SwitchConfig.Branches</c> /
    /// <c>TransformConfig.Values</c>.
    /// </summary>
    public List<WaitSignalKey> Keys { get; set; } = new();

    /// <summary>
    /// Optional auto-timeout in seconds — an expression, so authors can compute the deadline
    /// from run data. <b>Unset / null / non-positive resolution = wait indefinitely</b> — this is
    /// the low-level primitive, so a deadline is an explicit opt-in (mirrors <c>Approve</c>, unlike
    /// WaitForm's finite default). When set, the sweeper fires the <c>timedOut</c> port on elapse.
    /// </summary>
    public Expr<int?>? TimeoutSeconds { get; set; }
}

public class WaitSignalKey
{
    /// <summary>Expression resolving to one opaque correlation key. Static or Liquid/JS computed.</summary>
    public Expr<string>? Key { get; set; }
}
