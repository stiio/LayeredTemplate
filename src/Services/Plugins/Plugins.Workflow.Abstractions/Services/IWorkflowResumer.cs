using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Single entry point for finalising a step that's parked in <c>Waiting</c> on an external
/// signal (Approve action, manual webhook, …). Inputs identify what to resume, the chosen
/// outcome port, and an optional payload to stamp on the step's outputs. The implementation
/// is responsible for: tenant-match check, status guard, port validation, atomic Waiting →
/// Completed transition, fan-out via <see cref="IWorkflowFanOut"/>, and run-completion check —
/// all committed as ONE storage transaction, so a resume either lands completely or leaves the
/// step <c>Waiting</c>.
/// </summary>
public interface IWorkflowResumer
{
    /// <summary>
    /// Resumes a waiting step as a self-contained atomic unit of work: the Waiting-guard, the
    /// action's wake-up hook, the successor fan-out, and the run-completion check commit in a
    /// single storage transaction. A crash — or a post-guard failure such as the action's
    /// resume hook throwing — rolls the whole resume back: the step stays <c>Waiting</c> and
    /// the call is retryable, never wedged half-resumed.
    /// <para>
    /// When called inside an ambient store transaction (chain unwind: a child run's terminal
    /// transition auto-resumes its parent, whose run may terminate and resume ITS parent, …),
    /// the resume joins that transaction and the outermost owner commits the chain at once.
    /// The commit also flushes whatever the caller already staged on the plugin's scoped
    /// store — deliberate: the worker path relies on the child's terminal state and the
    /// parent's resume landing in the same commit.
    /// </para>
    /// </summary>
    Task<WorkflowResumeResult> ResumeAsync(
        WorkflowResumeCommand command,
        CancellationToken cancellationToken);
}

/// <summary>
/// Inputs to <see cref="IWorkflowResumer.ResumeAsync"/>. <see cref="TenantId"/> is the trusted
/// tenant the caller has already authorised — the resumer enforces it matches the run's
/// recorded tenant (mismatch surfaces as <see cref="WorkflowResumeFailureReason.RunNotFound"/>
/// so the contract doesn't leak run existence across tenants).
/// </summary>
public record WorkflowResumeCommand
{
    public required Guid RunId { get; init; }

    public required Guid StepId { get; init; }

    /// <summary>Tenant the caller is acting on behalf of — must match the run's stored tenant.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// Caller-supplied outcome port. The resumer hands it to the action's
    /// <c>OnStepResumedAsync(ctx, payload, port)</c>: pass-through actions (Approve, RunWorkflow-wait)
    /// echo it; fixed-port actions (WaitForm, task-actions) ignore it (returning their own port).
    /// <b>Currently required in practice</b> — the resumer rejects null/blank with
    /// <see cref="WorkflowResumeFailureReason.InvalidPort"/> BEFORE consulting the action, and every
    /// caller supplies a concrete port today. Typed nullable only to leave room for a future
    /// "let the action choose its default port" mode (ADR-027 Slice B follow-up); until that lands,
    /// pass a concrete port. The fired port is whatever the action returns, validated against its
    /// <c>OutputPorts</c>.
    /// </summary>
    public string? Port { get; init; }

    /// <summary>
    /// Free-form JSON payload handed to the action's <c>OnStepResumedAsync</c> and (for the
    /// echo-style actions) stamped on the step's outputs. Becomes available downstream via
    /// <c>steps.&lt;node_key&gt;.*</c>:
    /// <list type="bullet">
    ///   <item>Object → keys are flattened into the outputs as-is.</item>
    ///   <item>Array / scalar / null → kept under a single <c>value</c> key
    ///   (<c>steps.&lt;node_key&gt;.value</c>) so non-object payloads aren't silently lost.</item>
    /// </list>
    /// Resumer doesn't add audit fields itself — composing <c>resumedBy</c> / <c>resumedAt</c> /
    /// <c>payload</c> shape is the consumer's job (resumer is intentionally tenant- and
    /// identity-agnostic).
    /// </summary>
    public JsonElement? Payload { get; init; }
}

public enum WorkflowResumeFailureReason
{
    None,

    /// <summary>Run id doesn't exist or belongs to a different tenant.</summary>
    RunNotFound,

    /// <summary>Step id doesn't exist or doesn't belong to the supplied run.</summary>
    StepNotFound,

    /// <summary>Step is not in <c>Waiting</c> status — already resumed, timed out, or never suspended.</summary>
    StepNotWaiting,

    /// <summary>Port id is not one the step's action declared in its <c>OutputPorts</c>.</summary>
    InvalidPort,

    /// <summary>
    /// Pre-checks passed but the atomic guard saw the row in a non-<c>Waiting</c> state. Means
    /// another caller (resume / sweeper) won the race — caller treats this as 409.
    /// </summary>
    ConcurrencyConflict,

    /// <summary>
    /// A transient config field failed to resolve while preparing the resume. The step stays
    /// <c>Waiting</c> and nothing was staged — retryable. By the time a step waits, its
    /// transient fields have already resolved once (at execute), so this is almost always an
    /// environmental failure (secret store down, lookup target gone); the wait timeout remains
    /// the dead-letter backstop for the persistent case.
    /// </summary>
    ConfigResolutionFailed,
}

/// <summary>
/// Structured result of a resume attempt. The resumer never throws on a business-rule failure;
/// callers map <see cref="Reason"/> to whatever framework-level error they prefer.
/// </summary>
public class WorkflowResumeResult
{
    public bool Succeeded => this.Reason == WorkflowResumeFailureReason.None;

    public WorkflowResumeFailureReason Reason { get; init; } = WorkflowResumeFailureReason.None;

    public string? Message { get; init; }

    public static WorkflowResumeResult Success() => new();

    public static WorkflowResumeResult Failure(WorkflowResumeFailureReason reason, string message)
        => new() { Reason = reason, Message = message };
}
