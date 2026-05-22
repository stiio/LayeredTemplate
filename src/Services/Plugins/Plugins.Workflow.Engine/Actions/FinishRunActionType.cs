using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Successful early-termination action. Resolves <see cref="FinishRunConfig.ReturnValue"/>
/// and hands it back to the engine as a <see cref="ActionExecutionResult.OnFinish"/> result —
/// the engine flips the run to <c>Completed</c> with that payload on
/// <c>WorkflowRunRecord.ReturnValue</c>, and any sub-workflow parent gets resumed on its
/// <c>success</c> port with the payload as <c>steps.&lt;runWorkflowKey&gt;.returnValue</c>.
/// <para>
/// Declares zero output ports — the validator already rejects edges from a node whose action
/// has no ports, so authors literally can't wire anything past a FinishRun. Mirror of FailRun
/// for the success path.
/// </para>
/// </summary>
public class FinishRunActionType : ActionType<FinishRunConfig>
{
    public const string KindName = "FinishRun";

    public override string Kind => KindName;

    public override string DisplayName => "Finish (return value)";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Array.Empty<ActionPortDescriptor>();

    public override Task<ActionExecutionResult> ExecuteAsync(
        ActionContext<FinishRunConfig> context, CancellationToken cancellationToken)
    {
        return Task.FromResult(this.Finish(context.Config.ReturnValue?.Resolved));
    }
}

public class FinishRunConfig
{
    /// <summary>
    /// Optional payload surfaced to the parent run (when this run was started by a
    /// <c>RunWorkflow</c> action in wait mode). Null = parent gets <c>returnValue: null</c>.
    /// Resolves through any expression engine, so authors can build composite payloads via
    /// Liquid / JS.
    /// </summary>
    public Expr<object>? ReturnValue { get; set; }
}
