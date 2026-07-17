using System.Diagnostics;
using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Default <see cref="IWorkflowResumer"/>. Glues together the store (atomic transition), the
/// action-type registry (dispatch + port validation), and the fan-out service (successor enqueue
/// + run completion check). Stays trigger- and tenant-agnostic: callers tell it which tenant
/// they're authorised for; the resumer rejects mismatches up-front.
/// <para>
/// ADR-027: after winning the atomic Waiting-guard (the "you are the one true resumer" choke
/// point — C2), the resumer resolves the step's <see cref="IActionType"/> and calls
/// <c>OnStepResumedAsync</c> so the action owns the wake-up decision (which port fires, what gets
/// stamped). The guard call seeds the row with the caller-supplied port + normalized payload; the
/// action's returned result is then re-validated + re-stamped — in slice A every suspending action
/// echoes the same port + payload, so the row is unchanged either way (zero behavior change).
/// </para>
/// </summary>
internal class WorkflowResumer : IWorkflowResumer
{
    private readonly IWorkflowStore store;
    private readonly IWorkflowFanOut fanOut;
    private readonly IActionTypeRegistry registry;
    private readonly IExpressionResolver resolver;
    private readonly ILogger<WorkflowResumer> logger;

    public WorkflowResumer(
        IWorkflowStore store,
        IWorkflowFanOut fanOut,
        IActionTypeRegistry registry,
        IExpressionResolver resolver,
        ILogger<WorkflowResumer> logger)
    {
        this.store = store;
        this.fanOut = fanOut;
        this.registry = registry;
        this.resolver = resolver;
        this.logger = logger;
    }

