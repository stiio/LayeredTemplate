namespace LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

/// <summary>
/// A concrete action type in a workflow (SendEmail, HttpRequest, etc). Each implementation:
///  - declares its output ports (what outcomes it can produce)
///  - declares a typed config POCO (with <c>Expr&lt;T&gt;</c> for dynamic fields)
///  - executes a single instance and returns the single port that fired.
/// Prefer deriving from <see cref="ActionType{TConfig}"/> rather than implementing this
/// interface directly — the base class wires typed config deserialization for you.
/// Add a new action by dropping a new class — DI + registry pick it up automatically.
/// </summary>
public interface IActionType
{
    /// <summary>Stable string id, referenced by workflow nodes as <c>kind</c>.</summary>
    string Kind { get; }

    string DisplayName { get; }

    IReadOnlyList<ActionPortDescriptor> OutputPorts { get; }

    /// <summary>The typed config POCO (contains <c>Expr&lt;T&gt;</c> properties for dynamic fields).</summary>
    Type ConfigType { get; }

    /// <summary>
    /// Hint for the engine's worker pool routing. When the host configures
    /// <c>WorkflowEngineSettings.LongRunningWorkerCount &gt; 0</c>, steps of this kind are picked
    /// up only by the long-running pool — fast actions (Transform, Condition, …) keep flowing
    /// through the regular pool without being blocked by a 60-second HTTP call.
    /// <para>
    /// Default <c>false</c>. Mark <c>true</c> for actions whose synchronous body routinely takes
    /// hundreds of ms or more (HTTP requests, S3 operations, slow DB queries). Actions that
    /// suspend immediately (Approve, Delay, RunWorkflow) don't need this — they free the worker
    /// thread on first call regardless.
    /// </para>
    /// <para>
    /// The flag is read once when the step is built and stamped onto the row's
    /// <c>is_long_running</c> column; changing the override later only affects new steps. Use
    /// the same flag for every instance of an action — there's no per-call switch.
    /// </para>
    /// </summary>
    bool IsLongRunning => false;

    /// <summary>
    /// Called by the engine. <paramref name="context"/>.Config is pre-deserialized and fully
    /// resolved — every <c>Expr&lt;T&gt;.Resolved</c> is already populated.
    /// </summary>
    Task<ActionExecutionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for action types with a strongly-typed config. Concrete actions inherit this,
/// declare a POCO <typeparamref name="TConfig"/> (with <c>Expr&lt;T&gt;</c> for dynamic fields),
/// and implement the typed <c>ExecuteAsync(ActionContext&lt;TConfig&gt;)</c> overload.
/// </summary>
public abstract class ActionType<TConfig> : IActionType where TConfig : class
{
    public abstract string Kind { get; }

    public abstract string DisplayName { get; }

    public abstract IReadOnlyList<ActionPortDescriptor> OutputPorts { get; }

    public Type ConfigType => typeof(TConfig);

    /// <summary>
    /// Override and return <c>true</c> for actions whose synchronous body routinely takes
    /// hundreds of ms or more (HTTP requests, S3 operations, slow DB queries). See
    /// <see cref="IActionType.IsLongRunning"/> for engine-side semantics.
    /// </summary>
    public virtual bool IsLongRunning => false;

    public Task<ActionExecutionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var typed = new ActionContext<TConfig>
        {
            Config = (TConfig)context.Config,
            RunId = context.RunId,
            StepExecutionId = context.StepExecutionId,
            TenantId = context.TenantId,
            DefinitionId = context.DefinitionId,
            ActorUserId = context.ActorUserId,
            TriggerSourceKind = context.TriggerSourceKind,
            TriggerSourceId = context.TriggerSourceId,
            IsDryRun = context.IsDryRun,
            NodeKey = context.NodeKey,
            StepsOutputs = context.StepsOutputs,
        };
        return this.ExecuteAsync(typed, cancellationToken);
    }

    public abstract Task<ActionExecutionResult> ExecuteAsync(ActionContext<TConfig> context, CancellationToken cancellationToken);

    /// <summary>Fire a single successor step on <paramref name="port"/>. Convenience over the static factory.</summary>
    protected ActionExecutionResult Port(string port, object? outputs = null)
        => ActionExecutionResult.OnPort(port, outputs);

    /// <summary>Surface an unexpected failure for the engine's retry / dead-letter path.</summary>
    protected ActionExecutionResult Error(string error, object? outputs = null, bool transient = true)
        => ActionExecutionResult.OnError(error, outputs, transient);

    /// <summary>
    /// Suspend the step until an external <c>IWorkflowStore.TryResumeWaitingStepAsync</c> call
    /// or the timeout elapses. Pair with <see cref="ITimeoutAwareActionType"/> to decide what
    /// happens on timeout.
    /// </summary>
    protected ActionExecutionResult Suspend(int? timeoutSeconds = null, object? initialOutputs = null)
        => ActionExecutionResult.OnSuspend(timeoutSeconds, initialOutputs);

    protected ActionExecutionResult Finish(object? returnValue = null)
        => ActionExecutionResult.OnFinish(returnValue);
}
