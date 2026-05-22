namespace LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

/// <summary>
/// UI / authoring marker for an output port. The engine fires successor steps based on the
/// single port the action returns in <see cref="ActionExecutionResult.OutputPort"/>; the kind
/// here only drives canvas colour and validator hints (e.g. Error-kind ports are allowed to
/// be left disconnected without a warning).
/// </summary>
public enum ActionPortKind
{
    /// <summary>Specific outcome (e.g. <c>success</c>, <c>true</c>, <c>branch1</c>, <c>done</c>). Default green colour.</summary>
    Normal,

    /// <summary>Failure outcome — fired when the action explicitly hits an error branch. Red.</summary>
    Error,
}

/// <summary>Static metadata about a single output port — exposed by <c>IActionType.OutputPorts</c>.</summary>
public record ActionPortDescriptor(string Id, string Label, ActionPortKind Kind);
