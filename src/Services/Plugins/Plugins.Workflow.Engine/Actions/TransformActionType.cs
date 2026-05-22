using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Computes a flat map of named values (each via any expression engine) and exposes them as
/// outputs so later steps can read <c>{{ steps.&lt;node_key&gt;.&lt;name&gt; }}</c> in Liquid or
/// <c>steps.&lt;node_key&gt;.&lt;name&gt;</c> in JS. Single port: <c>done</c>.
/// </summary>
public class TransformActionType : ActionType<TransformConfig>
{
    public const string KindName = "Transform";

    public static readonly IReadOnlyList<ActionPortDescriptor> Ports = new[]
    {
        new ActionPortDescriptor("done", "Done", ActionPortKind.Normal),
    };

    public override string Kind => KindName;

    public override string DisplayName => "Transform (set variables)";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Ports;

    public override Task<ActionExecutionResult> ExecuteAsync(ActionContext<TransformConfig> context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, object?>();
        foreach (var entry in context.Config.Values ?? new())
        {
            if (string.IsNullOrWhiteSpace(entry.Name)) continue;
            outputs[entry.Name] = entry.Expression?.Resolved;
        }
        return Task.FromResult(this.Port("done", outputs));
    }
}

public class TransformConfig
{
    public List<TransformValue> Values { get; set; } = new();
}

public class TransformValue
{
    public string Name { get; set; } = string.Empty;

    public Expr<object>? Expression { get; set; }
}
