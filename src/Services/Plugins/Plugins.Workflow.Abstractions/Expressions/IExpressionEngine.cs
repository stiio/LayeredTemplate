using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

/// <summary>
/// Evaluates one kind of expression (static literal, Liquid template, JS expression) and
/// returns a <see cref="JsonElement"/> representing the result. The resolver then coerces
/// that <see cref="JsonElement"/> into the target <c>Expr&lt;T&gt;.Resolved</c> type via
/// <c>JsonSerializer.Deserialize&lt;T&gt;</c>.
/// </summary>
public interface IExpressionEngine
{
    /// <summary>Matches <c>Expr&lt;T&gt;.Engine</c> — see <see cref="ExpressionEngines"/>.</summary>
    string Name { get; }

    /// <summary>
    /// Async-by-default so engines with real I/O paths (Liquid filters that hit S3/DB,
    /// JS host functions returning <c>Task&lt;T&gt;</c>) don't have to sync-over-async block
    /// the worker thread. Static / pure-CPU engines just return a completed ValueTask.
    /// </summary>
    ValueTask<JsonElement> EvaluateAsync(
        string rawValue,
        IDictionary<string, object?> model,
        Type targetType,
        ExpressionEvaluationContext context,
        CancellationToken cancellationToken);
}
