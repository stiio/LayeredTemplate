namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// Filter + pagination for <see cref="Services.IWorkflowStore.ListRunsAsync"/>. Tenant scoping
/// is mandatory; everything else is an optional narrowing predicate (null = "don't constrain").
/// All non-null filters are combined with AND.
/// <para>
/// Sort is implicit: <c>CreatedAt DESC</c> always — that's the order the dedicated index supports.
/// If a future caller needs a different sort, add it as an explicit option here rather than
/// silently falling back to a sequential scan.
/// </para>
/// </summary>
public record WorkflowRunFilter
{
    /// <summary>Mandatory — every list-runs query is tenant-scoped for isolation.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Filter by the workflow definition the run was started from.</summary>
    public Guid? DefinitionId { get; init; }

    /// <summary>Filter by trigger kind (e.g. <c>"SubmissionCompleted"</c>, <c>"SubWorkflow"</c>).</summary>
    public string? TriggerKind { get; init; }

    /// <summary>Filter by trigger source kind (e.g. <c>"Submission"</c>, <c>"WorkflowRun"</c>).</summary>
    public string? TriggerSourceKind { get; init; }

    /// <summary>Filter by trigger source id within <see cref="TriggerSourceKind"/>.</summary>
    public Guid? TriggerSourceId { get; init; }

    /// <summary>Filter by dry-run flag — <c>true</c> = dry-runs only, <c>false</c> = real runs only,
    /// null = both. Used by the delete-workflow guard to detect real (PHI-carrying) run history.</summary>
    public bool? IsDryRun { get; init; }

    public required WorkflowPagination Pagination { get; init; }
}
