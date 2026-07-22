using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Entities;

/// <summary>
/// Persistent step-execution row. Mapped to/from
/// <c>Hipaa.Backend.Plugins.Workflow.Abstractions.WorkflowStepRecord</c> by the store.
/// </summary>
internal class WorkflowStepExecution
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RunId { get; set; }

    /// <summary>
    /// Denormalized from <see cref="WorkflowRun.TenantId"/>. Lets purge / scoped-query operations
    /// hit a single index instead of joining through workflow_runs.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>Node id inside WorkflowRun.WorkflowSnapshot.</summary>
    public string NodeId { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public string? Name { get; set; }

    public Guid? PredecessorExecutionId { get; set; }

    /// <summary>Which output port of the predecessor fired the edge that created this step.</summary>
    public string? TriggerPort { get; set; }

    /// <summary>Config after expression resolution — ready to dispatch.</summary>
    public JsonElement ResolvedConfig { get; set; }

    /// <summary>
    /// Stamped at insert-time from <c>IActionType.IsLongRunning</c>. Drives the lane filter in
    /// <c>ClaimPendingStepIdsAsync</c> — see <c>WorkflowStepLane</c>. Defaults to <c>false</c>
    /// so legacy rows behave as fast steps.
    /// </summary>
    public bool IsLongRunning { get; set; }

    /// <summary>Data produced by this step (available to subsequent steps as steps.{nodeKey}.*).</summary>
    public JsonElement? Outputs { get; set; }

    /// <summary>
    /// Port the action returned in <c>ActionExecutionResult.OutputPort</c>. Null for Pending /
    /// Running / Dead steps and for steps still parked in Waiting.
    /// </summary>
    public string? OutputPort { get; set; }

    /// <summary>pending | running | completed | failed | dead | waiting.</summary>
    public string Status { get; set; } = "pending";

    public int AttemptCount { get; set; }

    public DateTime NextAttemptAt { get; set; }

    /// <summary>
    /// Start of the LAST attempt — stamped by the claim SQL (pending → running), overwritten on
    /// retry claims, nulled by the release path. Null = never claimed. The timeout sweep does
    /// NOT re-stamp it (a Waiting step's wait belongs to its duration).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    public string? LastError { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public WorkflowRun Run { get; set; } = null!;
}
