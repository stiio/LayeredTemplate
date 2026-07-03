using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

namespace LayeredTemplate.Tests.Workflow.TestDoubles;

/// <summary>
/// Dictionary-backed <see cref="IActionTypeRegistry"/>. Either wrap real action instances
/// (sweep tests exercising built-ins) or use the convenience ctor that fabricates a single
/// <see cref="FakeAction"/> with a canned result (worker execute tests).
/// </summary>
internal class FakeRegistry : IActionTypeRegistry
{
    private readonly Dictionary<string, IActionType> map;

    public FakeRegistry(string kind, IReadOnlyList<ActionPortDescriptor> ports, ActionExecutionResult result)
        : this(new FakeAction(kind, ports, result))
    {
    }

    public FakeRegistry(params IActionType[] actions)
    {
        this.map = actions.ToDictionary(a => a.Kind, StringComparer.Ordinal);
    }

    public IReadOnlyList<IActionType> All => this.map.Values.ToList();

    public IActionType Get(string kind) => this.map[kind];

    public IActionType? TryGet(string kind) => this.map.TryGetValue(kind, out var t) ? t : null;

    public ActionPortDescriptor? GetPort(string kind, string portId)
        => this.TryGet(kind)?.OutputPorts.FirstOrDefault(p => p.Id == portId);
}
