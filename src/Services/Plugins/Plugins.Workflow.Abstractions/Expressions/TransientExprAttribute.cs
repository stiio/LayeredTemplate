namespace LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

/// <summary>
/// Action-type-enforced counterpart of <see cref="Expr{T}.Transient"/>: every
/// <see cref="Expr{T}"/> reached through the decorated config property is treated as transient
/// regardless of what the stored expression says — skipped at step-build time, resolved
/// just-in-time before the action runs, resolved value never persisted. Put it on fields that
/// are secret by nature (signing keys, tokens) so authors don't have to know about the
/// per-field flag for the engine to keep the value out of the database.
/// <code>
/// [TransientExpr]
/// public Expr&lt;string&gt;? SignSecret { get; set; }
/// </code>
/// The attribute covers the property's whole subtree: on a collection property
/// (e.g. <c>List&lt;Header&gt;</c>) every <c>Expr</c> inside the items is forced transient.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TransientExprAttribute : Attribute
{
}
