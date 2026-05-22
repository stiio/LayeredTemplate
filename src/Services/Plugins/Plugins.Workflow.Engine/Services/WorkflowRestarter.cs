using System.Diagnostics;
using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.Logging;
using PluginWorkflowDefinition = LayeredTemplate.Plugins.Workflow.Abstractions.Models.WorkflowDefinition;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Default <see cref="IWorkflowRestarter"/>. Loads the old run, resolves the graph via the
/// chosen <see cref="WorkflowRestartMode"/>, reconstructs a <see cref="WorkflowStartIntent"/>
/// from the frozen <c>StaticContext</c>, and hands off to <see cref="IWorkflowRunner"/> to
/// stage a fresh run. The old run is never mutated.
/// </summary>
internal class WorkflowRestarter : IWorkflowRestarter
{
    private readonly IWorkflowStore store;
    private readonly IWorkflowRunner runner;
    private readonly ILogger<WorkflowRestarter> logger;

    public WorkflowRestarter(
        IWorkflowStore store,
        IWorkflowRunner runner,
        ILogger<WorkflowRestarter> logger)
    {
        this.store = store;
        this.runner = runner;
        this.logger = logger;
    }

    public async Task<WorkflowRestartResult> RestartAsync(
        WorkflowRestartCommand command, CancellationToken cancellationToken)
    {
        using var scope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["OldRunId"] = command.RunId,
            ["TenantId"] = command.TenantId,
            ["Mode"] = command.Mode,
        });
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.run.restart", ActivityKind.Internal);
        activity?.SetTag(WorkflowTags.OldRunId, command.RunId);
        activity?.SetTag(WorkflowTags.TenantId, command.TenantId);
        activity?.SetTag(WorkflowTags.RestartMode, command.Mode.ToString());

        var oldRun = await this.store.GetRunAsync(command.RunId, cancellationToken);
        // Tenant mismatch reported as NotFound — same convention as Resumer/Canceller.
        if (oldRun is null || oldRun.TenantId != command.TenantId)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowRestartOutcome.NotFound));
            return WorkflowRestartResult.NotFound();
        }

        // Resolve the graph + a synthetic WorkflowDefinition wrapper. Runner only reads
        // .Id and .Graph, so the other fields are populated for completeness but don't drive
        // behaviour.
        PluginWorkflowDefinition? definition;
        if (command.Mode == WorkflowRestartMode.UseSnapshot)
        {
            WorkflowGraph? snapshotGraph;
            try
            {
                snapshotGraph = JsonSerializer.Deserialize<WorkflowGraph>(oldRun.WorkflowSnapshot, WorkflowJsonOptions.Default);
            }
            catch (JsonException ex)
            {
                this.logger.LogError(ex, "Restart: workflow_snapshot of old run is not valid JSON");
                activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowRestartOutcome.SnapshotMalformed));
                return WorkflowRestartResult.SnapshotMalformed(oldRun.Id);
            }
            if (snapshotGraph is null)
            {
                activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowRestartOutcome.SnapshotMalformed));
                return WorkflowRestartResult.SnapshotMalformed(oldRun.Id);
            }

            definition = new PluginWorkflowDefinition
            {
                Id = oldRun.DefinitionId,
                TenantId = oldRun.TenantId,
                OwnerKind = string.Empty,   // Runner doesn't read these — frozen snapshot path
                OwnerId = null,
                TriggerKind = oldRun.TriggerKind,
                Graph = snapshotGraph,
            };
        }
        else
        {
            definition = await this.store.GetDefinitionByIdAsync(oldRun.DefinitionId, cancellationToken);
            if (definition is null)
            {
                activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowRestartOutcome.DefinitionGone));
                return WorkflowRestartResult.DefinitionGone(oldRun.Id);
            }
        }

        var variables = ExtractVariables(oldRun.StaticContext);
        var intent = new WorkflowStartIntent
        {
            TenantId = oldRun.TenantId,
            TriggerKind = oldRun.TriggerKind,
            TriggerSourceKind = oldRun.TriggerSourceKind,
            TriggerSourceId = oldRun.TriggerSourceId,
            // Caller's actor wins; fall back to the original run's actor for unbroken audit
            // when the caller doesn't have a user id handy (typical App handler path).
            ActorUserId = command.ActorUserId ?? oldRun.ActorUserId,
            IsDryRun = oldRun.IsDryRun,
            Variables = variables,
            // Top-level — restart never re-attaches to a parent, even if old run was a child.
            NestingLevel = 0,
            ParentRunId = null,
            ParentStepId = null,
        };

        var newRun = await this.runner.StartAsync(intent, definition, cancellationToken);
        if (newRun is null)
        {
            // Graph parsed but had no nodes / no start node — treat as EmptyGraph for the same
            // reason the dispatcher does.
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowRestartOutcome.EmptyGraph));
            return WorkflowRestartResult.EmptyGraph(oldRun.Id);
        }

        // Plugin owns its DbContext; flush so restart is a self-contained unit of work.
        await this.store.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation(
            "Run {OldRunId} restarted as {NewRunId} (mode={Mode})",
            oldRun.Id, newRun.Id, command.Mode);

        activity?.SetTag(WorkflowTags.NewRunId, newRun.Id);
        activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowRestartOutcome.Started));
        return WorkflowRestartResult.StartedAt(oldRun.Id, newRun.Id);
    }

    /// <summary>
    /// Pulls the original <c>vars</c> sub-object out of <c>StaticContext</c>. The Runner re-emits
    /// <c>trigger</c> from the new intent, so we ignore that namespace entirely and only re-feed
    /// consumer-supplied variables. Missing / wrong-shaped <c>vars</c> falls back to an empty
    /// object — the restart still proceeds (workflow may behave differently with no vars, but at
    /// least won't crash on parsing).
    /// </summary>
    private static JsonElement ExtractVariables(JsonElement staticContext)
    {
        if (staticContext.ValueKind != JsonValueKind.Object)
        {
            return JsonSerializer.SerializeToElement(new { }, WorkflowJsonOptions.Default);
        }

        if (!staticContext.TryGetProperty("vars", out var varsEl)
            || varsEl.ValueKind != JsonValueKind.Object)
        {
            // Either pre-namespace static_context (no longer supported in dev) or someone
            // wrote a non-object under vars. Fail soft to empty rather than throw.
            return JsonSerializer.SerializeToElement(new { }, WorkflowJsonOptions.Default);
        }

        // Clone so the returned element doesn't share a buffer with the input — caller might be
        // looking at a record whose underlying JsonDocument has different lifetime expectations.
        return varsEl.Clone();
    }
}
