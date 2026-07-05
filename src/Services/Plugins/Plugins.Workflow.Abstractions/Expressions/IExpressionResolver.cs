using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

/// <summary>
/// Walks a deserialized config object, finds every <c>Expr&lt;T&gt;</c> leaf, evaluates each
/// via its declared engine, and populates <c>Expr&lt;T&gt;.Resolved</c>. The action then reads
/// values via the implicit <c>Expr&lt;T&gt; → T</c> conversion.
/// <para>
/// Two-phase resolution: regular fields resolve at step-build time via
/// <see cref="ResolveConfigAsync"/> and their resolved values are persisted for audit;
/// transient fields (<see cref="Expr{T}.Transient"/> / <see cref="TransientExprAttribute"/> —
/// secrets, heavy payloads) are skipped there and resolve just-in-time via
/// <see cref="ResolveTransientAsync"/> in the worker, so their values never touch storage.
/// </para>
/// </summary>
public interface IExpressionResolver
{
    /// <summary>
    /// Build-time phase. Deserializes <paramref name="storedConfig"/> into
    /// <paramref name="configType"/>, walks the graph, evaluates every non-transient
    /// <c>Expr&lt;T&gt;</c> via its declared engine and populates <c>Resolved</c>. Transient
    /// leaves are left untouched — their expression (not value) is what gets persisted.
    /// Returns the populated instance.
    /// </summary>
    ValueTask<object> ResolveConfigAsync(
        JsonElement storedConfig,
        Type configType,
        IDictionary<string, object?> model,
        ExpressionEvaluationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Execution-time phase. Walks an already-deserialized <paramref name="config"/> and
    /// evaluates ONLY transient leaves, populating their <c>Resolved</c> in place — values that
    /// were deliberately not materialised at build time. Fields resolved at build time keep
    /// their persisted values (they are the audit record; re-evaluating them here could drift).
    /// <paramref name="modelFactory"/> is invoked lazily on the first transient leaf found, so
    /// configs without transient fields pay only a reflection walk. Throws
    /// <see cref="ExpressionResolutionException"/> on evaluation failure — callers surface it
    /// with the retry semantics of the invoking site (action error, resume rollback, …).
    /// </summary>
    ValueTask ResolveTransientAsync(
        object config,
        Func<IDictionary<string, object?>> modelFactory,
        ExpressionEvaluationContext context,
        CancellationToken cancellationToken);
}
