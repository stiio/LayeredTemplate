using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// Engine-side projection of a single step execution. Field semantics match the
/// <c>workflow_step_executions</c> table; storage plugins map both directions.
/// </summary>
public class WorkflowStepRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid RunId { get; init; }

    /// <summary>Denormalized from the parent run for tenant-scoped purge / query paths.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Stable UUID of the source node (<c>WorkflowNode.Id</c>).</summary>
    public string NodeId { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string? Name { get; init; }

    public Guid? PredecessorExecutionId { get; init; }

    public string? TriggerPort { get; init; }

    /// <summary>Already-resolved config JSON — what the action sees in <c>ActionContext.Config</c>.</summary>
    public JsonElement ResolvedConfig { get; init; }

    /// <summary>
    /// Stamped at step creation from <c>IActionType.IsLongRunning</c>. Drives worker-pool
    /// routing: when the host configures a separate long-running pool, only that pool claims
    /// rows where this is <c>true</c>; the regular fast pool filters them out so a slow HTTP
    /// call can't starve quick Transform/Condition steps.
    /// </summary>
    public bool IsLongRunning { get; init; }

    // Mutable engine state ----------------------------------------------------

    public string Status { get; set; } = StepExecutionStatus.Pending;

    /// <summary>
    /// Port the action returned in <see cref="ActionExecutionResult.OutputPort"/>. Null for
    /// Pending / Running / Dead steps and for steps still parked in Waiting (suspended-on-resume).
    /// </summary>
    public string? OutputPort { get; set; }

    public int AttemptCount { get; set; }

    public DateTime NextAttemptAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? LastError { get; set; }

    /// <summary>
    /// Step's output payload. Stored as JSON; null when the step has no outputs (Pending /
    /// Running / Dead). Object shape is <c>steps.&lt;node_key&gt;.*</c>-addressable by
    /// downstream expressions.
    /// </summary>
    public JsonElement? Outputs { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; set; }
}
