using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Triggers another workflow run from within a running step. Two modes:
/// <list type="bullet">
///   <item>
///     <b>Fire-and-forget</b> (<c>WaitForCompletion = false</c>): dispatches the child run and
///     immediately fires <c>started</c> so the parent continues. The child runs to completion on
///     its own; outcome is not surfaced back.
///   </item>
///   <item>
///     <b>Wait for completion</b> (<c>WaitForCompletion = true</c>): dispatches the child, then
///     suspends the parent step against the new run's id. When the child reaches a terminal
///     state, <c>WorkflowFanOut.CheckRunCompletionAsync</c> auto-resumes the parent step on
///     <c>success</c> (Completed) or <c>failed</c> (Failed), with the child's
///     <c>steps_outputs</c> stamped on the parent step's outputs.
///   </item>
/// </list>
/// Dispatch failures (no matching definition, empty graph, nesting / sub-run caps) fire the
/// <c>error</c> port with a machine-readable <c>reason</c> — authors can wire fallback paths
/// off it. The wait-mode deadline fires the dedicated <c>timedOut</c> port instead (same
/// pattern as WaitSignal), so "child never started" and "child is too slow" stay
/// distinguishable without parsing reasons. Nesting depth is capped by
/// <see cref="WorkflowEngineSettings.MaxNestingLevel"/>.
/// </summary>
public class RunWorkflowActionType : ActionType<RunWorkflowConfig>
{
    public const string KindName = "RunWorkflow";

    public static readonly IReadOnlyList<ActionPortDescriptor> Ports = new[]
    {
        new ActionPortDescriptor(RunWorkflowPorts.Started, "Started", ActionPortKind.Normal),
        new ActionPortDescriptor(RunWorkflowPorts.Success, "Success (child completed)", ActionPortKind.Normal),
        new ActionPortDescriptor(RunWorkflowPorts.Failed, "Failed (child failed)", ActionPortKind.Error),
        new ActionPortDescriptor(RunWorkflowPorts.Error, "Error (dispatch failed)", ActionPortKind.Error),
        new ActionPortDescriptor(RunWorkflowPorts.TimedOut, "Timed out", ActionPortKind.Error),
    };

    // Lazy resolution of IWorkflowDispatcher via the (scoped) IServiceProvider. IActionType is
    // enumerated by ActionTypeRegistry, which is reached transitively from IWorkflowDispatcher
    // (StepExecutionBuilder needs it), so injecting the dispatcher directly would close a
    // constructor DI cycle. Resolving at call time from the SAME scope's provider breaks the
    // cycle without giving up scoped lifetime — and keeps the dispatcher on this step's scoped
    // store, so the child run stages into the same unit of work as this step's transition
    // (same trick WorkflowFanOut uses for IWorkflowResumer).
    private readonly IServiceProvider services;
    private readonly IWorkflowStore store;
    private readonly ILogger<RunWorkflowActionType> logger;

    public RunWorkflowActionType(
        IServiceProvider services,
        IWorkflowStore store,
        ILogger<RunWorkflowActionType> logger)
    {
        this.services = services;
        this.store = store;
        this.logger = logger;
    }

    public override string Kind => KindName;

    public override string DisplayName => "Run workflow";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Ports;

