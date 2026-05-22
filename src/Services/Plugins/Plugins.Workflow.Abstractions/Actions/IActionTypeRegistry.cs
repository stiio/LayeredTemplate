namespace LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

/// <summary>
/// Lookup index over registered <see cref="IActionType"/>s. Engine resolves actions by their
/// stable <c>Kind</c> string (referenced from workflow nodes) and inspects their static port
/// metadata when walking edges.
/// </summary>
public interface IActionTypeRegistry
{
    /// <summary>Strict lookup — throws if <paramref name="kind"/> is unknown.</summary>
    IActionType Get(string kind);

    /// <summary>Lenient lookup — returns null if <paramref name="kind"/> is unknown.</summary>
    IActionType? TryGet(string kind);

    IReadOnlyList<IActionType> All { get; }

    /// <summary>Resolves a single port descriptor by (kind, portId). Null if either is missing.</summary>
    ActionPortDescriptor? GetPort(string kind, string portId);
}
