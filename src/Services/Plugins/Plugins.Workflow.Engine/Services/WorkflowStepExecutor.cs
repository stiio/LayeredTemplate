using System.Diagnostics;
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
/// Per-scope step dispatch pipeline: loads the step's run, deserializes the resolved config,
/// materialises transient expression fields, invokes the <see cref="IActionType"/>, and lands
/// the <see cref="ActionExecutionResult"/> on the step via <see cref="ApplyResultAsync"/>
/// (retry / dead-letter / suspend / terminate state machine + fan-out).
/// <para>
/// Division of labour with <see cref="WorkflowEngineWorker"/>: the worker owns claiming,
/// DI-scope-per-step lifetimes, lane cancellation budgets and flushing; this class owns
/// everything that happens to ONE step inside such a scope. All dependencies are scoped —
/// resolve a fresh executor per step, never cache one across scopes. Mutations are staged on
/// the scoped <see cref="IWorkflowStore"/> without flushing; the caller decides when (and
/// whether) to <c>SaveChangesAsync</c>.
/// </para>
/// </summary>
internal class WorkflowStepExecutor
{
    private readonly IWorkflowStore store;
    private readonly IActionTypeRegistry registry;
    private readonly IWorkflowFanOut fanOut;

    /// <summary>
    /// Execute-time half of the two-phase expression resolution — materialises transient config
    /// fields (secrets / heavy payloads) just before the action runs. For the common config with
    /// no transient leaf the resolve call is a cached-reflection no-op, so it runs unconditionally.
    /// </summary>
    private readonly IExpressionResolver resolver;

    private readonly WorkflowEngineSettings settings;
    private readonly ILogger<WorkflowStepExecutor> logger;

    public WorkflowStepExecutor(
        IWorkflowStore store,
        IActionTypeRegistry registry,
        IWorkflowFanOut fanOut,
        IExpressionResolver resolver,
        IOptions<WorkflowEngineSettings> settings,
        ILogger<WorkflowStepExecutor> logger)
    {
        this.store = store;
        this.registry = registry;
        this.fanOut = fanOut;
        this.resolver = resolver;
        this.settings = settings.Value;
        this.logger = logger;
    }

