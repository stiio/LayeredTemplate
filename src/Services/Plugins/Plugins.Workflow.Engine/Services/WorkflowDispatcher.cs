using System.Diagnostics;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Default <see cref="IWorkflowDispatcher"/>. Looks up the definition, builds the run via
/// <see cref="IWorkflowRunner"/>, and (by default) flushes the plugin's own <c>IWorkflowStore</c>
/// so the caller's app-side <c>SaveChanges</c> doesn't need to know anything about workflow state
/// (and couldn't reach it any more — separate DbContext now). Engine-internal callers opt out of
/// the flush to stage the child atomically with their own step transition — see
/// <see cref="IWorkflowDispatcher.DispatchAsync"/>.
/// <para>
/// Also enforces <see cref="WorkflowEngineSettings.MaxNestingLevel"/> for sub-workflow
/// dispatches so a runaway recursive RunWorkflow chain can't keep starting new runs.
/// </para>
/// </summary>
internal class WorkflowDispatcher : IWorkflowDispatcher
{
    private readonly IWorkflowStore store;
    private readonly IWorkflowRunner runner;
    private readonly WorkflowEngineSettings settings;

    public WorkflowDispatcher(
        IWorkflowStore store,
        IWorkflowRunner runner,
        IOptions<WorkflowEngineSettings> settings)
    {
        this.store = store;
        this.runner = runner;
        this.settings = settings.Value;
    }

    public async Task<WorkflowDispatchResult> DispatchAsync(
        WorkflowDispatchRequest request, CancellationToken cancellationToken, bool flush = true)
    {
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.run.dispatch", ActivityKind.Internal);
        activity?.SetTag(WorkflowTags.TenantId, request.TenantId);
        activity?.SetTag(WorkflowTags.OwnerKind, request.OwnerKind);
        activity?.SetTag(WorkflowTags.TriggerKind, request.TriggerKind);
        activity?.SetTag(WorkflowTags.NestingLevel, request.NestingLevel);
        activity?.SetTag(WorkflowTags.IsDryRun, request.IsDryRun);

        // Cap first — cheaper than the definition lookup and matches the engine-level
        // "won't start" signal so callers don't burn DB roundtrips on doomed dispatches.
        if (request.NestingLevel > this.settings.MaxNestingLevel)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowDispatchOutcome.NestingLimitExceeded));
            return WorkflowDispatchResult.NestingLimitExceeded();
        }

        // Per-parent fan-out cap. Only relevant for sub-dispatches (top-level form / API
        // dispatches have no ParentRunId, so this never fires for them). One indexed COUNT —
        // see EfCoreWorkflowStore.CountChildRunsAsync for why local-overlay isn't needed.
        if (request.ParentRunId is { } parentId)
        {
            var existing = await this.store.CountChildRunsAsync(parentId, cancellationToken);
            if (existing >= this.settings.MaxSubRunsPerRun)
            {
                activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowDispatchOutcome.SubRunLimitExceeded));
                return WorkflowDispatchResult.SubRunLimitExceeded();
            }
        }

        // Definition-tenancy ≠ run-tenancy (ADR-028): the lookup uses OwnerTenantId when supplied
        // (system workflows live under a sentinel tenant), but the run below is still built from
        // `request` and so created under the REAL TenantId. OwnerTenantId defaults to TenantId, so
        // ordinary callers (null) resolve the definition in the same tenant they always did.
        var definition = await this.store.FindDefinitionAsync(
            request.OwnerTenantId ?? request.TenantId,
            request.OwnerKind,
            request.OwnerId,
            request.TriggerKind,
            cancellationToken);

        if (definition is null)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowDispatchOutcome.NotConfigured));
            return WorkflowDispatchResult.NotConfigured();
        }

        var intent = new WorkflowStartIntent
        {
            TenantId = request.TenantId,
            TriggerKind = request.TriggerKind,
            TriggerSourceKind = request.TriggerSourceKind,
            TriggerSourceId = request.TriggerSourceId,
            ActorUserId = request.ActorUserId,
            IsDryRun = request.IsDryRun,
            Variables = request.Variables,
            NestingLevel = request.NestingLevel,
            ParentRunId = request.ParentRunId,
            ParentStepId = request.ParentStepId,
        };

        var run = await this.runner.StartAsync(intent, definition, cancellationToken);
        if (run is null)
        {
            // Definition exists but produced no start steps (empty graph or dangling start ids).
            // Nothing to commit — store has no staged changes from a no-op Start.
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowDispatchOutcome.EmptyGraph));
            return WorkflowDispatchResult.EmptyGraph();
        }

        // Plugin owns its DbContext: flushing here doesn't touch any consumer transaction.
        // Engine-internal callers (RunWorkflow) pass flush:false — their child run stays staged
        // on the dispatching step's scoped store and commits atomically with that step's own
        // transition in the per-step flush (see IWorkflowDispatcher.DispatchAsync remarks).
        if (flush)
        {
            await this.store.SaveChangesAsync(cancellationToken);
        }
        activity?.SetTag(WorkflowTags.RunId, run.Id);
        activity?.SetTag(WorkflowTags.DefinitionId, definition.Id);
        activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowDispatchOutcome.Started));
        return WorkflowDispatchResult.StartedAt(run.Id, run.Status);
    }
}
