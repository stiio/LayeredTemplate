namespace LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

/// <summary>
/// Return value of an <see cref="IActionType"/>.<c>ExecuteAsync</c> call. The engine fires a
/// single successor step for the named <see cref="OutputPort"/>. Multi-port fan-out is
/// intentionally out of scope — keeps every run a linear pipeline of steps and avoids
/// concurrent in-run state mutation.
/// </summary>
public class ActionExecutionResult
{
    private ActionExecutionResult() { }

    /// <summary>
    /// Port the action wants the engine to fire. Null means "no successor step" — used by
    /// <see cref="OnError"/> (Dead-letter on retry exhaustion) and <see cref="OnSuspend"/>
    /// (the run pauses here until external resume).
    /// </summary>
    public string? OutputPort { get; init; }

    public object? Outputs { get; init; }

    /// <summary>
    /// Diagnostic message recorded on the step's <c>LastError</c>. Set only via <see cref="OnError"/>.
    /// Action authors reserve <c>OnError</c> for genuinely unexpected failures — expected branching
    /// outcomes (HTTP 4xx, send failed, etc.) should use <see cref="OnPort"/> with an Error-kind
    /// port so the run continues normally.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Whether the failure described by <see cref="Error"/> is worth retrying. Default <c>true</c>:
    /// the engine retries up to <c>MaxAttempts</c> before transitioning the step to <c>Dead</c>.
    /// Set to <c>false</c> for deterministic failures (e.g. <c>FailRun</c>) so the step transitions
    /// to <c>Dead</c> immediately, saving wasted retries.
    /// </summary>
    public bool IsTransient { get; init; } = true;

    /// <summary>
    /// When true, the engine parks the step in <see cref="Models.StepExecutionStatus.Waiting"/>
    /// instead of completing it — used by actions that hand control to an external trigger
    /// (Approval, manual webhook, scheduled hold, …). The step is resumed via
    /// <c>IWorkflowResumer.ResumeAsync</c> (which calls the action's
    /// <see cref="ActionType{TConfig}.OnStepResumedAsync"/>); if <see cref="SuspendTimeoutSeconds"/>
    /// is set and elapses first, the engine sweeper consults
    /// <see cref="ActionType{TConfig}.OnStepTimedOutAsync"/> to decide the outcome (default = Dead).
    /// </summary>
    public bool IsSuspended { get; init; }

    /// <summary>
    /// Optional wall-clock deadline (seconds from now) for a suspended step. Null = wait
    /// forever. Stored on the step as <c>NextAttemptAt</c>.
    /// </summary>
    public int? SuspendTimeoutSeconds { get; init; }

    /// <summary>
    /// When true, the engine treats this step as a successful terminator: the run flips to
    /// <see cref="Models.WorkflowRunStatus.Completed"/> immediately and no successor edges
    /// fire (the action declares no output ports). The contract counterpart of FailRun, which
    /// goes to <c>Failed</c>; FinishRun goes to <c>Completed</c> with an explicit return payload.
    /// </summary>
    public bool TerminatesRun { get; init; }

    /// <summary>
    /// Payload the action wants to surface to the run's parent (when this run was started by a
    /// <c>RunWorkflow</c> action in wait-for-completion mode). Stored on the run as
    /// <c>ReturnValue</c>; the parent step receives it as <c>steps.&lt;runWorkflowKey&gt;.returnValue</c>.
    /// Ignored unless <see cref="TerminatesRun"/> is true.
    /// </summary>
    public object? ReturnValue { get; init; }

    /// <summary>
    /// Bookmarks the engine should persist alongside the parked Waiting step. Each registration
    /// declares an opaque correlation key plus the resume port to fire when an external
    /// <c>IWorkflowSignaler.SignalAsync(tenant, key, payload)</c> matches it. Only honoured when
    /// <see cref="IsSuspended"/> is true; ignored on every other result flavour.
    /// <para>
    /// The engine never interprets the key — it's a domain-agnostic string the registering action
    /// owns (e.g. an App-side action may key it on a submission id, but the engine doesn't know
    /// that). Plural because a single suspend may wait on several keys (wait-for-any-of-N); a
    /// signal on any one of them resumes the step. Fan-IN / wait-for-ALL is out of scope.
    /// </para>
    /// </summary>
    public IReadOnlyList<WorkflowBookmarkRegistration>? Bookmarks { get; init; }

    /// <summary>Single-port fire — the only fan-out primitive.</summary>
    public static ActionExecutionResult OnPort(string port, object? outputs = null) =>
        new() { OutputPort = port, Outputs = outputs };

    /// <summary>
    /// Action failed unexpectedly. Step records <paramref name="error"/> as <c>LastError</c>; engine
    /// retries up to <c>MaxAttempts</c> (when <paramref name="transient"/>) and ultimately transitions
    /// the step to <c>Dead</c>. Dead steps don't fire any successor edges — branches that should
    /// run after a failure must be wired explicitly via Error-kind ports the action returns.
    /// </summary>
    public static ActionExecutionResult OnError(
        string error,
        object? outputs = null,
        bool transient = true) =>
        new()
        {
            OutputPort = null,
            Error = error,
            Outputs = outputs,
            IsTransient = transient,
        };

    /// <summary>
    /// Park the step until an external resume call (or timeout). <paramref name="timeoutSeconds"/>
    /// = null means "wait forever" — the run stays in <c>running</c> until something resumes it.
    /// <paramref name="initialOutputs"/> are stamped on the step right away so downstream Conditions
    /// (or the resume handler) can read pre-suspend metadata via <c>steps.&lt;key&gt;.*</c>.
    /// <paramref name="bookmarks"/> register one or more opaque correlation keys an external
    /// <c>IWorkflowSignaler.SignalAsync</c> can use to resume this exact step; persisted atomically
    /// with the suspend by the worker (see <see cref="Bookmarks"/>).
    /// </summary>
    public static ActionExecutionResult OnSuspend(
        int? timeoutSeconds = null,
        object? initialOutputs = null,
        IReadOnlyList<WorkflowBookmarkRegistration>? bookmarks = null) =>
        new()
        {
            OutputPort = null,
            Outputs = initialOutputs,
            IsSuspended = true,
            SuspendTimeoutSeconds = timeoutSeconds,
            Bookmarks = bookmarks,
        };

    /// <summary>
    /// Successful early termination of the run with an explicit return payload. The step is
    /// marked Completed (with <paramref name="returnValue"/> stamped on its outputs for trace),
    /// the run flips to Completed, no successor edges fire. If this run has a parent waiting
    /// (started via RunWorkflow in wait mode), the parent step is auto-resumed on
    /// <c>success</c> with <paramref name="returnValue"/> as <c>steps.&lt;key&gt;.returnValue</c>.
    /// </summary>
    public static ActionExecutionResult OnFinish(object? returnValue = null) =>
        new()
        {
            OutputPort = null,
            Outputs = returnValue,
            TerminatesRun = true,
            ReturnValue = returnValue,
        };
}
