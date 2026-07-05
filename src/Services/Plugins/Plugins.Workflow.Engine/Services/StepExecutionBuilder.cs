using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Resolves a node's config via <see cref="IExpressionResolver"/> and emits a
/// <see cref="WorkflowStepRecord"/>. Used by the runner for start nodes and by the engine
/// worker for successor enqueue. Build-time phase of the two-phase resolution: non-transient
/// fields are materialised and persisted here; transient fields keep their raw expression in
/// the stored config and resolve just-in-time in the worker.
/// </summary>
internal class StepExecutionBuilder : IStepExecutionBuilder
{
    /// <summary>Cached empty JSON object for dead-on-arrival rows that never had real config.</summary>
    private static readonly JsonElement EmptyObject = JsonSerializer.SerializeToElement(new { }, WorkflowJsonOptions.Default);

    private readonly IExpressionResolver expressionResolver;
    private readonly IActionTypeRegistry registry;
    private readonly WorkflowEngineSettings settings;
    private readonly ILogger<StepExecutionBuilder> logger;

    public StepExecutionBuilder(
        IExpressionResolver expressionResolver,
        IActionTypeRegistry registry,
        IOptions<WorkflowEngineSettings> settings,
        ILogger<StepExecutionBuilder> logger)
    {
        this.expressionResolver = expressionResolver;
        this.registry = registry;
        this.settings = settings.Value;
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

        var evaluationContext = ExpressionModelBuilder.EvaluationContextForRun(run);

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
                // Lane stamp kept for row-shape consistency — terminal rows are never claimed
                // by either pool, but a uniform column beats a "sometimes default" one.
                IsLongRunning = actionType.IsLongRunning,
                Status = StepExecutionStatus.Dead,
                LastError = ex.Message,
                NextAttemptAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
            };
        }

        // Resolved config goes straight to JsonElement — store layer's converter pushes the
        // raw bytes into bytea without re-stringifying. Options must match the worker's read
        // path (camelCase + enum-as-string) so the round-trip is symmetric. Transient fields
        // serialize as their raw expression (the Expr converter never writes a transient
        // resolved value), so nothing secret / heavy is in this payload by construction.
        var resolvedJson = JsonSerializer.SerializeToElement(resolved, actionType.ConfigType, WorkflowJsonOptions.Default);

        // Size guardrail: a resolved config past this cap almost always means file content
        // (base64) or a similar payload got materialised into a persisted field. Fail loud with
        // actionable advice instead of silently bloating the step row (and, transitively, every
        // later rewrite of the run).
        var configChars = resolvedJson.GetRawText().Length;
        if (configChars > this.settings.MaxResolvedConfigChars)
        {
            this.logger.LogWarning(
                "Resolved config for node '{NodeId}' ({Kind}) is {Chars} chars — exceeds MaxResolvedConfigChars={Cap}.",
                node.Id, node.Kind, configChars, this.settings.MaxResolvedConfigChars);
            return new WorkflowStepRecord
            {
                RunId = run.Id,
                TenantId = run.TenantId,
                NodeId = node.Id,
                Kind = node.Kind,
                Name = node.Name,
                PredecessorExecutionId = predecessorExecutionId,
                TriggerPort = triggerPort,
                // The oversized payload is deliberately NOT persisted — that's the whole point.
                ResolvedConfig = EmptyObject,
                IsLongRunning = actionType.IsLongRunning,
                Status = StepExecutionStatus.Dead,
                LastError = $"Resolved config is {configChars} chars, exceeding the {this.settings.MaxResolvedConfigChars}-char cap. "
                    + "Mark heavy fields as transient (resolved at execution, never persisted) or pass references instead of content.",
                NextAttemptAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
            };
        }

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
