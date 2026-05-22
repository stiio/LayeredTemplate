using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Entities;

/// <summary>
/// Persistent workflow run row. <see cref="Definition"/> nav lets EF model the FK; engine code
/// maps this to <c>Hipaa.Backend.Plugins.Workflow.Abstractions.WorkflowRunRecord</c>.
/// </summary>
public class WorkflowRun : IHaveProtectedData
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Multi-tenant key. Engine treats it as opaque; consumer maps to its own tenant concept.</summary>
    public Guid TenantId { get; set; }

    /// <summary>FK → <see cref="WorkflowDefinition"/>.</summary>
    public Guid DefinitionId { get; set; }

    /// <summary>
    /// Snapshot of <c>WorkflowStartIntent.TriggerKind</c> (<c>SubmissionCompleted</c>,
    /// <c>SubmissionUpdated</c>, …). Lets traces / list views surface the trigger without
    /// joining back to the definition.
    /// </summary>
    public string TriggerKind { get; set; } = string.Empty;

    public string? TriggerSourceKind { get; set; }

    public Guid? TriggerSourceId { get; set; }

    public bool IsDryRun { get; set; }

    /// <summary>
    /// Optional plaintext label for the run; max 256 chars. Surfaced on list / detail
    /// dashboards. Set at dispatch time (<c>WorkflowStartIntent.Name</c>) or mid-run by the
    /// engine's built-in <c>SetRunName</c> action. <b>Not</b> a PHI column — see record-level
    /// remarks.
    /// </summary>
    public string? Name { get; set; }

    public Guid? ActorUserId { get; set; }

    public string WorkflowSnapshot { get; set; } = null!;

    /// <summary>
    /// Stored as bytea via <see cref="WorkflowProtectedJsonConverter"/> — encryption pivots on
    /// whether <see cref="IWorkflowDataProtector"/> is registered.
    /// </summary>
    public JsonElement StaticContext { get; set; }

    /// <summary>JSON object keyed by <c>node.key</c>; appended to as steps complete.</summary>
    public JsonElement StepsOutputs { get; set; }

    public string Status { get; set; } = "running";

    public string? AbortReason { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// JSON payload set by a successful FinishRun terminator. Read by sub-workflow auto-resume
    /// to surface to the parent step's outputs as <c>return_value</c>.
    /// </summary>
    public JsonElement? ReturnValue { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Depth in the parent → child run chain. Top-level runs have <c>0</c>.</summary>
    public int NestingLevel { get; set; }

    /// <summary>FK → parent <see cref="WorkflowRun"/> when started by a <c>RunWorkflow</c> action.</summary>
    public Guid? ParentRunId { get; set; }

    /// <summary>
    /// FK → suspended <see cref="WorkflowStepExecution"/> on the parent run that should be
    /// resumed once this run reaches a terminal state. Null for fire-and-forget children
    /// and for top-level runs.
    /// </summary>
    public Guid? ParentStepId { get; set; }

    /// <summary>
    /// Active key version at the time this row's protected columns were last written. Stamped
    /// by <c>WorkflowProtectionStampInterceptor</c>; null when no protector is registered or
    /// the row pre-dates encryption. Operators query this to find rows still on a rotated-out
    /// key.
    /// </summary>
    public string? ProtectionVersion { get; set; }

    // Reverse navigation kept because the worker batch loads steps via Run; it's used in
    // EfCoreWorkflowStore for change-tracking the run's children. The Definition navigation was
    // dropped — store callers go through GetDefinitionByIdAsync explicitly when they need it,
    // and removing it lets us suppress the FK-shadow index that no application query uses.
    public ICollection<WorkflowStepExecution> StepExecutions { get; set; } = new List<WorkflowStepExecution>();
}
