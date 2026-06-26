using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Models;

/// <summary>
/// A workflow graph plus the metadata the engine needs to attach a run to it. Domain-agnostic:
/// the owner is identified by an opaque <see cref="OwnerKind"/> + <see cref="OwnerId"/> pair so
/// the engine doesn't know about Forms, Contacts, etc.
/// </summary>
public record WorkflowDefinition
{
    public required Guid Id { get; init; }

    public required Guid TenantId { get; init; }

    /// <summary>Opaque owner classification — <c>"Form"</c>, <c>"Contact"</c>, <c>"Standalone"</c>, etc.</summary>
    public required string OwnerKind { get; init; }

    /// <summary>Owner entity id; null for <c>OwnerKind="Standalone"</c>.</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Trigger this definition listens to — see <see cref="WorkflowTriggerKinds"/>.</summary>
    public required string TriggerKind { get; init; }

    /// <summary>
    /// Optional human-readable label. Useful for standalone / custom workflows the user can pick
    /// from a list (e.g. as the target of a <c>RunWorkflow</c> action) — without it, the only
    /// identifier in the UI is the (ownerKind, ownerId, triggerKind) tuple, which is opaque to
    /// the user. Null when the definition is implicitly named by its owner (e.g. a Form's
    /// SubmissionCompleted definition is identified by the form's title).
    /// </summary>
    public string? DisplayName { get; init; }

    public required WorkflowGraph Graph { get; init; }

    /// <summary>Row creation instant (UTC), set by the store on insert. Default for in-memory records the store never produced.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Last mutation instant (UTC), set by the store on insert/update. Default for in-memory records the store never produced.</summary>
    public DateTime UpdatedAt { get; init; }
}
