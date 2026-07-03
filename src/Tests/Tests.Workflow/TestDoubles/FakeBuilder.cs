using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;

namespace LayeredTemplate.Tests.Workflow.TestDoubles;

/// <summary>
/// Pass-through <see cref="IStepExecutionBuilder"/> — emits a Pending step for the node without
/// touching the expression resolver (config resolution is covered by its own tests).
/// </summary>
internal class FakeBuilder : IStepExecutionBuilder
{
    private static readonly JsonElement EmptyJsonObject = JsonDocument.Parse("{}").RootElement;

    public ValueTask<WorkflowStepRecord?> TryBuildAsync(
        WorkflowRunRecord run,
        WorkflowNode node,
        Guid? predecessorExecutionId,
        string? triggerPort,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken)
        => ValueTask.FromResult<WorkflowStepRecord?>(new WorkflowStepRecord
        {
            RunId = run.Id,
            TenantId = run.TenantId,
            NodeId = node.Id,
            Kind = node.Kind,
            Name = node.Name,
            PredecessorExecutionId = predecessorExecutionId,
            TriggerPort = triggerPort,
            ResolvedConfig = EmptyJsonObject,
            Status = StepExecutionStatus.Pending,
            NextAttemptAt = DateTime.UtcNow,
        });
}
