using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;

namespace LayeredTemplate.Tests.Workflow.TestDoubles;

/// <summary>
/// Canned <see cref="IActionType"/>: declares the supplied ports and returns a fixed
/// <see cref="ActionExecutionResult"/> from every <c>ExecuteAsync</c>. Resume / timeout hooks
/// keep the interface defaults (fail-loud non-transient error).
/// </summary>
internal class FakeAction : IActionType
{
    private readonly ActionExecutionResult result;

    public FakeAction(string kind, IReadOnlyList<ActionPortDescriptor> ports, ActionExecutionResult result)
    {
        this.Kind = kind;
        this.OutputPorts = ports;
        this.result = result;
    }

    public string Kind { get; }

    public string DisplayName => this.Kind;

    public IReadOnlyList<ActionPortDescriptor> OutputPorts { get; }

    public Type ConfigType => typeof(object);

    public Task<ActionExecutionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
        => Task.FromResult(this.result);
}
