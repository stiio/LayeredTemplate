using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Branches the workflow based on an expression that evaluates to a boolean. Liquid/static
/// templates rendering the strings <c>"true"</c>/<c>"false"</c> coerce naturally; JS expressions
/// return a native bool. Ports: <c>true</c>, <c>false</c>.
/// </summary>
public class ConditionActionType : ActionType<ConditionConfig>
{
    public const string KindName = "Condition";

    public static readonly IReadOnlyList<ActionPortDescriptor> Ports = new[]
    {
        new ActionPortDescriptor("true", "True", ActionPortKind.Normal),
        new ActionPortDescriptor("false", "False", ActionPortKind.Normal),
    };

    public override string Kind => KindName;

    public override string DisplayName => "Condition (if / else)";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Ports;

    public override Task<ActionExecutionResult> ExecuteAsync(ActionContext<ConditionConfig> context, CancellationToken cancellationToken)
    {
        var result = context.Config.Expression.Resolved;
        return Task.FromResult(this.Port(result ? "true" : "false", new { result }));
    }
}

public class ConditionConfig
{
    public Expr<bool> Expression { get; set; } = new();
}
