using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;

namespace LayeredTemplate.Tests.Workflow.TestDoubles;

/// <summary>
/// Recording no-op <see cref="IWorkflowFanOut"/> — for tests that assert a service DROVE the
/// fan-out (canceller's parent-resume cascade) rather than what the fan-out does. Tests that
/// need real edge-walking use the actual <c>WorkflowFanOut</c> over <see cref="FakeStore"/>.
/// </summary>
internal sealed class FakeFanOut : IWorkflowFanOut
{
    public int OnRunFinalizedCallCount { get; private set; }

    public Guid? LastFinalizedRunId { get; private set; }

    public Task EnqueueNextStepAsync(WorkflowStepRecord completedStep, string? firedPort, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task CheckRunCompletionAsync(WorkflowStepRecord justFinished, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task OnRunFinalizedAsync(Guid runId, CancellationToken cancellationToken)
    {
        this.OnRunFinalizedCallCount++;
        this.LastFinalizedRunId = runId;
        return Task.CompletedTask;
    }

    public Task<WorkflowGraph?> GetGraphAsync(WorkflowRunRecord run, CancellationToken cancellationToken)
        => Task.FromResult<WorkflowGraph?>(null);
}
