namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// Engine-side projection of a single persisted bookmark row. Field semantics match the
/// <c>workflow_bookmark</c> table; storage plugins map both directions. A bookmark ties an opaque
/// <see cref="CorrelationKey"/> to the exact frozen <c>(RunId, StepId, ResumePort)</c> that an
/// <c>IWorkflowSignaler.SignalAsync</c> match should resume.
/// </summary>
public class WorkflowBookmarkRecord
{
    public Guid Id { get; init; }

    /// <summary>Tenant scope — every signal lookup AND resume re-checks this. Mandatory isolation.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Run that owns the parked step. FK → workflow_runs (ON DELETE CASCADE).</summary>
    public Guid RunId { get; init; }

    /// <summary>The exact Waiting step this bookmark resumes — frozen at suspend time.</summary>
    public Guid StepId { get; init; }

    /// <summary>Opaque domain key the signal matches against. Engine never parses it.</summary>
    public string CorrelationKey { get; init; } = string.Empty;

    /// <summary>Port to fire on the frozen step when this bookmark is signalled.</summary>
    public string ResumePort { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}
