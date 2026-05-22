using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Builds a <see cref="WorkflowStepRecord"/> for a workflow node — resolves its config via the
/// expression resolver and emits a <c>Pending</c> record (or <c>Dead</c> if resolution fails).
/// Shared between the runner (for start nodes) and the worker (for successor enqueue) so the
/// caller doesn't need to know which is which.
/// </summary>
public interface IStepExecutionBuilder
{
    /// <summary>
    /// Returns the step record to insert, or null if the node's <c>Kind</c> is unknown
    /// (callers skip silently in that case).
    /// </summary>
    ValueTask<WorkflowStepRecord?> TryBuildAsync(
        WorkflowRunRecord run,
        WorkflowNode node,
        Guid? predecessorExecutionId,
        string? triggerPort,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken);
}
