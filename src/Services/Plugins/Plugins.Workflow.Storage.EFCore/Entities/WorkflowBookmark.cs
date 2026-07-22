namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Entities;

/// <summary>
/// Persistent bookmark row for the generic signal-wait primitive. A suspended step registers one
/// row per <c>WorkflowBookmarkRegistration</c>; an external
/// <c>IWorkflowSignaler.SignalAsync(tenant, correlation_key, payload)</c> finds all rows for that
/// (tenant, key) pair and resumes the exact frozen <see cref="StepId"/> via the resumer. Mapped
/// to/from <c>Hipaa.Backend.Plugins.Workflow.Abstractions.Models.WorkflowBookmarkRecord</c>.
/// <para>
/// Not protected/encrypted: the correlation key is an opaque domain id (e.g. a submission id),
/// never PHI by the engine's contract. Lifetime is bounded — deleted on resume (eager) and by the
/// reconciliation sweep (backstop); cascade-deleted when the owning run is purged.
/// </para>
/// </summary>
internal class WorkflowBookmark
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    /// <summary>FK → <see cref="WorkflowRun"/> (ON DELETE CASCADE) — kills the "run vanished" orphan class.</summary>
    public Guid RunId { get; set; }

    /// <summary>The exact Waiting step this bookmark resumes — frozen at suspend time.</summary>
    public Guid StepId { get; set; }

    /// <summary>Opaque match key. Engine never parses it; signal lookup is exact-string + tenant-scoped.</summary>
    public string CorrelationKey { get; set; } = null!;

    /// <summary>Port to fire on the frozen step when this bookmark is signalled.</summary>
    public string ResumePort { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public WorkflowRun Run { get; set; } = null!;
}
