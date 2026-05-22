using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Iterates a JSON array sequentially. On the first visit it resolves <see cref="ForEachConfig.Items"/>
/// to a frozen array; on every subsequent visit it consults its own previous outputs in
/// <c>steps_outputs</c> to find the index, fires the <c>iterate</c> port for the next element,
/// and finally fires <c>done</c> when the index runs past the end.
/// <para>
/// Authors wire the <c>iterate</c> port to the body of the loop and close the body's last edge
/// back to this node — every body completion produces a fresh ForEach step that picks up where
/// the previous one left off. The single-port-per-step engine processes iterations strictly
/// in order; there is no parallel fan-out.
/// </para>
/// <para>
/// The body of the loop revisits its own nodes once per iteration — make sure
/// <c>WorkflowEngineSettings.MaxVisitsPerNode</c> is at least as big as
/// <c>MaxLoopIterations</c> (defaults are 50 / 25, which fits one level of looping with a
/// margin). Nested loops multiply the visit count by the same factor; deeper nesting needs a
/// proportionally bigger cap.
/// </para>
/// </summary>
public class ForEachActionType : ActionType<ForEachConfig>
{
    public const string KindName = "ForEach";

    private const string PortIterate = "iterate";
    private const string PortDone = "done";

    public static readonly IReadOnlyList<ActionPortDescriptor> Ports = new[]
    {
        new ActionPortDescriptor(PortIterate, "Iterate", ActionPortKind.Normal),
        new ActionPortDescriptor(PortDone, "Done", ActionPortKind.Normal),
    };

    private readonly WorkflowEngineSettings settings;

    public ForEachActionType(IOptions<WorkflowEngineSettings> settings)
    {
        this.settings = settings.Value;
    }

    public override string Kind => KindName;

    public override string DisplayName => "For each";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Ports;

    public override Task<ActionExecutionResult> ExecuteAsync(
        ActionContext<ForEachConfig> context, CancellationToken cancellationToken)
    {
        // Look up our own previous outputs (latest step on this node, if any). Steps_outputs is
        // a JSON object keyed by node-key — engine populates it as steps complete.
        var prev = TryGetPrevious(context.StepsOutputs, context.NodeKey);

        // Frozen items: first call resolves cfg.Items; subsequent calls read the array from
        // their predecessor's outputs so the iterable stays stable across iterations even when
        // the source expression isn't deterministic (e.g. depends on `now`).
        if (!TryReadFrozenItems(prev, out var items))
        {
            if (!TryCoerceToArray(context.Config.Items?.Resolved, out items))
            {
                return Task.FromResult(ActionExecutionResult.OnError(
                    "ForEach 'items' must resolve to an array.",
                    transient: false));
            }

            if (items.Length > this.settings.MaxLoopIterations)
            {
                return Task.FromResult(ActionExecutionResult.OnError(
                    $"ForEach received {items.Length} items, exceeds engine cap of {this.settings.MaxLoopIterations}.",
                    transient: false));
            }
        }

        // Read key must match the camelCase output key written below — JsonElement.TryGetProperty
        // is case-sensitive structural and ignores naming-policy options.
        var index = TryGetInt(prev, "nextIndex", defaultValue: 0);
        var total = items.Length;

        if (total == 0 || index >= total)
        {
            return Task.FromResult(this.Port(PortDone, new
            {
                total,
                completed = true,
            }));
        }

        var item = items[index];
        return Task.FromResult(this.Port(PortIterate, new
        {
            index,
            item,
            items,                          // carry forward — keeps the iterable frozen
            total,
            isFirst = index == 0,
            isLast = index == total - 1,
            nextIndex = index + 1,
        }));
    }

    private static JsonElement? TryGetPrevious(JsonElement stepsOutputs, string nodeKey)
    {
        if (string.IsNullOrEmpty(nodeKey)) return null;
        if (stepsOutputs.ValueKind != JsonValueKind.Object) return null;
        if (!stepsOutputs.TryGetProperty(nodeKey, out var prev)) return null;
        return prev.ValueKind == JsonValueKind.Object ? prev : null;
    }

    private static bool TryReadFrozenItems(JsonElement? prev, out object[] items)
    {
        items = Array.Empty<object>();
        if (prev is null) return false;
        if (!prev.Value.TryGetProperty("items", out var arrEl)) return false;
        if (arrEl.ValueKind != JsonValueKind.Array) return false;
        items = ToObjectArray(arrEl);
        return true;
    }

    private static int TryGetInt(JsonElement? prev, string property, int defaultValue)
    {
        if (prev is null) return defaultValue;
        if (!prev.Value.TryGetProperty(property, out var el)) return defaultValue;
        if (el.ValueKind != JsonValueKind.Number) return defaultValue;
        return el.TryGetInt32(out var i) ? i : defaultValue;
    }

    /// <summary>
    /// Best-effort coercion of <paramref name="resolved"/> into an array. Resolution layer hands us
    /// either a CLR collection (JS path), a <see cref="JsonElement"/> (config-direct), or a string
    /// that may contain a JSON array (Liquid path). Returns false for everything else.
    /// </summary>
    private static bool TryCoerceToArray(object? resolved, out object[] items)
    {
        items = Array.Empty<object>();
        switch (resolved)
        {
            case null:
                return false;
            case JsonElement el when el.ValueKind == JsonValueKind.Array:
                items = ToObjectArray(el);
                return true;
            case JsonElement:
                return false;
            case System.Collections.IEnumerable enumerable when resolved is not string:
                items = enumerable.Cast<object?>().Select(o => o ?? new object()).ToArray();
                return true;
            case string s:
                try
                {
                    using var doc = JsonDocument.Parse(s);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
                    items = ToObjectArray(doc.RootElement);
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
            default:
                return false;
        }
    }

    private static object[] ToObjectArray(JsonElement array)
    {
        var result = new object[array.GetArrayLength()];
        var i = 0;
        foreach (var el in array.EnumerateArray())
        {
            // Round-trip through JsonSerializer so nested objects come back as plain dictionaries
            // suitable for downstream Liquid / JS expression resolution. Performance-cheap for
            // typical loop sizes (capped at MaxLoopIterations anyway).
            result[i++] = JsonSerializer.Deserialize<object>(el.GetRawText(), WorkflowJsonOptions.Default) ?? new object();
        }
        return result;
    }
}

public class ForEachConfig
{
    /// <summary>
    /// Resolved into the array to iterate over. Liquid (<c>{{ vars.answers.attendees }}</c>) and
    /// JS (<c>vars.answers.attendees</c>) both work; static arrays do too. Frozen on the first iteration.
    /// </summary>
    public Expr<object>? Items { get; set; }
}