    public override async Task<ActionExecutionResult> ExecuteAsync(
        ActionContext<RunWorkflowConfig> context, CancellationToken cancellationToken)
    {
        var ownerKind = context.Config.OwnerKind?.Trim();
        var triggerKind = context.Config.TriggerKind?.Trim();
        if (string.IsNullOrEmpty(ownerKind) || string.IsNullOrEmpty(triggerKind))
        {
            return ActionExecutionResult.OnError(
                "RunWorkflow requires both ownerKind and triggerKind.",
                transient: false);
        }

        // Snapshot the variables — config builder hands us either pre-resolved scalars or
        // Expr<object> wrappers; build a dict and serialize to JsonElement so the dispatcher
        // (which now wants JSON, not CLR types) gets a properly-typed payload. Resolved values
        // can be anything (string, number, bool, dict, JsonElement, …) — JsonSerializer handles
        // each natively.
        var dict = new Dictionary<string, object?>();
        foreach (var entry in context.Config.Variables ?? new())
        {
            if (string.IsNullOrWhiteSpace(entry.Name)) continue;
            dict[entry.Name] = entry.Expression?.Resolved;
        }
        var variables = JsonSerializer.SerializeToElement(dict, WorkflowJsonOptions.Default);

        // Look up the parent run so we can propagate nesting + parent linkage. Skipping the
        // lookup and assuming nesting=0 would let recursive RunWorkflow chains bypass the cap.
        var parentRun = await this.store.GetRunAsync(context.RunId, cancellationToken);
        var parentNesting = parentRun?.NestingLevel ?? 0;

        // In wait-for-completion mode we wire ParentStepId to this step so FanOut can auto-resume
        // it when the child terminates. Fire-and-forget leaves it null — the child still gets
        // ParentRunId for trace purposes, but no auto-resume happens.
        var request = new WorkflowDispatchRequest
        {
            TenantId = context.TenantId,
            OwnerKind = ownerKind,
            OwnerId = context.Config.OwnerId,
            TriggerKind = triggerKind,
            TriggerSourceKind = "WorkflowRun",
            TriggerSourceId = context.RunId,
            ActorUserId = context.ActorUserId,
            IsDryRun = context.IsDryRun,
            Variables = variables,
            NestingLevel = parentNesting + 1,
            ParentRunId = context.RunId,
            ParentStepId = context.Config.WaitForCompletion ? context.StepExecutionId : null,
        };

        // Dispatch on the CURRENT (step-scoped) dispatcher with flush:false — the child run +
        // its initial step are only STAGED on this step's scoped store here and commit
        // atomically with THIS step's own transition in the per-step flush. That single commit
        // closes two holes the previous fresh-scope + immediate-commit design had:
        //   1. Fast-child race: a child committed before this step's Waiting transition could
        //      run to completion first; its parent auto-resume then found this step not Waiting
        //      and skipped, parking the parent in Suspended forever. Now the child is never
        //      claimable before the parent's Waiting state is durable.
        //   2. Crash duplication: a crash between the two commits left an orphaned child; the
        //      released step then dispatched a SECOND child on retry (and the orphan still
        //      consumed a MaxSubRunsPerRun slot). Staged together, it's both-or-neither.
        // Safe on the shared context: WorkflowRunner.StartAsync stages run+step back-to-back
        // with no awaits in between (its throwing work happens before any Add), so a dispatch
        // failure leaves nothing behind for the batch flush to pick up.
        WorkflowDispatchResult result;
        try
        {
            var dispatcher = this.services.GetRequiredService<IWorkflowDispatcher>();
            result = await dispatcher.DispatchAsync(request, cancellationToken, flush: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogError(ex, "RunWorkflow dispatch failed for owner={Owner}/{OwnerId}, trigger={Trigger}", ownerKind, context.Config.OwnerId, triggerKind);
            return ActionExecutionResult.OnError(
                $"RunWorkflow dispatch failed: {ex.Message}",
                transient: true);
        }

        switch (result.Outcome)
        {
            case WorkflowDispatchOutcome.NotConfigured:
                return this.Port(RunWorkflowPorts.Error, new
                {
                    reason = "not_configured",
                    message = $"No workflow defined for owner '{ownerKind}'/'{context.Config.OwnerId}' on trigger '{triggerKind}'.",
                });

            case WorkflowDispatchOutcome.EmptyGraph:
                return this.Port(RunWorkflowPorts.Error, new
                {
                    reason = "empty_graph",
                    message = "Target workflow has no nodes / no start node.",
                });

            case WorkflowDispatchOutcome.NestingLimitExceeded:
                return this.Port(RunWorkflowPorts.Error, new
                {
                    reason = "nesting_limit_exceeded",
                    message = "Sub-workflow chain is too deep — bump WorkflowEngineSettings.MaxNestingLevel if intentional.",
                });

            case WorkflowDispatchOutcome.SubRunLimitExceeded:
                return this.Port(RunWorkflowPorts.Error, new
                {
                    reason = "sub_run_limit_exceeded",
                    message = "This run already started the maximum allowed sub-workflows — "
                            + "bump WorkflowEngineSettings.MaxSubRunsPerRun if intentional.",
                });

            case WorkflowDispatchOutcome.Started:
                if (!context.Config.WaitForCompletion)
                {
                    // Fire-and-forget: the child runs on its own, parent moves on immediately.
                    return this.Port(RunWorkflowPorts.Started, new
                    {
                        childRunId = result.RunId,
                    });
                }

                // Wait mode, but the child already reached a terminal state AT DISPATCH — its start
                // step was dead-on-arrival (config failed to resolve, e.g. invalid Liquid). Suspending
                // would park us for a resume that never comes: the child never runs on a worker, and a
                // synchronous resume can't fire either (this step isn't Waiting yet). Fire the terminal
                // port now with the child's outcome.
                if (result.RunStatus is WorkflowRunStatus.Failed or WorkflowRunStatus.Completed)
                {
                    var terminalPort = result.RunStatus == WorkflowRunStatus.Completed
                        ? RunWorkflowPorts.Success
                        : RunWorkflowPorts.Failed;
                    return this.Port(terminalPort, new
                    {
                        childRunId = result.RunId,
                        childStatus = result.RunStatus,
                    });
                }

                // Normal wait mode: park the step. FanOut.CheckRunCompletionAsync resumes us via the
                // success / failed port when the child terminates, stamping its steps_outputs
                // on this step's outputs.
                return this.Suspend(
                    timeoutSeconds: context.Config.TimeoutSeconds,
                    initialOutputs: new
                    {
                        childRunId = result.RunId,
                        waitForCompletion = true,
                    });

            default:
                return this.Port(RunWorkflowPorts.Error, new
                {
                    reason = "unknown",
                    message = $"Unexpected dispatch outcome: {result.Outcome}",
                });
        }
    }

    /// <summary>
    /// Wait-mode timeout: fire the dedicated <c>timedOut</c> port so authors can wire escalation
    /// (notify, retry, …) separately from dispatch-error fallbacks — same pattern as WaitSignal.
    /// The child run is not aborted; it keeps running and its eventual termination is silently
    /// ignored because the parent step is no longer Waiting.
    /// </summary>
    public override Task<ActionExecutionResult> OnStepTimedOutAsync(
        ActionContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(this.Port(
            RunWorkflowPorts.TimedOut,
            new
            {
                timedOutAt = DateTime.UtcNow.ToString("O"),
                message = "Sub-workflow did not finish within timeoutSeconds.",
            }));
    }

    /// <summary>
    /// Wait-mode resume: the parent step woke because the child run terminated. Pass-through —
    /// <c>WorkflowFanOut.ResumeParentStepAsync</c> computes the port (<c>success</c> / <c>failed</c>)
    /// from the child's terminal status and injects the child summary as the resume payload; we
    /// echo both verbatim. The engine validates the port and stamps the child summary on this
    /// step's outputs (same shape as the pre-ADR-027 direct-store stamp).
    /// </summary>
    public override Task<ActionExecutionResult> OnStepResumedAsync(
        ActionContext context, JsonElement? payload, string? port, CancellationToken cancellationToken)
        => Task.FromResult(this.Port(port ?? RunWorkflowPorts.Success, payload));
}

public class RunWorkflowConfig
{
    /// <summary>
    /// Owner kind for the target workflow definition (e.g. <c>"Form"</c>). Combined with
    /// <see cref="OwnerId"/> + <see cref="TriggerKind"/> to look up the definition.
    /// </summary>
    public string? OwnerKind { get; set; }