    public async Task<WorkflowResumeResult> ResumeAsync(
        WorkflowResumeCommand command, CancellationToken cancellationToken)
    {
        // Carry the full resume identity through the call. Engine-internal callers (FanOut auto-
        // resume) and external callers (HTTP resume API) both flow through here, so a single
        // scope shape covers both.
        using var scope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["RunId"] = command.RunId,
            ["StepId"] = command.StepId,
            ["TenantId"] = command.TenantId,
            ["Port"] = command.Port,
        });
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.run.resume", ActivityKind.Internal);
        activity?.SetTag(WorkflowTags.RunId, command.RunId);
        activity?.SetTag(WorkflowTags.StepId, command.StepId);
        activity?.SetTag(WorkflowTags.TenantId, command.TenantId);
        activity?.SetTag(WorkflowTags.ResumePort, command.Port);

        if (string.IsNullOrWhiteSpace(command.Port))
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowResumeFailureReason.InvalidPort));
            return WorkflowResumeResult.Failure(
                WorkflowResumeFailureReason.InvalidPort,
                "Output port is required.");
        }

        var run = await this.store.GetRunAsync(command.RunId, cancellationToken);
        // Tenant mismatch is reported as RunNotFound so the API doesn't leak existence across
        // tenants. Same treatment as a missing row.
        if (run is null || run.TenantId != command.TenantId)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowResumeFailureReason.RunNotFound));
            return WorkflowResumeResult.Failure(
                WorkflowResumeFailureReason.RunNotFound,
                $"Run '{command.RunId}' not found.");
        }

        var step = await this.store.GetStepAsync(command.StepId, cancellationToken);
        if (step is null || step.RunId != run.Id)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowResumeFailureReason.StepNotFound));
            return WorkflowResumeResult.Failure(
                WorkflowResumeFailureReason.StepNotFound,
                $"Step '{command.StepId}' not found in run '{command.RunId}'.");
        }

        if (step.Status != StepExecutionStatus.Waiting)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowResumeFailureReason.StepNotWaiting));
            return WorkflowResumeResult.Failure(
                WorkflowResumeFailureReason.StepNotWaiting,
                $"Step is not waiting (current status: '{step.Status}'). It may have been resumed already, timed out, or never suspended.");
        }

        // Pre-guard port validation of the CALLER-supplied port. Kept before the guard so an
        // unknown port surfaces InvalidPort with the step still Waiting (retryable) — exactly the
        // pre-ADR-027 behavior. Every slice-A action echoes this port verbatim (pass-through) or
        // returns a known-valid fixed port, so the action's chosen port is always valid too.
        var portMeta = this.registry.GetPort(step.Kind, command.Port);
        if (portMeta is null)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowResumeFailureReason.InvalidPort));
            return WorkflowResumeResult.Failure(
                WorkflowResumeFailureReason.InvalidPort,
                $"Port '{command.Port}' is not declared by action '{step.Kind}'.");
        }

        // Resolve the action + materialise transient config fields BEFORE the guard. Placement
        // is the point: a failing transient expression must surface as a clean retryable
        // Failure with the step still Waiting and NOTHING staged. Doing this after the guard
        // couldn't fail safely in the ambient-transaction chain (child terminal → parent
        // auto-resume): throwing would roll back the child's committed outcome and re-run its
        // action (side effects included), while returning a failure would commit a half-resumed
        // step. By the time a step waits its transient fields have already resolved once (at
        // execute), so a failure here is almost always environmental — the caller retries; the
        // wait timeout remains the dead-letter backstop for the persistent case.
        var actionType = this.registry.TryGet(step.Kind);
        if (actionType is null)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowResumeFailureReason.InvalidPort));
            return WorkflowResumeResult.Failure(
                WorkflowResumeFailureReason.InvalidPort,
                $"Action '{step.Kind}' is not registered.");
        }

        var configObj = DeserializeConfig(step, actionType);
        try
        {
            await this.resolver.ResolveTransientAsync(
                configObj,
                () => ExpressionModelBuilder.Build(run.StaticContext, run.StepsOutputs),
                ExpressionModelBuilder.EvaluationContextForRun(run),
                cancellationToken);
        }
        catch (ExpressionResolutionException ex)
        {
            this.logger.LogWarning(
                ex,
                "Transient config resolution failed while resuming step {StepId} ({Kind}); step stays Waiting.",
                step.Id, step.Kind);
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowResumeFailureReason.ConfigResolutionFailed));
            return WorkflowResumeResult.Failure(
                WorkflowResumeFailureReason.ConfigResolutionFailed,
                $"Transient config resolution failed: {ex.Message}");
        }

        // One storage transaction for everything past this point: the atomic Waiting-guard, the
        // action's wake-up hook, the successor fan-out, and the run-completion check commit
        // together. Without it the guard auto-committed on its own, so a crash — or a post-guard
        // failure such as the action throwing or returning an undeclared port — left the step
        // Completed with no output port and no successor staged, wedging the run forever. Now
        // every exit short of the commit below disposes the transaction uncommitted, the guard
        // rolls back, and the step stays Waiting — retryable.
        //
        // Null = an ambient transaction is already open on this scope's store: the chain-unwind
        // case (child run terminal → parent auto-resume → parent run terminal → grandparent
        // auto-resume). We participate in the outer transaction — stage + flush — and the
        // outermost resume commits the whole chain atomically. (Caveat: inside an ambient
        // transaction a post-guard defensive failure can't be rolled back in isolation; that
        // rare path keeps its pre-transaction wedge behaviour and is logged loud by the caller.)
        await using var transaction = await this.store.BeginTransactionAsync(cancellationToken);

        // C2 — win the atomic Waiting-guard FIRST. The store flips Waiting → Completed only if it's
        // still Waiting, seeding the row with the caller's port + normalized payload. Winning this
        // UPDATE is what makes us the one true resumer; OnStepResumed runs strictly after.
        var seedOutputs = NormalizeOutputs(command.Payload);
        var resumed = await this.store.TryResumeWaitingStepAsync(
            command.StepId,
            command.Port,
            seedOutputs,
            cancellationToken);
        if (resumed is null)
        {
            // Lost the race against another resume / the timeout sweeper. Step is now in some
            // non-Waiting status; caller decides whether to retry or surface 409.
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowResumeFailureReason.ConcurrencyConflict));
            return WorkflowResumeResult.Failure(
                WorkflowResumeFailureReason.ConcurrencyConflict,
                "Step is no longer waiting — it was resumed or expired by another process.");
        }

        // Guard won → let the action own the wake-up decision (which port fires). The action
        // type and its config (transient fields included) were prepared before the guard.
        var actionContext = await this.BuildContextAsync(run, step, configObj, cancellationToken);
        var result = await actionType.OnStepResumedAsync(
            actionContext, command.Payload, command.Port, cancellationToken);

        // Validate the action's chosen port (ADR-027 §3) — defense in depth. In slice A this equals
        // the already-validated caller port (pass-through) or a fixed declared port, so it always
        // passes; kept so a future action that returns an undeclared port fails loud. Failing here
        // disposes the transaction uncommitted: the guard's flip rolls back and the step stays
        // Waiting instead of wedging Completed-without-port.
        if (string.IsNullOrWhiteSpace(result.OutputPort)
            || this.registry.GetPort(step.Kind, result.OutputPort) is null)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowResumeFailureReason.InvalidPort));
            return WorkflowResumeResult.Failure(
                WorkflowResumeFailureReason.InvalidPort,
                $"Port '{result.OutputPort}' returned by action '{step.Kind}' is not one of its declared ports.");
        }

        // The guard already stamped the row with the caller's port + normalized payload. In slice A
        // every suspending action echoes that SAME port (pass-through) or returns a fixed port equal
        // to it, and echoes the SAME payload — so the seeded row is already correct. Re-stamp the
        // fired port back onto the record (a value-preserving write in slice A) so the record handed
        // to fan-out carries the action-chosen port; the seeded outputs are kept verbatim, which
        // preserves the byte-exact normalization the store applied (flattened object / {value:…}
        // wrapper for scalars). When a later slice makes an action diverge the port, this is the
        // single place it takes effect — re-stamp via UpdateStep + the eventual SaveChanges (C3).
        var firedPort = result.OutputPort!;
        if (!string.Equals(resumed.OutputPort, firedPort, StringComparison.Ordinal))
        {
            resumed.OutputPort = firedPort;
            this.store.UpdateStep(resumed);
        }

        await this.fanOut.EnqueueNextStepAsync(resumed, firedPort, cancellationToken);
        await this.fanOut.CheckRunCompletionAsync(resumed, cancellationToken);
        activity?.SetTag(WorkflowTags.Outcome, "Success");

        // Flush inside the transaction (or the ambient one when nested). This also flushes
        // whatever the caller staged on the shared scope before resuming — deliberate: in the
        // worker path the child run's terminal transition and its parent's resume land in the
        // SAME commit, so no observer can ever see a terminal child with an un-resumed parent.
        await this.store.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return WorkflowResumeResult.Success();
    }

    /// <summary>
    /// Deserialize the step's persisted resolved config to the action's typed POCO, falling
    /// back to a default instance on missing/bad JSON — the resume body of slice-A actions
    /// doesn't read config, but a future override might. Transient fields inside come back
    /// unresolved by construction; the caller materialises them (pre-guard) before use.
    /// </summary>
    private static object DeserializeConfig(WorkflowStepRecord step, IActionType actionType)
    {
        try
        {
            return step.ResolvedConfig.Deserialize(actionType.ConfigType, WorkflowJsonOptions.Default)
                ?? Activator.CreateInstance(actionType.ConfigType)!;
        }
        catch (JsonException)
        {
            return Activator.CreateInstance(actionType.ConfigType)!;
        }
    }

    /// <summary>
    /// Build the <see cref="ActionContext"/> the resume hook sees — the same shape the worker hands
    /// <c>ExecuteAsync</c> / the timeout sweep hands <c>OnStepTimedOutAsync</c>. Config arrives
    /// pre-deserialized (and transient-resolved) from the pre-guard phase of
    /// <see cref="ResumeAsync"/>. Node-key + steps-outputs are populated so a state-aware resume
    /// can read prior outputs, mirroring the execute path.
    /// </summary>
    private async Task<ActionContext> BuildContextAsync(
        WorkflowRunRecord run,
        WorkflowStepRecord step,
        object configObj,
        CancellationToken cancellationToken)
    {
        var graph = await this.fanOut.GetGraphAsync(run, cancellationToken);
        var node = graph?.Nodes.FirstOrDefault(n => n.Id == step.NodeId);
        var nodeKey = string.IsNullOrWhiteSpace(node?.Key) ? step.NodeId : node.Key;

        return new ActionContext
        {
            Config = configObj,
            RunId = step.RunId,
            StepExecutionId = step.Id,
            TenantId = run.TenantId,
            DefinitionId = run.DefinitionId,
            ActorUserId = run.ActorUserId,
            TriggerSourceKind = run.TriggerSourceKind,
            TriggerSourceId = run.TriggerSourceId,
            IsDryRun = run.IsDryRun,
            NodeKey = nodeKey,
            StepsOutputs = run.StepsOutputs,
            // For a resume hook these are the suspend-time initial outputs — same channel as
            // the worker's retry checkpoint.
            PriorAttemptOutputs = step.Outputs,
            AttemptCount = step.AttemptCount,
        };
    }

    /// <summary>
    /// Convert the user-supplied <see cref="JsonElement"/> outputs into the dict shape the store
    /// persists. Objects flatten naturally so authors can read <c>steps.&lt;key&gt;.fieldName</c>;
    /// arrays / scalars / null get stuffed under a single <c>value</c> key so non-object payloads
    /// aren't silently dropped (writing the raw scalar would break the dict-of-keys contract that
    /// downstream <c>steps.*</c> traversal assumes).
    /// </summary>
    private static IReadOnlyDictionary<string, object?>? NormalizeOutputs(JsonElement? outputs)
    {
        if (outputs is not { } el) return null;

        if (el.ValueKind == JsonValueKind.Object)
        {
            return el.EnumerateObject()
                .ToDictionary(p => p.Name, p => ExpressionModelBuilder.JsonElementToClr(p.Value));
        }

        // Non-object payload (array / scalar / null / undefined) — preserve the value under a
        // sentinel key. JsonElementToClr handles all the kinds we care about.
        return new Dictionary<string, object?>
        {
            ["value"] = ExpressionModelBuilder.JsonElementToClr(el),
        };
    }
}
