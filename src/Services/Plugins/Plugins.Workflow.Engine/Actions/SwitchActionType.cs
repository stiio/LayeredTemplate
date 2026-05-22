using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Multi-way branch with first-match-wins semantics — fires the port of the first branch whose
/// condition resolves truthy. Falls back to <c>default</c> if none match. Each branch maps to
/// one of <c>branch1..branch5</c> + <c>default</c>; only one port fires per execution, since
/// the engine is single-port-per-step. Authors model parallel branches by wiring sequential
/// follow-ups, not by trying to fan out multiple ports at once.
/// </summary>
public class SwitchActionType : ActionType<SwitchConfig>
{
    public const string KindName = "Switch";

    public const string PortDefault = "default";
    public const int MaxBranches = 5;

    public static readonly IReadOnlyList<ActionPortDescriptor> Ports = new[]
    {
        new ActionPortDescriptor("branch1", "Branch 1", ActionPortKind.Normal),
        new ActionPortDescriptor("branch2", "Branch 2", ActionPortKind.Normal),
        new ActionPortDescriptor("branch3", "Branch 3", ActionPortKind.Normal),
        new ActionPortDescriptor("branch4", "Branch 4", ActionPortKind.Normal),
        new ActionPortDescriptor("branch5", "Branch 5", ActionPortKind.Normal),
        new ActionPortDescriptor(PortDefault, "Default (no match)", ActionPortKind.Normal),
    };

    public override string Kind => KindName;

    public override string DisplayName => "Switch (first match)";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Ports;

    public override Task<ActionExecutionResult> ExecuteAsync(
        ActionContext<SwitchConfig> context, CancellationToken cancellationToken)
    {
        var cfg = context.Config;
        var branches = (cfg.Branches ?? new List<SwitchBranch>())
            .Where(b => !string.IsNullOrEmpty(b.Port))
            .Take(MaxBranches);

        var matched = branches.FirstOrDefault(IsTruthy);
        var port = matched?.Port ?? PortDefault;
        return Task.FromResult(this.Port(port));
    }

    /// <summary>Null condition = unconditional branch (always true).</summary>
    private static bool IsTruthy(SwitchBranch branch) => branch.Condition?.Resolved ?? true;
}

public class SwitchConfig
{
    /// <summary>Up to 5 branches; entries beyond the limit are ignored. Evaluated top-down, first match wins.</summary>
    public List<SwitchBranch> Branches { get; set; } = new();
}

public class SwitchBranch
{
    /// <summary>Port id (<c>branch1</c>..<c>branch5</c>) this branch fires when matched.</summary>
    public string Port { get; set; } = string.Empty;

    /// <summary>Condition expression. Null = always true (used as a final fallthrough).</summary>
    public Expr<bool>? Condition { get; set; }
}
