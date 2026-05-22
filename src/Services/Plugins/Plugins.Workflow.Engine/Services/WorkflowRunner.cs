using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Universal workflow starter. Trigger-agnostic: caller passes a fully-formed
/// <see cref="WorkflowStartIntent"/> + <see cref="WorkflowDefinition"/>; the runner builds an
/// in-memory <see cref="WorkflowRunRecord"/> + initial step records and stages them in the
/// <see cref="IWorkflowStore"/>. Caller flushes via <c>store.SaveChangesAsync</c> (typically
/// alongside its own domain changes — e.g. the submit transaction).
/// </summary>
internal class WorkflowRunner : IWorkflowRunner
{
    private readonly IStepExecutionBuilder stepBuilder;
    private readonly IWorkflowStore store;

    public WorkflowRunner(IStepExecutionBuilder stepBuilder, IWorkflowStore store)
    {
        this.stepBuilder = stepBuilder;
        this.store = store;
    }

    public async ValueTask<WorkflowRunRecord?> StartAsync(
        WorkflowStartIntent intent,
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        var graph = definition.Graph;
        if (graph.Nodes.Count == 0 || string.IsNullOrEmpty(graph.StartNodeId)) return null;

        var nodesById = graph.Nodes
            .Where(n => !string.IsNullOrEmpty(n.Id))
            .ToDictionary(n => n.Id, StringComparer.Ordinal);

        if (!nodesById.TryGetValue(graph.StartNodeId, out var startNode)) return null;

        var staticContext = BuildStaticContextFromIntent(intent);
        var emptyStepsOutputs = EmptyObject;
        var model = ExpressionModelBuilder.Build(staticContext, emptyStepsOutputs);

        var snapshotJson = JsonSerializer.Serialize(graph, WorkflowJsonOptions.Default);

        var run = new WorkflowRunRecord
        {
            TenantId = intent.TenantId,
            DefinitionId = definition.Id,
            TriggerKind = intent.TriggerKind,
            TriggerSourceKind = intent.TriggerSourceKind,
            TriggerSourceId = intent.TriggerSourceId,
            WorkflowSnapshot = snapshotJson,
            StaticContext = staticContext,
            StepsOutputs = emptyStepsOutputs,
            Status = WorkflowRunStatus.Running,
            StartedAt = DateTime.UtcNow,
            IsDryRun = intent.IsDryRun,
            // Caller-supplied label seeded at dispatch time. SetRunName action overrides this
            // mid-run if the graph carries one. Trim + cap mirrors the action's policy so the
            // two ingress paths agree.
            Name = NormalizeName(intent.Name),
            ActorUserId = intent.ActorUserId,
            NestingLevel = intent.NestingLevel,
            ParentRunId = intent.ParentRunId,
            ParentStepId = intent.ParentStepId,
        };

        var initialStep = await this.stepBuilder.TryBuildAsync(
            run, startNode, predecessorExecutionId: null, triggerPort: null, model, cancellationToken);
        if (initialStep is null) return null;

        // Stage the run + its single start step in the store. Caller calls SaveChangesAsync to flush.
        this.store.AddRun(run);
        this.store.AddStep(initialStep);

        return run;
    }

    /// <summary>
    /// Static context shape: two top-level slots — <c>trigger</c> (engine-owned metadata) and
    /// <c>vars</c> (the trigger source's payload). Splitting the namespace keeps consumer keys
    /// from ever colliding with engine-added ones; templates address them as
    /// <c>{{ vars.answers.email }}</c> and <c>{{ trigger.kind }}</c> respectively.
    /// <para>
    /// <see cref="WorkflowStartIntent.Variables"/> is taken as-is — JSON in, JSON out — so the
    /// shape contract is what the consumer sent. Null collapses to an empty object. Anything
    /// other than a JSON object throws: the engine's expression model assumes <c>vars</c> is
    /// keyable, and silently substituting <c>{}</c> for a malformed value would hide bugs in
    /// the trigger code.
    /// </para>
    /// </summary>
    private static JsonElement BuildStaticContextFromIntent(WorkflowStartIntent intent)
    {
        if (intent.Variables is { } v && v.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                $"WorkflowStartIntent.Variables must be a JSON object or null (got {v.ValueKind}).",
                nameof(intent));
        }

        // SerializeToElement accepts null and emits the JSON `null` literal; we pre-substitute
        // an empty object so the resulting static_context always has `vars` as a real object,
        // simplifying the resolver / restart paths (no null-vs-{} branching).
        var output = new Dictionary<string, object?>
        {
            ["trigger"] = new
            {
                kind = intent.TriggerKind,
                isDryRun = intent.IsDryRun,
                sourceKind = intent.TriggerSourceKind,
                sourceId = intent.TriggerSourceId?.ToString(),
            },
            ["vars"] = intent.Variables ?? EmptyObject,
        };
        return JsonSerializer.SerializeToElement(output, WorkflowJsonOptions.Default);
    }

    /// <summary>
    /// Cached empty-object JsonElement used when an intent supplies no variables. SerializeToElement
    /// returns a self-contained element, so the static field stays valid for the process lifetime
    /// without keeping a JsonDocument alive.
    /// </summary>
    private static readonly JsonElement EmptyObject = JsonSerializer.SerializeToElement(new { }, WorkflowJsonOptions.Default);

    /// <summary>
    /// Length-cap + trim policy for run names. Empty / whitespace collapses to <c>null</c> so
    /// the column stays unset rather than carrying a meaningless empty string.
    /// </summary>
    internal const int MaxNameLength = 256;

    internal static string? NormalizeName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }
}
