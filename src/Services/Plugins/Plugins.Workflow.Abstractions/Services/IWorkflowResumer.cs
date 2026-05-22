using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Single entry point for finalising a step that's parked in <c>Waiting</c> on an external
/// signal (Approve action, manual webhook, …). Inputs identify what to resume, the chosen
/// outcome port, and an optional payload to stamp on the step's outputs. The implementation
/// is responsible for: tenant-match check, status guard, port validation, atomic Waiting →
/// Completed transition, fan-out via <see cref="IWorkflowFanOut"/>, and run-completion check.
/// All persistence is staged — the caller is responsible for the final
/// <c>IWorkflowStore.SaveChangesAsync</c> so the resume travels with any other domain changes
/// in the same unit of work.
/// </summary>
public interface IWorkflowResumer
{
    /// <summary>
    /// Resumes a waiting step. <paramref name="flush"/> controls the final
    /// <c>store.SaveChangesAsync</c>: external callers (HTTP handlers, integration tests) keep
    /// the default <c>true</c> so resume is a self-contained unit of work; engine-internal
    /// callers (e.g. fan-out auto-resuming a parent step on child run completion) pass
    /// <c>false</c> so the surrounding worker batch's flush handles persistence.
    /// </summary>
    Task<WorkflowResumeResult> ResumeAsync(
        WorkflowResumeCommand command,
        CancellationToken cancellationToken,
        bool flush = true);
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

    /// <summary>Output port to fire — must be one of the action's declared <c>OutputPorts</c>.</summary>
    public required string Port { get; init; }

    /// <summary>
    /// Free-form JSON payload merged into the step's outputs. Becomes available downstream via
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
    public JsonElement? Outputs { get; init; }
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
