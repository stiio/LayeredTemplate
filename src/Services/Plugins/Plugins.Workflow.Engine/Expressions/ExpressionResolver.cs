using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions;

/// <summary>
/// Resolves all <see cref="Expr{T}"/> leaves inside a deserialized config object.
/// Walk rule: for any visited node — if it's an <c>Expr&lt;T&gt;</c>, evaluate via its engine
/// and populate <c>Resolved</c>. Otherwise, descend into public fields/properties / list items /
/// dictionary values. Primitives and strings terminate the walk.
/// </summary>
internal class ExpressionResolver : IExpressionResolver
{
    /// <summary>
    /// Per-type reflection cache: public readable instance properties (indexers excluded —
    /// they need arguments) paired with the camelCase path segment used in error paths.
    /// Process-wide: config POCOs are a small closed set of shapes, and this walk runs on
    /// every step build — re-running GetProperties + camelCase per visit was pure waste.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Prop, string PathSegment)[]> PropertiesByType = new();

    /// <summary>Cached Engine / Value getters + Resolved setter per closed <c>Expr&lt;T&gt;</c> type.</summary>
    private static readonly ConcurrentDictionary<Type, ExprAccessors> ExprAccessorsByType = new();

    private readonly Dictionary<string, IExpressionEngine> engines;

    public ExpressionResolver(IEnumerable<IExpressionEngine> engines)
    {
        this.engines = engines.ToDictionary(e => e.Name, StringComparer.Ordinal);
    }

    public async ValueTask<object> ResolveConfigAsync(
        JsonElement storedConfig,
        Type configType,
        IDictionary<string, object?> model,
        ExpressionEvaluationContext context,
        CancellationToken cancellationToken)
    {
        var config = storedConfig.Deserialize(configType, WorkflowJsonOptions.Default)
            ?? throw new InvalidOperationException($"Failed to deserialize config as {configType.Name}.");

        await this.ResolveNodeAsync(config, model, context, path: "config", cancellationToken);
        return config;
    }

    private async ValueTask ResolveNodeAsync(object? node, IDictionary<string, object?> model, ExpressionEvaluationContext context, string path, CancellationToken cancellationToken)
    {
        if (node is null) return;

        var type = node.GetType();

        // Leaf: Expr<T>.
        if (IsExprType(type, out var innerType))
        {
            await this.ResolveExprAsync(node, innerType!, model, context, path, cancellationToken);
            return;
        }

        // Primitives / strings — nothing to descend into.
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid)
            || type.IsEnum)
        {
            return;
        }

        // Dictionary<string, V>.
        if (node is IDictionary dict)
        {
            foreach (var key in dict.Keys)
            {
                await this.ResolveNodeAsync(dict[key!], model, context, $"{path}.{key}", cancellationToken);
            }
            return;
        }

        // IEnumerable (lists, arrays).
        if (node is IEnumerable enumerable)
        {
            var i = 0;
            foreach (var item in enumerable)
            {
                await this.ResolveNodeAsync(item, model, context, $"{path}[{i}]", cancellationToken);
                i++;
            }
            return;
        }

        // Object: iterate public readable props (reflection metadata cached per type).
        var members = PropertiesByType.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Select(p => (p, ToCamelCase(p.Name)))
            .ToArray());
        foreach (var (prop, pathSegment) in members)
        {
            object? value;
            try { value = prop.GetValue(node); }
            catch { continue; }
            await this.ResolveNodeAsync(value, model, context, $"{path}.{pathSegment}", cancellationToken);
        }
    }

    private async ValueTask ResolveExprAsync(object exprInstance, Type innerType, IDictionary<string, object?> model, ExpressionEvaluationContext context, string path, CancellationToken cancellationToken)
    {
        var type = exprInstance.GetType();
        var accessors = ExprAccessorsByType.GetOrAdd(type, static t => new ExprAccessors(
            t.GetProperty(nameof(Expr<object>.Engine))!,
            t.GetProperty(nameof(Expr<object>.Value))!,
            t.GetProperty(nameof(Expr<object>.Resolved))!));
        var engineName = (string)accessors.Engine.GetValue(exprInstance)!;
        var rawValue = (string)accessors.Value.GetValue(exprInstance)!;

        if (!this.engines.TryGetValue(engineName, out var engine))
        {
            throw new ExpressionResolutionException(engineName, path, innerType.Name, $"No engine registered for '{engineName}'.");
        }

        JsonElement evaluated;
        try
        {
            evaluated = await engine.EvaluateAsync(rawValue, model, innerType, context, cancellationToken);
        }
        catch (ExpressionResolutionException ex)
        {
            // Re-wrap with the actual path.
            throw new ExpressionResolutionException(ex.Engine, path, ex.TargetType, ex.Message, ex.InnerException);
        }

        object? resolved;
        try
        {
            resolved = evaluated.ValueKind == JsonValueKind.Null || evaluated.ValueKind == JsonValueKind.Undefined
                ? GetDefault(innerType)
                : evaluated.Deserialize(innerType, WorkflowJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            throw new ExpressionResolutionException(
                engineName,
                path,
                innerType.Name,
                $"Could not coerce to target type ({evaluated.GetRawText()[..Math.Min(80, evaluated.GetRawText().Length)]}…): {ex.Message}",
                ex);
        }

        accessors.Resolved.SetValue(exprInstance, resolved);
    }

    private static bool IsExprType(Type type, out Type? innerType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Expr<>))
        {
            innerType = type.GetGenericArguments()[0];
            return true;
        }
        innerType = null;
        return false;
    }

    private static object? GetDefault(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;

    private static string ToCamelCase(string s) =>
        string.IsNullOrEmpty(s) || char.IsLower(s[0]) ? s : char.ToLowerInvariant(s[0]) + s[1..];

    private sealed record ExprAccessors(PropertyInfo Engine, PropertyInfo Value, PropertyInfo Resolved);
}
