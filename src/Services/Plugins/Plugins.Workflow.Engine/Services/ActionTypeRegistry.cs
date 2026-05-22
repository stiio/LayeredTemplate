using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

internal class ActionTypeRegistry : IActionTypeRegistry
{
    private readonly Dictionary<string, IActionType> byKind;

    public ActionTypeRegistry(IEnumerable<IActionType> actionTypes)
    {
        this.byKind = actionTypes.ToDictionary(t => t.Kind, StringComparer.Ordinal);
        this.All = this.byKind.Values.ToList();
    }

    public IReadOnlyList<IActionType> All { get; }

    public IActionType Get(string kind) =>
        this.byKind.TryGetValue(kind, out var t)
            ? t
            : throw new InvalidOperationException($"Unknown action type '{kind}'.");

    public IActionType? TryGet(string kind) =>
        this.byKind.TryGetValue(kind, out var t) ? t : null;

    public ActionPortDescriptor? GetPort(string kind, string portId) =>
        this.TryGet(kind)?.OutputPorts.FirstOrDefault(p => p.Id == portId);
}
