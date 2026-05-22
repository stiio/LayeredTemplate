using LayeredTemplate.Plugins.Workflow.Abstractions.Models;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Universal entry point into the workflow engine. Given a <see cref="WorkflowStartIntent"/>
/// and the matching <see cref="WorkflowDefinition"/>, builds and stages a
/// <see cref="WorkflowRunRecord"/> + initial step records inside <see cref="IWorkflowStore"/>.
/// Caller is responsible for committing via <c>store.SaveChangesAsync</c> alongside any
/// domain-side changes (the submit transaction commits both atomically).
/// </summary>
public interface IWorkflowRunner
{
    /// <summary>Returns the staged record, or null if the graph has no runnable start nodes.</summary>
    ValueTask<WorkflowRunRecord?> StartAsync(
        WorkflowStartIntent intent,
        WorkflowDefinition definition,
        CancellationToken cancellationToken);
}
