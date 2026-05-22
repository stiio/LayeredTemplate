using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Engines;

/// <summary>
/// Static engine: <c>value</c> is a literal. For <c>Expr&lt;string&gt;</c> it's the string itself;
/// for complex types it's a JSON literal that parses directly into the target type.
/// </summary>
internal class StaticExpressionEngine : IExpressionEngine
{
    public string Name => ExpressionEngines.Static;

    public ValueTask<JsonElement> EvaluateAsync(
        string rawValue,
        IDictionary<string, object?> model,
        Type targetType,
        ExpressionEvaluationContext context,
        CancellationToken cancellationToken)
    {
        // Static engine ignores context — no template, no host functions. Pure CPU work, no I/O,
        // so the async signature is just a contract honourer; we hand back a completed
        // ValueTask without ever yielding.
        if (targetType == typeof(string))
        {
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(rawValue, WorkflowJsonOptions.Default));
        }

        // Other simple targets: let System.Text.Json parse the literal (numbers, bools, null).
        // For POCOs / collections, value must be a JSON literal.
        var trimmed = rawValue?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return ValueTask.FromResult(JsonSerializer.SerializeToElement<object?>(null, WorkflowJsonOptions.Default));
        }
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return ValueTask.FromResult(doc.RootElement.Clone());
        }
        catch (JsonException ex)
        {
            throw new ExpressionResolutionException(
                this.Name,
                path: "<static>",
                targetType.Name,
                $"Value is not valid JSON for target type: {ex.Message}",
                ex);
        }
    }
}
