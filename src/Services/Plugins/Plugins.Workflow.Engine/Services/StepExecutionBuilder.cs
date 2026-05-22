using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Resolves a node's config via <see cref="IExpressionResolver"/> and emits a
/// <see cref="WorkflowStepRecord"/>. Used by the runner for start nodes and by the engine
/// worker for successor enqueue.
/// </summary>
internal class StepExecutionBuilder : IStepExecutionBuilder
{
    /// <summary>Cached empty JSON object for dead-on-arrival rows that never had real config.</summary>
    private static readonly JsonElement EmptyObject = JsonSerializer.SerializeToElement(new { }, WorkflowJsonOptions.Default);

    private readonly IExpressionResolver expressionResolver;
    private readonly IActionTypeRegistry registry;
    private readonly ILogger<StepExecutionBuilder> logger;

    public StepExecutionBuilder(
        IExpressionResolver expressionResolver,
        IActionTypeRegistry registry,
        ILogger<StepExecutionBuilder> logger)
    {
        this.expressionResolver = expressionResolver;
        this.registry = registry;
        this.logger = logger;
    }

    public async ValueTask<WorkflowStepRecord?> TryBuildAsync(
        WorkflowRunRecord run,
        WorkflowNode node,
        Guid? predecessorExecutionId,
        string? triggerPort,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(node.Id))
        {
            throw new InvalidOperationException("Workflow node missing 'id'.");
        }
        if (string.IsNullOrEmpty(node.Kind))
        {
            throw new InvalidOperationException($"Node '{node.Id}' missing 'kind'.");
        }

        var actionType = this.registry.TryGet(node.Kind);
        if (actionType is null)
        {
            this.logger.LogWarning("Unknown action kind '{Kind}' on node '{NodeId}' — skipping.", node.Kind, node.Id);
            return null;
        }

        var config = node.Config.ValueKind == JsonValueKind.Undefined
            ? JsonSerializer.Deserialize<JsonElement>("{}", WorkflowJsonOptions.Default)
            : node.Config;

        // Mutable model for the resolver (it may add per-step values; we don't care here, but
        // ensure it can write).
        var resolverModel = model is Dictionary<string, object?> mutable
            ? mutable
            : new Dictionary<string, object?>(model);

        var evaluationContext = new ExpressionEvaluationContext
        {
            TenantId = run.TenantId,
            RunId = run.Id,
            DefinitionId = run.DefinitionId,
            ActorUserId = run.ActorUserId,
            TriggerSourceKind = run.TriggerSourceKind,
            TriggerSourceId = run.TriggerSourceId,
            IsDryRun = run.IsDryRun,
        };

        object resolved;
        try
        {
            resolved = await this.expressionResolver.ResolveConfigAsync(config, actionType.ConfigType, resolverModel, evaluationContext, cancellationToken);
        }
        catch (ExpressionResolutionException ex)
        {
            this.logger.LogWarning(ex, "Expression resolution failed for node '{NodeId}' ({Kind})", node.Id, node.Kind);
            return new WorkflowStepRecord
            {
                RunId = run.Id,
                TenantId = run.TenantId,
                NodeId = node.Id,
                Kind = node.Kind,
                Name = node.Name,
                PredecessorExecutionId = predecessorExecutionId,
                TriggerPort = triggerPort,
                ResolvedConfig = EmptyObject,
                // Lane stamp matters even for dead-on-arrival rows: the long-running pool's
                // sweeper still picks them up via the same lane filter (and immediately moves
                // on, since they're already terminal).
                IsLongRunning = actionType.IsLongRunning,
                Status = StepExecutionStatus.Dead,
                LastError = ex.Message,
                NextAttemptAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
            };
        }

        // Resolved config goes straight to JsonElement — store layer's converter pushes the
        // raw bytes into bytea without re-stringifying. Options must match the worker's read
        // path (camelCase + enum-as-string) so the round-trip is symmetric.
        var resolvedJson = JsonSerializer.SerializeToElement(resolved, actionType.ConfigType, WorkflowJsonOptions.Default);

        return new WorkflowStepRecord
        {
            RunId = run.Id,
            TenantId = run.TenantId,
            NodeId = node.Id,
            Kind = node.Kind,
            Name = node.Name,
            PredecessorExecutionId = predecessorExecutionId,
            TriggerPort = triggerPort,
            ResolvedConfig = resolvedJson,
            // Read once at build time; the row carries the decision through every retry / timeout
            // sweep. Flipping the action's IsLongRunning later doesn't affect already-built rows.
            IsLongRunning = actionType.IsLongRunning,
            Status = StepExecutionStatus.Pending,
            NextAttemptAt = DateTime.UtcNow,
        };
    }
}
