using System.Text.Json;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Graph;

/// <summary>
/// Typed projection of a stored workflow graph (the JSON object that lives at
/// <c>form.settings.workflow</c> and is later snapshotted into <c>workflow_runs.workflow_snapshot</c>).
/// All public consumers — validator, starter, engine worker — work on this POCO; only the
/// per-node <see cref="WorkflowNode.Config"/> stays as <see cref="JsonElement"/> because each
/// action <c>kind</c> defines its own config shape (resolved later via <c>IExpressionResolver</c>).
/// </summary>
public class WorkflowGraph
{
    public List<WorkflowNode> Nodes { get; set; } = new();

    public List<WorkflowEdge> Edges { get; set; } = new();

    /// <summary>
    /// Single entry point. Engine creates exactly one initial step from this node when the run
    /// starts. Multi-start was intentionally removed: it conflicted with the single-port-per-step
    /// philosophy (silent parallel pipelines without explicit fan-out). Authors who need branching
    /// at the very start use a single start node + Switch / Condition.
    /// </summary>
    public string? StartNodeId { get; set; }

    /// <summary>
    /// Author's free-text notes about the workflow as a whole. Purely informational, like
    /// <see cref="WorkflowNode.Description"/>. Lives in the graph document rather than on the
    /// definition row so it versions and snapshots together with the graph it describes (the
    /// definition-level <c>DisplayName</c> remains the queryable picker label).
    /// </summary>
    public string? Description { get; set; }
}

public class WorkflowNode
{
    public string Id { get; set; } = string.Empty;

    /// <summary>User-facing slug, unique within the graph. Used in Liquid/JS as <c>steps.&lt;key&gt;.*</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Action kind id — matches <c>IActionType.Kind</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public string? Name { get; set; }

    /// <summary>
    /// Author's free-text notes ("what this node does and why"). Purely informational — engine,
    /// validator and expressions never read it. Must be declared on the POCO (not just in the
    /// editor): the store re-serializes <see cref="WorkflowGraph"/> on save, so any field the
    /// editor sends but the model doesn't declare is silently dropped.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>Action-specific config (heterogeneous by <see cref="Kind"/>; resolved against the run model).</summary>
    public JsonElement Config { get; set; }

    public WorkflowNodePosition? Position { get; set; }
}

public class WorkflowNodePosition
{
    public double X { get; set; }

    public double Y { get; set; }
}

public class WorkflowEdge
{
    public WorkflowEdgeFrom From { get; set; } = new();

    public string To { get; set; } = string.Empty;
}

public class WorkflowEdgeFrom
{
    public string NodeId { get; set; } = string.Empty;

    public string Port { get; set; } = string.Empty;
}
