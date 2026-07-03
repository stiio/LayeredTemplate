using System.Diagnostics;
using LayeredTemplate.Plugins.Workflow.Abstractions.Telemetry;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Process-wide <see cref="ActivitySource"/> for engine instrumentation. Singleton instance
/// keyed by <see cref="WorkflowTelemetry.ActivitySourceName"/>. <c>StartActivity</c> returns
/// null when no listener is registered (zero-overhead fast path).
/// </summary>
internal static class WorkflowActivitySource
{
    public static readonly ActivitySource Instance = new(WorkflowTelemetry.ActivitySourceName);
}

/// <summary>
/// Tag keys used across engine spans. Stable identifiers consumers can build dashboards / alerts
/// against — keep changes additive (deprecate, don't rename).
/// </summary>
internal static class WorkflowTags
{
    // Identity
    public const string TenantId = "workflow.tenant_id";
    public const string RunId = "workflow.run_id";
    public const string StepId = "workflow.step_id";
    public const string DefinitionId = "workflow.definition_id";
    public const string OldRunId = "workflow.old_run_id";
    public const string NewRunId = "workflow.new_run_id";

    // Run / step shape
    public const string OwnerKind = "workflow.owner_kind";
    public const string TriggerKind = "workflow.trigger_kind";
    public const string TriggerSourceKind = "workflow.trigger_source_kind";
    public const string TriggerSourceId = "workflow.trigger_source_id";
    public const string NestingLevel = "workflow.nesting_level";
    public const string IsDryRun = "workflow.is_dry_run";

    public const string StepKind = "workflow.step.kind";
    public const string StepNodeKey = "workflow.step.node_key";
    public const string StepAttempt = "workflow.step.attempt";
    public const string StepOutputPort = "workflow.step.output_port";

    /// <summary>"fast" | "long" — which worker pool processed the step (or "any" in single-pool mode).</summary>
    public const string StepLane = "workflow.step.lane";

    // Action invocation
    public const string ActionKind = "workflow.action.kind";
    public const string ActionResultType = "workflow.action.result_type";

    // Fan-out
    public const string FanOutFiredPort = "workflow.fanout.fired_port";
    public const string FanOutTargetNodeId = "workflow.fanout.target_node_id";
    public const string FanOutFromStepId = "workflow.fanout.from_step_id";

    // Run completion
    public const string RunStatusBefore = "workflow.run.status_before";
    public const string RunStatusAfter = "workflow.run.status_after";
    public const string RunBecameTerminal = "workflow.run.became_terminal";

    // Outcomes (string form of *Outcome enums)
    public const string Outcome = "workflow.outcome";

    // Resume / cancel
    public const string ResumePort = "workflow.resume.port";

    // Restart
    public const string RestartMode = "workflow.restart.mode";

    // Retention
    public const string RetentionFinishedPurged = "workflow.retention.finished_purged";
    public const string RetentionStaleFailed = "workflow.retention.stale_failed";
}
