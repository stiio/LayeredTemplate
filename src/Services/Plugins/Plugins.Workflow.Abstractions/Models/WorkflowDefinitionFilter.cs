namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// Filter + pagination for <see cref="Services.IWorkflowStore.ListDefinitionsAsync"/>. Tenant
/// scoping is mandatory; everything else is an optional narrowing predicate. Useful for admin
/// UIs that want "show me all definitions for this owner" or "every definition that triggers
/// on SubmissionCompleted".
/// <para>
/// Sort is implicit: <c>CreatedAt DESC</c>. Pagination is required — definitions tables stay
/// small in practice, but the contract is uniform with <see cref="WorkflowRunFilter"/> so
/// callers don't deal with two different shapes.
/// </para>
/// </summary>
public record WorkflowDefinitionFilter
{
    /// <summary>Mandatory — every list-definitions query is tenant-scoped.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Owner kind (e.g. <c>"Form"</c>, <c>"Standalone"</c>).</summary>
    public string? OwnerKind { get; init; }

    /// <summary>Owner entity id within <see cref="OwnerKind"/>; null for sourceless owners.</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Trigger kind to filter on (e.g. <c>"SubmissionCompleted"</c>).</summary>
    public string? TriggerKind { get; init; }

    public required WorkflowPagination Pagination { get; init; }
}
