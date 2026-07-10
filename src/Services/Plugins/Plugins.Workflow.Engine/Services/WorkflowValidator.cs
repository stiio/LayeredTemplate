using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

internal class WorkflowValidator : IWorkflowValidator
{
    private readonly IActionTypeRegistry registry;
    private readonly WorkflowEngineSettings settings;

    public WorkflowValidator(
        IActionTypeRegistry registry,
        IOptions<WorkflowEngineSettings> settings)
    {
        this.registry = registry;
        this.settings = settings.Value;
    }

    public IReadOnlyList<WorkflowValidationError> Validate(JsonElement workflow)
    {
        if (workflow.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<WorkflowValidationError>();
        }

        WorkflowGraph? graph;
        try
        {
            graph = workflow.Deserialize<WorkflowGraph>(WorkflowJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            return new[] { new WorkflowValidationError("workflow_malformed", $"Could not parse workflow: {ex.Message}") };
        }

        return this.ValidateGraph(graph);
    }

    private IReadOnlyList<WorkflowValidationError> ValidateGraph(WorkflowGraph? graph)
    {
        var errors = new List<WorkflowValidationError>();
        if (graph is null) return errors;

        // Hard size cap — defends storage / runtime against 10k-node monsters that would
        // deserialise on every step and bloat workflow_runs.workflow_snapshot.
        if (graph.Nodes.Count > this.settings.MaxNodesPerGraph)
        {
            errors.Add(new WorkflowValidationError(
                "graph_too_large",
                $"Workflow has {graph.Nodes.Count} nodes; engine cap is {this.settings.MaxNodesPerGraph}."));
            return errors; // bail fast — further checks would be expensive on a malformed graph
        }

        // Nodes: collect ids, check duplicates, kinds, keys.
        var nodesById = new Dictionary<string, WorkflowNode>(StringComparer.Ordinal);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            if (string.IsNullOrEmpty(node.Id)) continue;

            if (nodesById.ContainsKey(node.Id))
            {
                errors.Add(new WorkflowValidationError("node_duplicate_id", $"Duplicate node id '{node.Id}'.", node.Id));
            }
            nodesById[node.Id] = node;

            var key = node.Key?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                errors.Add(new WorkflowValidationError("node_missing_key", $"Node '{node.Id}' has no 'key'.", node.Id));
            }
            else if (!seenKeys.Add(key))
            {
                errors.Add(new WorkflowValidationError("node_duplicate_key", $"Duplicate node key '{key}'.", node.Id));
            }

            if (string.IsNullOrEmpty(node.Kind))
            {
                errors.Add(new WorkflowValidationError("node_missing_kind", $"Node '{node.Id}' has no 'kind'.", node.Id));
            }
            else if (this.registry.TryGet(node.Kind) is not { } actionType)
            {
                errors.Add(new WorkflowValidationError(
                    "node_unknown_kind",
                    $"Node '{node.Id}' uses unknown kind '{node.Kind}'.",
                    node.Id));
            }
            else
            {
                // Config shape/type check — mirror the dispatch-time deserialize so a bad config is
                // rejected at save time, not on the first run.
                ValidateNodeConfig(node, actionType, errors);
            }
        }

        // Edges: validate refs and ports + reject duplicates on (fromNodeId, fromPort).
        // Single-port-per-step engine fires exactly one successor per port; a second edge sharing
        // the same source port would be silently ignored at runtime, which is a footgun. Better
        // to reject at save time.
        var seenPortPairs = new HashSet<(string NodeId, string Port)>();
        for (var i = 0; i < graph.Edges.Count; i++)
        {
            var edge = graph.Edges[i];
            var path = $"edge[{i}]";
            var fromNodeId = edge.From?.NodeId;
            var fromPort = edge.From?.Port;
            var to = edge.To;

            if (string.IsNullOrEmpty(fromNodeId) || string.IsNullOrEmpty(fromPort) || string.IsNullOrEmpty(to))
            {
                errors.Add(new WorkflowValidationError("edge_malformed", "Edge missing from.nodeId / from.port / to.", path));
                continue;
            }
            if (!nodesById.TryGetValue(fromNodeId, out var fromNode))
            {
                errors.Add(new WorkflowValidationError("edge_from_missing", $"Edge source node '{fromNodeId}' not found.", path));
                continue;
            }
            if (!nodesById.ContainsKey(to))
            {
                errors.Add(new WorkflowValidationError("edge_to_missing", $"Edge target node '{to}' not found.", path));
                continue;
            }
            if (this.registry.GetPort(fromNode.Kind, fromPort) is null)
            {
                errors.Add(new WorkflowValidationError(
                    "edge_unknown_port",
                    $"Node '{fromNodeId}' ({fromNode.Kind}) has no port '{fromPort}'.",
                    path));
                continue;
            }

            if (!seenPortPairs.Add((fromNodeId, fromPort)))
            {
                errors.Add(new WorkflowValidationError(
                    "edge_duplicate_port",
                    $"Duplicate edge from node '{fromNodeId}' port '{fromPort}' — only one successor allowed per port.",
                    path));
            }
        }

        // Start node required + must exist. We have exactly one entry point per graph
        // (single-port-per-step engine = strictly linear runs from a single start; multi-start
        // would silently fan out without explicit fan-out, which we forbid by design).
        if (graph.Nodes.Count > 0)
        {
            if (string.IsNullOrEmpty(graph.StartNodeId))
            {
                errors.Add(new WorkflowValidationError(
                    "no_start_node",
                    "Workflow has nodes but no start node — pick one to begin the run."));
            }
            else if (!nodesById.ContainsKey(graph.StartNodeId))
            {
                errors.Add(new WorkflowValidationError(
                    "start_node_missing",
                    $"Start node '{graph.StartNodeId}' not in nodes.",
                    graph.StartNodeId));
            }
        }

        // No cycle detection on purpose — loop actions (ForEach) wire the body's tail edge back to
        // the loop node by design. The engine guards against runaway cycles via MaxVisitsPerNode
        // and MaxStepsPerRun (see WorkflowEngineSettings); both abort the run with a clear reason
        // before the database fills up.

        return errors;
    }

    /// <summary>
    /// Structural type-check of a node's stored <c>config</c> against its action's
    /// <see cref="IActionType.ConfigType"/>: deserializes with the SAME options the resolver uses at
    /// dispatch (<see cref="WorkflowJsonOptions.Default"/>), so a shape / type mismatch — an
    /// <c>Expr&lt;T&gt;</c> field sent as a bare literal instead of the <c>{ engine, value }</c> wrapper,
    /// a bad enum, a wrong primitive — is caught at save time instead of blowing up the first run. Only
    /// the STRUCTURE is checked: expressions are NOT evaluated (no model / engines here), so this never
    /// rejects a config the runtime would accept, and never runs author-supplied Liquid / JS.
    /// </summary>
    private static void ValidateNodeConfig(
        WorkflowNode node, IActionType actionType, List<WorkflowValidationError> errors)
    {
        // No config object → nothing structural to check (a required-but-missing field is a runtime
        // concern, not a shape error). An absent config is Undefined; an explicit JSON null is Null.
        if (node.Config.ValueKind is not JsonValueKind.Object)
        {
            return;
        }

        try
        {
            _ = node.Config.Deserialize(actionType.ConfigType, WorkflowJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            errors.Add(new WorkflowValidationError(
                "node_config_invalid",
                $"Node '{node.Id}' ({node.Kind}) has an invalid config: {ex.Message}",
                node.Id));
        }
    }
}
