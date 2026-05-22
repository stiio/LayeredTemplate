using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

/// <summary>
/// Walks a deserialized config object, finds every <c>Expr&lt;T&gt;</c> leaf, evaluates each
/// via its declared engine, and populates <c>Expr&lt;T&gt;.Resolved</c>. The action then reads
/// values via the implicit <c>Expr&lt;T&gt; → T</c> conversion.
/// </summary>
public interface IExpressionResolver
{
    /// <summary>
    /// Deserializes <paramref name="storedConfig"/> into <paramref name="configType"/>,
    /// walks the graph, evaluates every <c>Expr&lt;T&gt;</c> via its declared engine and
    /// populates <c>Resolved</c>. Returns the populated instance.
    /// </summary>
    ValueTask<object> ResolveConfigAsync(
        JsonElement storedConfig,
        Type configType,
        IDictionary<string, object?> model,
        ExpressionEvaluationContext context,
        CancellationToken cancellationToken);
}
