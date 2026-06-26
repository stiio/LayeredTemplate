using System.Text.Json;

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

    /// <summary>
    /// Called by <c>IWorkflowResumer</c> after it wins the atomic Waiting-guard, to let the action
    /// choose how its step wakes up. See <see cref="ActionType{TConfig}.OnStepResumedAsync"/> for the
    /// full contract. <see cref="ActionType{TConfig}"/> overrides it; this default-interface body is
    /// the same fail-loud non-transient error, so a direct <see cref="IActionType"/> implementer (e.g.
    /// a test fake) that never suspends doesn't have to implement it.
    /// </summary>
    Task<ActionExecutionResult> OnStepResumedAsync(
        ActionContext context, JsonElement? payload, string? port, CancellationToken cancellationToken)
        => Task.FromResult(ActionExecutionResult.OnError(
            $"Action '{this.Kind}' was resumed but defines no OnStepResumed handler.",
            transient: false));

    /// <summary>
    /// Called by the engine sweeper when a Waiting step passes its suspend deadline. See
    /// <see cref="ActionType{TConfig}.OnStepTimedOutAsync"/> for the full contract.
    /// <see cref="ActionType{TConfig}"/> overrides it; this default-interface body is the same
    /// fail-loud non-transient error (terminal Dead) so a direct <see cref="IActionType"/> implementer
    /// doesn't have to implement it.
    /// </summary>
    Task<ActionExecutionResult> OnStepTimedOutAsync(
        ActionContext context, CancellationToken cancellationToken)
        => Task.FromResult(ActionExecutionResult.OnError(
            $"Step '{this.Kind}' timed out while waiting and the action declared no timeout policy.",
            transient: false));
}

/// <summary>
/// Base class for action types with a strongly-typed config. Concrete actions inherit this,
/// declare a POCO <typeparamref name="TConfig"/> (with <c>Expr&lt;T&gt;</c> for dynamic fields),
/// and implement the typed <c>ExecuteAsync(ActionContext&lt;TConfig&gt;)</c> overload.
/// <para>
/// An action is a small state machine over its step: <see cref="ExecuteAsync(ActionContext{TConfig}, CancellationToken)"/>
/// runs the step once; suspending actions additionally own <see cref="OnStepResumedAsync"/> (the step
/// woke up via an external resume) and <see cref="OnStepTimedOutAsync"/> (the step's deadline elapsed).
/// The two lifecycle hooks are non-abstract so non-suspending actions ignore them — the default
/// raises a loud non-transient error, matching "you called <c>OnSuspend</c> ⇒ you must define how
/// you wake up; otherwise it's a bug, fail Dead" (ADR-027).
/// </para>
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

    /// <summary>
    /// Called by the engine when a step parked in <c>Waiting</c> by this action is resumed through
    /// <c>IWorkflowResumer.ResumeAsync</c> (HTTP resume API, task completion, signal fan-out, or
    /// sub-workflow parent-resume). The action owns the wake-up decision: pass-through actions
    /// echo <paramref name="port"/> verbatim (the caller's chosen outcome); fixed-port actions
    /// ignore it and return their single resume port. Return value is processed exactly like
    /// <c>ExecuteAsync</c> — the engine validates the fired port against <see cref="OutputPorts"/>,
    /// fans out, and stamps the outputs.
    /// <para>
    /// <paramref name="payload"/> is the raw resume payload the caller passed (already
    /// audit-wrapped caller-side where applicable); <paramref name="port"/> is the caller-supplied
    /// port (may be null when the caller doesn't choose one). Default: a non-transient
    /// <see cref="ActionExecutionResult.OnError"/> — an action that suspends MUST override this.
    /// </para>
    /// </summary>
    public virtual Task<ActionExecutionResult> OnStepResumedAsync(
        ActionContext context, JsonElement? payload, string? port, CancellationToken cancellationToken)
        => Task.FromResult(ActionExecutionResult.OnError(
            $"Action '{this.Kind}' was resumed but defines no OnStepResumed handler.",
            transient: false));

    /// <summary>
    /// Called by the engine sweeper when a step parked in <c>Waiting</c> by this action passes its
    /// suspend deadline before an external resume arrives. Return value is processed identically to
    /// <c>ExecuteAsync</c>: an <see cref="ActionExecutionResult.OnPort"/> fans out a successor (e.g.
    /// a <c>timedOut</c> escalation branch or Delay's happy-path <c>done</c>), an
    /// <see cref="ActionExecutionResult.OnError"/> sends the step Dead. Default: a non-transient
    /// <see cref="ActionExecutionResult.OnError"/> (terminal Dead — NOT transient, so a generic
    /// timeout can't loop timeout → retry → re-suspend). Suspending actions that want a graceful
    /// timeout override this.
    /// </summary>
    public virtual Task<ActionExecutionResult> OnStepTimedOutAsync(
        ActionContext context, CancellationToken cancellationToken)
        => Task.FromResult(ActionExecutionResult.OnError(
            $"Step '{this.Kind}' timed out while waiting and the action declared no timeout policy.",
            transient: false));

    /// <summary>Fire a single successor step on <paramref name="port"/>. Convenience over the static factory.</summary>
    protected ActionExecutionResult Port(string port, object? outputs = null)
        => ActionExecutionResult.OnPort(port, outputs);

    /// <summary>Surface an unexpected failure for the engine's retry / dead-letter path.</summary>
    protected ActionExecutionResult Error(string error, object? outputs = null, bool transient = true)
        => ActionExecutionResult.OnError(error, outputs, transient);

    /// <summary>
    /// Suspend the step until an external resume (<c>IWorkflowResumer.ResumeAsync</c>) or the
    /// timeout elapses. Override <see cref="OnStepResumedAsync"/> to choose the wake-up port and
    /// <see cref="OnStepTimedOutAsync"/> to decide what happens on timeout.
    /// </summary>
    protected ActionExecutionResult Suspend(int? timeoutSeconds = null, object? initialOutputs = null)
        => ActionExecutionResult.OnSuspend(timeoutSeconds, initialOutputs);

    protected ActionExecutionResult Finish(object? returnValue = null)
        => ActionExecutionResult.OnFinish(returnValue);
}
