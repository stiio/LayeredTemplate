namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Entities;

/// <summary>
/// Persistent home for a workflow graph. Distinct from
/// <c>Hipaa.Backend.Plugins.Workflow.Abstractions.WorkflowDefinition</c> (the engine-side
/// record): this is the EF-mapped row, that is the runtime POCO. Store maps both directions.
/// </summary>
public class WorkflowDefinition
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    /// <summary>"Form" | "Contact" | "Standalone".</summary>
    public string OwnerKind { get; set; } = null!;

    /// <summary>Owner entity id; null for <c>OwnerKind="Standalone"</c>.</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>Trigger this definition listens to — see <c>WorkflowTriggerKinds</c>.</summary>
    public string TriggerKind { get; set; } = null!;

    /// <summary>Optional human-readable label, displayed in admin pickers (RunWorkflow target etc.).</summary>
    public string? DisplayName { get; set; }

    /// <summary>Serialized <c>WorkflowGraph</c> (jsonb).</summary>
    public string Graph { get; set; } = null!;

    /// <summary>Set by the store on insert; not auto-managed by EF or any host interceptor.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Set by the store on insert/update; not auto-managed.</summary>
    public DateTime UpdatedAt { get; set; }
}
