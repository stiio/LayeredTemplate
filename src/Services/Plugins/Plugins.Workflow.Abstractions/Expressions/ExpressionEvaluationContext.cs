namespace LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

/// <summary>
/// Tenant + run metadata threaded into every expression evaluation. Exposed to Liquid/JS
/// extensions so custom filters / globals / converters can read tenant scope, actor identity,
/// and trigger source without grovelling through scoped services.
/// </summary>
/// <remarks>
/// Engine-agnostic POCO — lives in Abstractions so the <see cref="IExpressionEngine"/>
/// signature can carry it without dragging Fluid / Jint into the abstraction surface.
/// </remarks>
public class ExpressionEvaluationContext
{
    /// <summary>Tenant the run belongs to. Mirrors <c>WorkflowRunRecord.TenantId</c>.</summary>
    public Guid TenantId { get; init; }

    public Guid RunId { get; init; }

    public Guid DefinitionId { get; init; }

    /// <summary>User who triggered the run, when known. Null for anonymous public-form submissions.</summary>
    public Guid? ActorUserId { get; init; }

    public string? TriggerSourceKind { get; init; }

    public Guid? TriggerSourceId { get; init; }

    public bool IsDryRun { get; init; }
}
