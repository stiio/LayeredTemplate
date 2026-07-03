using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;

namespace LayeredTemplate.Tests.Workflow.TestDoubles;

/// <summary>
/// Recording <see cref="IWorkflowRunner"/> stub: captures the intent it was handed and returns
/// a canned run (null = the empty-graph outcome). Used by dispatcher / restarter tests that
/// assert what reaches the runner rather than what the runner does.
/// </summary>
internal sealed class FakeRunner : IWorkflowRunner
{
    private readonly WorkflowRunRecord? returnRun;

    public FakeRunner(WorkflowRunRecord? returnRun = null)
    {
        this.returnRun = returnRun;
    }

    public bool StartCalled { get; private set; }

    public WorkflowStartIntent? LastIntent { get; private set; }

    public WorkflowDefinition? LastDefinition { get; private set; }

    public ValueTask<WorkflowRunRecord?> StartAsync(
        WorkflowStartIntent intent,
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        this.StartCalled = true;
        this.LastIntent = intent;
        this.LastDefinition = definition;
        return ValueTask.FromResult(this.returnRun);
    }
}
