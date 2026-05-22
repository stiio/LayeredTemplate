using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// Engine-side projection of a workflow run. Storage plugins map this 1:1 to whatever
/// persistence model they own (an EF entity, a Mongo document, a row dictionary, …). The
/// engine only ever sees this POCO.
/// </summary>
/// <remarks>
/// Class (not record) so engine code can mutate <see cref="StepsOutputs"/>, <see cref="Status"/>,
/// <see cref="AbortReason"/>, <see cref="FinishedAt"/> in place — and then call
/// <c>IWorkflowStore.UpdateRun(record)</c> to push changes into the underlying store.
/// </remarks>
public class WorkflowRunRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid TenantId { get; init; }

    public Guid DefinitionId { get; init; }

    /// <summary>
    /// Captured from <c>WorkflowStartIntent.TriggerKind</c>. Stored on the run so traces and
    /// list views can show <c>SubmissionCompleted</c> vs <c>SubmissionUpdated</c> without
    /// joining back to <see cref="WorkflowDefinition"/>.
    /// </summary>
    public string TriggerKind { get; init; } = string.Empty;

    public string? TriggerSourceKind { get; init; }

    public Guid? TriggerSourceId { get; init; }

    public bool IsDryRun { get; init; }

    /// <summary>
    /// Optional human-friendly label for the run. Surfaced on list / detail dashboards so an
    /// operator can tell two runs apart at a glance (e.g. <c>"Patient Smith — intake"</c> vs
    /// <c>"Patient Doe — intake"</c>). Caller-supplied via
    /// <see cref="WorkflowStartIntent.Name"/> at dispatch time, or set mid-run by the
    /// engine-built-in <c>SetRunName</c> action. Plaintext, max 256 chars — do <b>not</b> put
    /// PHI here. For PHI use protected step outputs instead.
    /// </summary>
    public string? Name { get; set; }

    public Guid? ActorUserId { get; init; }

    public string WorkflowSnapshot { get; init; } = string.Empty;

    /// <summary>
    /// Run-time static context as JSON. Shape is <c>{ trigger: {...}, vars: {...} }</c> — see
    /// <see cref="WorkflowStartIntent.Variables"/> for the namespace contract. Stored typed so
    /// expression engines can read it without a per-evaluation deserialization.
    /// </summary>
    public JsonElement StaticContext { get; init; }

    /// <summary>JSON object keyed by <c>node.key</c>; engine appends as steps complete.</summary>
    public JsonElement StepsOutputs { get; set; }

    /// <summary>
    /// <c>running</c> | <c>suspended</c> | <c>completed</c> | <c>failed</c>. <c>suspended</c>
    /// = the only active step is parked in <c>waiting</c> (Approve / Delay / RunWorkflow with
    /// <c>waitForCompletion</c>); the dedicated status keeps it out of the stale-running purge.
    /// </summary>
    public string Status { get; set; } = WorkflowRunStatus.Running;

    public string? AbortReason { get; set; }

    /// <summary>
    /// Stamped by the store at insert time. Drives the canonical sort order for
    /// <see cref="Services.IWorkflowStore.ListRunsAsync"/>. Distinct from <see cref="StartedAt"/>:
    /// for top-level runs the two are essentially equal (insert-time = start-time), but
    /// reserved separately to match standard auditable-entity semantics.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// Optional payload set by a <c>FinishRun</c> action. JSON value the run wants to surface to
    /// its parent — when this run was started by a <c>RunWorkflow</c> action in
    /// wait-for-completion mode, the parent step receives it as
    /// <c>steps.&lt;runWorkflowKey&gt;.returnValue</c>. Null for runs that completed without
    /// a FinishRun (parent gets <c>returnValue: null</c>).
    /// </summary>
    public JsonElement? ReturnValue { get; set; }

    /// <summary>
    /// Depth of this run in the parent → child chain. Zero for runs started by external triggers
    /// (form submissions, the API). A child workflow spawned by a <c>RunWorkflow</c> action
    /// inherits parent + 1. The engine refuses to dispatch above
    /// <see cref="WorkflowEngineSettings.MaxNestingLevel"/> to keep recursive sub-workflows from
    /// fanning out without bound.
    /// </summary>
    public int NestingLevel { get; init; }

    /// <summary>
    /// When this run was started by a <c>RunWorkflow</c> action, the parent run id.
    /// Null for top-level runs.
    /// </summary>
    public Guid? ParentRunId { get; init; }

    /// <summary>
    /// When this run was started in <c>waitForCompletion</c> mode, the suspended step on the
    /// parent that should be resumed once we reach a terminal state. Null otherwise (including
    /// fire-and-forget children).
    /// </summary>
    public Guid? ParentStepId { get; init; }
}
