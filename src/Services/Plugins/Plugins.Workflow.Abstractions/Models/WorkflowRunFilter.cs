namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// Filter + pagination for <see cref="Services.IWorkflowStore.ListRunsAsync"/>. Every filter is
/// an optional narrowing predicate (null = "don't constrain"); non-null filters are combined
/// with AND.
/// <para>
/// Sort is implicit: <c>CreatedAt DESC</c> always — that's the order the dedicated index supports.
/// If a future caller needs a different sort, add it as an explicit option here rather than
/// silently falling back to a sequential scan.
/// </para>
/// </summary>
public record WorkflowRunFilter
{
    /// <summary>
    /// Tenant scope. Deliberately <c>required</c> even though nullable: passing <c>null</c> is
    /// an explicit "across ALL tenants" decision reserved for admin surfaces (system-workflow
    /// runs execute under each operator's workspace tenant, so an admin view spans tenants by
    /// nature). Regular consumer surfaces must always set it — forgetting is a compile error,
    /// not a silent isolation leak. Cross-tenant listing can't use the
    /// <c>(tenant_id, created_at)</c> index; fine for admin-rare traffic.
    /// </summary>
    public required Guid? TenantId { get; init; }

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