    /// <summary>
    /// Dispatches one claimed step through its action and stages the outcome on the scoped
    /// store. <paramref name="lane"/> only labels telemetry — the lane's cancellation budget
    /// is the caller's job (it arrives pre-baked in <paramref name="ct"/>).
    /// </summary>
    internal async Task ExecuteAsync(
        WorkflowStepRecord step,
        WorkflowStepLane lane,
        CancellationToken ct)
    {
        // Outer scope covers everything we know up-front (step + tenant). Inner scope (after the
        // run loads) adds run-level fields. Serilog enrichers / Seq pick these up automatically;
        // every log line below carries the structured fields without per-call repetition.
        using var stepScope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["StepId"] = step.Id,
            ["RunId"] = step.RunId,
            ["TenantId"] = step.TenantId,
            ["Kind"] = step.Kind,
            ["AttemptCount"] = step.AttemptCount,
            ["Lane"] = lane.ToString(),
        });

        using var stepActivity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.step.execute", ActivityKind.Internal);
        stepActivity?.SetTag(WorkflowTags.RunId, step.RunId);
        stepActivity?.SetTag(WorkflowTags.StepId, step.Id);
        stepActivity?.SetTag(WorkflowTags.TenantId, step.TenantId);
        stepActivity?.SetTag(WorkflowTags.StepKind, step.Kind);
        stepActivity?.SetTag(WorkflowTags.StepAttempt, step.AttemptCount);
        stepActivity?.SetTag(WorkflowTags.StepLane, FormatLane(lane));

        var actionType = this.registry.TryGet(step.Kind);
        if (actionType is null)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Unknown action kind '{step.Kind}'.";
            step.CompletedAt = DateTime.UtcNow;
            this.store.UpdateStep(step);
            await this.fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        var run = await this.store.GetRunAsync(step.RunId, ct);
        if (run is null)
        {
            // Defensive: dispatching with no run means we'd hand the action a zero TenantId,
            // which custom actions could mistake for "no scoping". Refuse to run and dead-letter.
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Run {step.RunId} not found — refusing to dispatch step.";
            step.CompletedAt = DateTime.UtcNow;
            this.store.UpdateStep(step);
            return;
        }

        // Run already terminal — typically means an operator cancel or FailRun fired between
        // the claim SQL and our load here. Don't invoke the action: it may have side effects
        // (HTTP, email, DB write) that we shouldn't trigger on a closed run. Mark the step
        // dead-by-association and bail.
        if (run.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Run already terminal ({run.Status}); step skipped.";
            step.CompletedAt = DateTime.UtcNow;
            this.store.UpdateStep(step);
            return;
        }

        object configObj;
        try
        {
            // step.ResolvedConfig is JsonElement on the record; .Deserialize is the typed
            // overload, no string round-trip. Options must match StepExecutionBuilder's
            // serialize path (camelCase + enum-as-string) for the round-trip to be symmetric.
            configObj = step.ResolvedConfig.Deserialize(actionType.ConfigType, WorkflowJsonOptions.Default)
                ?? Activator.CreateInstance(actionType.ConfigType)!;
        }
        catch (Exception ex)
        {
            step.Status = StepExecutionStatus.Dead;
            step.LastError = $"Could not deserialize resolved config: {ex.Message}";
            step.CompletedAt = DateTime.UtcNow;
            this.store.UpdateStep(step);
            await this.fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        // Resolve node-key + steps_outputs snapshot so state-aware actions (ForEach, …) can read
        // their own previous outputs without a separate query. Graph is cached by FanOut for the
        // scope's lifetime — repeated calls within a batch hit the cache instead of re-parsing
        // the snapshot.
        var graph = await this.fanOut.GetGraphAsync(run, ct);
        var nodeKey = ResolveNodeKey(graph, step.NodeId);
        // run.StepsOutputs is JsonElement on the record — no per-step parse.
        var stepsOutputsJson = run.StepsOutputs;

        // Run-aware scope: layered on top of stepScope so action-side log calls carry both.
        using var runScope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["NodeKey"] = nodeKey,
            ["DefinitionId"] = run.DefinitionId,
            ["IsDryRun"] = run.IsDryRun,
            ["NestingLevel"] = run.NestingLevel,
        });
        stepActivity?.SetTag(WorkflowTags.StepNodeKey, nodeKey);
        stepActivity?.SetTag(WorkflowTags.DefinitionId, run.DefinitionId);
        stepActivity?.SetTag(WorkflowTags.IsDryRun, run.IsDryRun);
        stepActivity?.SetTag(WorkflowTags.NestingLevel, run.NestingLevel);

        var context = new ActionContext
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
            StepsOutputs = stepsOutputsJson,
            // Retry-checkpoint channel: whatever a previous attempt persisted (via OnError with
            // outputs) comes back to the action so it can skip already-completed side effects.
            PriorAttemptOutputs = step.Outputs,
            AttemptCount = step.AttemptCount,
        };

        ActionExecutionResult result;
        // Child span wraps the action invocation specifically — I/O latency (HTTP / S3 / DB
        // inside the action) is visible separately from the surrounding step plumbing.
        using (var actionActivity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.action.execute", ActivityKind.Internal))
        {
            actionActivity?.SetTag(WorkflowTags.ActionKind, step.Kind);
            actionActivity?.SetTag(WorkflowTags.StepLane, FormatLane(lane));
            try
            {
                // Late resolution of transient config fields (secrets / heavy payloads) —
                // deliberately left unresolved at enqueue and never persisted. Inside this
                // try on purpose: a resolution failure (secret-store blip, bad expression)
                // flows through the same catch as an action exception — transient error,
                // retry / dead-letter path. Model factory is only invoked when the config
                // actually has a transient leaf.
                await this.resolver.ResolveTransientAsync(
                    configObj,
                    () => ExpressionModelBuilder.Build(run.StaticContext, run.StepsOutputs),
                    ExpressionModelBuilder.EvaluationContextForRun(run),
                    ct);

                result = await actionType.ExecuteAsync(context, ct);
                actionActivity?.SetTag(WorkflowTags.ActionResultType, ClassifyResult(result));
                if (result.OutputPort is not null)
                {
                    actionActivity?.SetTag(WorkflowTags.StepOutputPort, result.OutputPort);
                }
                if (result.Error is not null)
                {
                    actionActivity?.SetStatus(ActivityStatusCode.Error, result.Error);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Lane timeout or shutdown drain — the outcome policy lives in the worker's
                // ProcessBatchAsync, which can see stoppingToken and distinguish the two
                // (timeout ⇒ count the attempt; shutdown ⇒ release the claim). Tag the span
                // while the action activity is still in scope, then let the cancellation
                // propagate.
                actionActivity?.SetStatus(ActivityStatusCode.Error, "Cancelled (lane timeout or shutdown drain)");
                actionActivity?.SetTag(WorkflowTags.ActionResultType, "Cancelled");
                throw;
            }
            catch (Exception ex)
            {
                // Unhandled exception → record the message and let the retry / dead-letter path
                // handle it. No port is fired (Dead steps don't enqueue successors any more).
                this.logger.LogError(ex, "Action {Kind} threw an unhandled exception.", step.Kind);
                actionActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                actionActivity?.SetTag(WorkflowTags.ActionResultType, "Exception");
                result = ActionExecutionResult.OnError(ex.Message);
            }
        }

        if (result.OutputPort is not null)
        {
            stepActivity?.SetTag(WorkflowTags.StepOutputPort, result.OutputPort);
        }
        if (result.Error is not null)
        {
            stepActivity?.SetStatus(ActivityStatusCode.Error, result.Error);
        }

        // CT.None: the action has run — its outcome (fired port, outputs, run mutations) must be
        // applied and staged even if the lane deadline or shutdown drain fires right after the
        // action body returns. Cancelling bookkeeping here would discard a computed result and
        // re-run the action's side effects on the next claim.
        await this.ApplyResultAsync(step, result, CancellationToken.None);
    }

    /// <summary>
    /// Common landing for <c>Execute</c>, <c>OnTimeout</c> and lane-timeout results — branches on
    /// Suspend / Terminate / Error / success and updates the step accordingly. Also the shared
    /// entry for <see cref="WorkflowMaintenanceSweeper"/> (timeout outcomes) and the worker's
    /// lane-timeout path, so all result semantics live in exactly one place.
    /// </summary>
    internal async Task ApplyResultAsync(
        WorkflowStepRecord step,
        ActionExecutionResult result,
        CancellationToken ct)
    {
        // Suspend: park the step in Waiting, optionally with a deadline. NextAttemptAt is the
        // sweeper's hook; DateTime.MaxValue keeps the sweeper from ever picking it up.
        if (result.IsSuspended)
        {
            step.Status = StepExecutionStatus.Waiting;
            step.NextAttemptAt = result.SuspendTimeoutSeconds is { } t
                ? DateTime.UtcNow.AddSeconds(t)
                : DateTime.MaxValue;
            step.OutputPort = null;
            step.Outputs = ToJsonElement(result.Outputs);
            step.LastError = null;
            this.store.UpdateStep(step);
            // Persist any bookmarks the action registered on the SAME pending batch as the step's
            // Waiting transition — the choke point's single flush makes "step parked" and "bookmarks
            // live" atomic. Empty / null = no signal-wait, regular suspend (Approve / Delay / …).
            if (result.Bookmarks is { Count: > 0 } bookmarks)
            {
                this.store.AddBookmarks(step, bookmarks);
                // Correlation-key PHI hardening: log HASHED keys, never raw. A generic WaitSignal key
                // is author-controlled and could carry PHI; the stable hash lets ops match this
                // suspend to the later SignalAsync (same key → same hash) without exposing the value.
                this.logger.LogInformation(
                    "Step {StepId} suspended with {Count} bookmark(s): {KeyHashes}",
                    step.Id,
                    bookmarks.Count,
                    bookmarks.Select(b => CorrelationKeyLog.Hash(b.CorrelationKey)).ToArray());
            }
            // Drive run.Status → Suspended (single-port engine: this Waiting step is now the only
            // active one). CheckRunCompletion is the single source of truth for run state.
            await this.fanOut.CheckRunCompletionAsync(step, ct);
            return;
        }

        // Successful early termination (FinishRun): step is Completed with the return payload
        // stamped on its outputs (for trace), and the run flips to Completed with the same
        // payload on run.ReturnValue (canonical slot the sub-workflow auto-resume reads).
        // No successor edges fire — the action declares no output ports.
        if (result.TerminatesRun)
        {
            var serializedReturn = ToJsonElement(result.ReturnValue);

            step.Status = StepExecutionStatus.Completed;
            step.OutputPort = null;
            step.Outputs = serializedReturn;
            step.CompletedAt = DateTime.UtcNow;
            step.LastError = null;
            this.store.UpdateStep(step);

            var run = await this.store.GetRunAsync(step.RunId, ct);
            if (run is not null)
            {
                // Flip to Completed unless run is already terminal (Completed/Failed) — Suspended
                // is fine to override (the FinishRun terminator preempts whatever Waiting step was
                // there). ALWAYS run the parent-resume path: TryResumeWaitingStepAsync atomically
                // guards on Waiting status, so a duplicate resume is a safe no-op.
                if (run.Status is not (WorkflowRunStatus.Completed or WorkflowRunStatus.Failed))
                {
                    run.Status = WorkflowRunStatus.Completed;
                    run.FinishedAt = DateTime.UtcNow;
                    run.ReturnValue = serializedReturn;
                    this.store.UpdateRun(run);
                }

                await this.fanOut.OnRunFinalizedAsync(step.RunId, ct);
            }
            return;
        }

        if (!string.IsNullOrEmpty(result.Error))
        {
            step.LastError = result.Error;
            // Non-transient errors (e.g. FailRun) skip retries — exhausted immediately.
            var exhausted = !result.IsTransient || step.AttemptCount >= this.settings.MaxAttempts;

            if (exhausted && !string.IsNullOrEmpty(result.RetryExhaustedPort))
            {
                // Author-declared fallback branch: attempts are spent (or the failure was
                // deterministic), but the action told the engine where the run should go in
                // that case — complete the step on the fallback port instead of dead-lettering
                // the whole run. LastError stays on the row, so the trace shows the failed
                // attempts AND the branch taken; Outputs carry the LAST attempt's error
                // payload (or the surviving retry checkpoint when the last attempt returned
                // none), merged into steps_outputs like any completion so the fallback branch
                // can read them via steps.<key>.*.
                step.Status = StepExecutionStatus.Completed;
                step.OutputPort = result.RetryExhaustedPort;
                if (result.Outputs is not null)
                {
                    step.Outputs = ToJsonElement(result.Outputs);
                }
                step.CompletedAt = DateTime.UtcNow;
                this.store.UpdateStep(step);
                this.logger.LogWarning(
                    "Step failed {AttemptCount}/{MaxAttempts} attempt(s) (transient={Transient}); taking fallback port '{Port}': {Error}",
                    step.AttemptCount,
                    this.settings.MaxAttempts,
                    result.IsTransient,
                    result.RetryExhaustedPort,
                    result.Error);
                await this.fanOut.EnqueueNextStepAsync(step, result.RetryExhaustedPort, ct);
                await this.fanOut.CheckRunCompletionAsync(step, ct);
            }
            else if (exhausted)
            {
                step.Status = StepExecutionStatus.Dead;
                step.OutputPort = null;
                // Preserve-if-null: a final attempt that returned no outputs must not wipe an
                // earlier retry checkpoint — it's postmortem evidence of what DID happen.
                if (result.Outputs is not null)
                {
                    step.Outputs = ToJsonElement(result.Outputs);
                }
                step.CompletedAt = DateTime.UtcNow;
                this.store.UpdateStep(step);
                this.logger.LogError(
                    "Step dead-lettered after {AttemptCount}/{MaxAttempts} attempt(s) (transient={Transient}): {Error}",
                    step.AttemptCount,
                    this.settings.MaxAttempts,
                    result.IsTransient,
                    result.Error);
                // Dead steps don't fire any successor edges — branches that should run on
                // failure must wire to an Error-kind port the action returns explicitly (or
                // use RetryExhaustedPort, which takes the branch above instead of Dead).
                await this.fanOut.CheckRunCompletionAsync(step, ct);
            }
            else
            {
                // Retry. Outputs returned WITH the transient error are the retry checkpoint:
                // persisted on the row and handed back to the next attempt via
                // ActionContext.PriorAttemptOutputs, so a multi-side-effect action can skip
                // work it already completed (row inserted, email still owed). Null outputs
                // leave the previous checkpoint intact — an attempt that crashed before
                // producing one must not erase earlier progress.
                step.Status = StepExecutionStatus.Pending;
                step.NextAttemptAt = DateTime.UtcNow.Add(this.BackoffFor(step.AttemptCount));
                if (result.Outputs is not null)
                {
                    step.Outputs = ToJsonElement(result.Outputs);
                }
                this.store.UpdateStep(step);
                this.logger.LogWarning(
                    "Step transient error on attempt {AttemptCount}/{MaxAttempts}, retrying at {NextAttemptAt:o}: {Error}",
                    step.AttemptCount,
                    this.settings.MaxAttempts,
                    step.NextAttemptAt,
                    result.Error);
            }
            return;
        }

        step.Status = StepExecutionStatus.Completed;
        step.OutputPort = result.OutputPort;
        step.Outputs = ToJsonElement(result.Outputs);
        step.CompletedAt = DateTime.UtcNow;
        step.LastError = null;
        this.store.UpdateStep(step);

        await this.fanOut.EnqueueNextStepAsync(step, result.OutputPort, ct);
        await this.fanOut.CheckRunCompletionAsync(step, ct);
    }

    /// <summary>Backoff delay for the given (1-based) attempt; last configured value repeats.</summary>
    internal TimeSpan BackoffFor(int attemptIndex)
    {
        var backoff = this.settings.BackoffSeconds;
        if (backoff.Length == 0) return TimeSpan.FromSeconds(30);
        var idx = Math.Min(attemptIndex - 1, backoff.Length - 1);
        return TimeSpan.FromSeconds(backoff[Math.Max(0, idx)]);
    }

    /// <summary>
    /// String form of the lane for trace tags. Stable values — dashboards / alerts can compare
    /// directly without parsing the enum. Keep in sync with the enum.
    /// </summary>
    internal static string FormatLane(WorkflowStepLane lane) => lane switch
    {
        WorkflowStepLane.Any => "any",
        WorkflowStepLane.FastOnly => "fast",
        WorkflowStepLane.LongOnly => "long",
        _ => "unknown",
    };

    /// <summary>
    /// Tag-friendly label for what flavour of <see cref="ActionExecutionResult"/> the action
    /// returned. Lets dashboards split by suspended-vs-fired-vs-terminated without having to
    /// pattern-match raw fields.
    /// </summary>
    private static string ClassifyResult(ActionExecutionResult result) =>
        result.IsSuspended ? "Suspended"
        : result.TerminatesRun ? "TerminatesRun"
        : result.Error is not null ? "Error"
        : result.OutputPort is not null ? "OnPort"
        : "None";

    /// <summary>
    /// Extracts the node's user-facing key from the parsed graph. Falls back to the node id
    /// when the graph is missing the entry (legacy runs from before keys were mandatory, or
    /// snapshot parse failures the FanOut cache already logged).
    /// </summary>
    private static string ResolveNodeKey(WorkflowGraph? graph, string nodeId)
    {
        var node = graph?.Nodes.FirstOrDefault(n => n.Id == nodeId);
        return string.IsNullOrWhiteSpace(node?.Key) ? nodeId : node.Key;
    }

    /// <summary>
    /// Converts an action's <see cref="ActionExecutionResult.Outputs"/> / <c>ReturnValue</c>
    /// (loose <see cref="object"/> from the contract) into the JsonElement the record stores.
    /// Null in → null out so the column stays unset for actions that didn't produce a payload.
    /// Goes through <see cref="WorkflowJsonOptions.Default"/> so camelCase + enum-as-string is
    /// applied consistently (action authors return anonymous types whose property names should
    /// surface to consumers in camelCase).
    /// </summary>
    private static JsonElement? ToJsonElement(object? value) =>
        value is null ? null : JsonSerializer.SerializeToElement(value, WorkflowJsonOptions.Default);
}