    /// <summary>Owner id — null for tenant-scoped definitions.</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// Trigger kind — usually a custom one like <c>"SubWorkflow"</c> or one of the standard
    /// <c>WorkflowTriggerKinds</c> if you want to reuse an existing trigger graph.
    /// </summary>
    public string? TriggerKind { get; set; }

    /// <summary>
    /// When true, the parent step suspends until the child reaches a terminal state. The child's
    /// final <c>steps_outputs</c> ends up under <c>steps.&lt;node_key&gt;.steps_outputs</c> so
    /// downstream nodes can read it. Default <c>false</c> = fire-and-forget.
    /// </summary>
    public bool WaitForCompletion { get; set; }

    /// <summary>
    /// Optional timeout (seconds) for wait mode. Null = wait indefinitely. When the timer
    /// elapses without a child terminal state, the engine sweeper fires the <c>timedOut</c> port.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Named expressions resolved per step and merged into the child run's variables (alongside
    /// the engine-added <c>trigger</c> object). Same shape as <c>TransformConfig.Values</c>.
    /// </summary>
    public List<RunWorkflowVariable> Variables { get; set; } = new();
}

public class RunWorkflowVariable
{
    public string Name { get; set; } = string.Empty;

    public Expr<object>? Expression { get; set; }
}
