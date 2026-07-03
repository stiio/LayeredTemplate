using System.Diagnostics;
using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Default <see cref="IWorkflowFanOut"/> implementation. Walks the run's edges from a
/// just-completed step and stages exactly zero or one successor step — there is no parallel
/// branching: each port goes to at most one target node, the engine never duplicates the
/// fan-out itself. Shared between the worker (regular execution) and external finalisers
/// (notably the resume API for suspended steps) so edge-walking + run-completion live in one
/// place.
/// </summary>
internal class WorkflowFanOut : IWorkflowFanOut
{
    private readonly IWorkflowStore store;
    private readonly IStepExecutionBuilder stepBuilder;
    private readonly WorkflowEngineSettings settings;
    private readonly ILogger<WorkflowFanOut> logger;

    // Lazy resolution of IWorkflowResumer via the (scoped) IServiceProvider. WorkflowResumer
    // depends on IWorkflowFanOut, so injecting the resumer directly would close a constructor DI
    // cycle. Resolving it at call time from the SAME scope's provider breaks the cycle without
    // giving up scoped lifetime (same trick RunWorkflowActionType uses for IWorkflowDispatcher).
    private readonly IServiceProvider services;

    // Per-instance graph cache. Workflow snapshots are immutable (frozen at run start), so
    // caching by runId is safe for the scoped lifetime of this fan-out. The same scope serves
    // one worker batch (~10 steps) or one resume call — N entries max, no eviction needed.
    private readonly Dictionary<Guid, WorkflowGraph?> graphCache = new();

    public WorkflowFanOut(
        IWorkflowStore store,
        IStepExecutionBuilder stepBuilder,
        IOptions<WorkflowEngineSettings> settings,
        IServiceProvider services,
        ILogger<WorkflowFanOut> logger)
    {
        this.store = store;
        this.stepBuilder = stepBuilder;
        this.settings = settings.Value;
        this.services = services;
        this.logger = logger;
    }

    public Task<WorkflowGraph?> GetGraphAsync(WorkflowRunRecord run, CancellationToken cancellationToken)
    {
        if (this.graphCache.TryGetValue(run.Id, out var cached))
        {
            return Task.FromResult(cached);
        }

        WorkflowGraph? graph;
        try
        {
            graph = JsonSerializer.Deserialize<WorkflowGraph>(run.WorkflowSnapshot, WorkflowJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            this.logger.LogError(ex, "Run {RunId} workflow snapshot is not valid JSON; treating as empty graph.", run.Id);
            graph = null;
        }

        this.graphCache[run.Id] = graph;
        return Task.FromResult(graph);
    }

    public async Task EnqueueNextStepAsync(
        WorkflowStepRecord completedStep,
        string? firedPort,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(firedPort)) return;

        using var activity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.fanout.enqueue_next", ActivityKind.Internal);
        activity?.SetTag(WorkflowTags.RunId, completedStep.RunId);
        activity?.SetTag(WorkflowTags.FanOutFromStepId, completedStep.Id);
        activity?.SetTag(WorkflowTags.FanOutFiredPort, firedPort);

        var run = await this.store.GetRunAsync(completedStep.RunId, cancellationToken);
        if (run is null) return;

        // Guard against late-arriving fan-outs after an explicit terminator (FinishRun / FailRun)
        // already flipped the run to a terminal status. Suspended is fine — that's exactly the
        // state when a parent run is awaiting Approve/Delay/RunWorkflow-wait, and resume of the
        // child causes us to enqueue the next step here.
        if (run.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed) return;

        var graph = await this.GetGraphAsync(run, cancellationToken);
        if (graph is null) return;

        var nodesById = graph.Nodes
            .Where(n => !string.IsNullOrEmpty(n.Id))
            .ToDictionary(n => n.Id, StringComparer.Ordinal);

        // Merge outputs into run.steps_outputs, keyed by node.key (falls back to NodeId UUID for
        // runs snapshotted before keys existed). Deserialize the existing object into a CLR
        // dict, append the slot, re-serialize back to JsonElement — the storage converter wants
        // JsonElement on the property, and EnumerateObject on JsonElement doesn't let us mutate
        // a single key in place.
        if (completedStep.Outputs is { } stepOutputs)
        {
            var stepsOutputs = run.StepsOutputs.ValueKind == JsonValueKind.Object
                ? run.StepsOutputs.Deserialize<Dictionary<string, object?>>(WorkflowJsonOptions.Default) ?? new Dictionary<string, object?>()
                : new Dictionary<string, object?>();
            var outputsObj = ExpressionModelBuilder.JsonElementToClr(stepOutputs);
            var slotKey = nodesById.TryGetValue(completedStep.NodeId, out var completedNode)
                && !string.IsNullOrWhiteSpace(completedNode.Key)
                    ? completedNode.Key
                    : completedStep.NodeId;
            stepsOutputs[slotKey] = outputsObj;
            run.StepsOutputs = JsonSerializer.SerializeToElement(stepsOutputs, WorkflowJsonOptions.Default);
            this.store.UpdateRun(run);
        }

        // Find the (single) edge from this node + this port. The validator forbids multiple
        // edges sharing the same (NodeId, Port) — first match wins.
        var edge = graph.Edges.FirstOrDefault(e =>
            e.From is not null
            && e.From.NodeId == completedStep.NodeId
            && string.Equals(e.From.Port, firedPort, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(e.To));
        if (edge is null) return;

        if (!nodesById.TryGetValue(edge.To, out var targetNode)) return;
        activity?.SetTag(WorkflowTags.FanOutTargetNodeId, edge.To);

        // Safety nets — store knows about both saved + locally-staged steps.
        var existingStepCount = await this.store.CountStepsForRunAsync(run.Id, cancellationToken);
        if (existingStepCount >= this.settings.MaxStepsPerRun)
        {
            run.Status = WorkflowRunStatus.Failed;
            run.AbortReason = $"step_cap: exceeded {this.settings.MaxStepsPerRun} steps";
            run.FinishedAt = DateTime.UtcNow;
            this.store.UpdateRun(run);
            this.logger.LogWarning(
                "Run {RunId} aborted at {Cap}-step cap", run.Id, this.settings.MaxStepsPerRun);
            return;
        }

        // Per-node visit cap — same rule for every node, including loop bodies. Bump
        // MaxVisitsPerNode if you're running ForEach with iteration counts close to the cap.
        // Targeted count: only the enqueue TARGET's visits matter here.
        var targetVisits = await this.store.CountVisitsForNodeAsync(run.Id, edge.To, cancellationToken);
        if (targetVisits >= this.settings.MaxVisitsPerNode)
        {
            this.logger.LogWarning(
                "Run {RunId} skipping enqueue of node {NodeId} — visit cap {Cap} reached",
                run.Id, edge.To, this.settings.MaxVisitsPerNode);
            return;
        }

        // Both fields are already JsonElement on the record — no per-step deserialize.
        var model = ExpressionModelBuilder.Build(run.StaticContext, run.StepsOutputs);

        var newStep = await this.stepBuilder.TryBuildAsync(
            run, targetNode, completedStep.Id, firedPort, model, cancellationToken);
        if (newStep is not null)
        {
            this.store.AddStep(newStep);
        }
    }

    public async Task CheckRunCompletionAsync(WorkflowStepRecord justFinished, CancellationToken cancellationToken)
    {
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.fanout.check_completion", ActivityKind.Internal);
        activity?.SetTag(WorkflowTags.RunId, justFinished.RunId);

        var run = await this.store.GetRunAsync(justFinished.RunId, cancellationToken);
        if (run is null) return;
        // Already terminal — no transitions possible. Don't redo parent-resume either; it ran
        // when we first hit the terminal state.
        if (run.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed) return;

        // Single scan computes the canonical state from authoritative step_executions data.
        var summary = await this.store.GetStepStateSummaryAsync(run.Id, justFinished.Id, cancellationToken);

        // Decide desired run.Status from summary:
        //   - any active step (Pending/Running) → Running
        //   - else any Waiting step → Suspended (parked on external signal)
        //   - else terminal: Failed if any Dead step, otherwise Completed
        string desiredStatus;
        if (summary.HasPendingOrRunning)
        {
            desiredStatus = WorkflowRunStatus.Running;
        }
        else if (summary.HasWaiting)
        {
            desiredStatus = WorkflowRunStatus.Suspended;
        }
        else
        {
            desiredStatus = summary.HasDead ? WorkflowRunStatus.Failed : WorkflowRunStatus.Completed;
        }

        activity?.SetTag(WorkflowTags.RunStatusBefore, run.Status);
        activity?.SetTag(WorkflowTags.RunStatusAfter, desiredStatus);

        if (run.Status == desiredStatus) return;  // no transition needed

        var becameTerminal = desiredStatus is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed;
        activity?.SetTag(WorkflowTags.RunBecameTerminal, becameTerminal);
        run.Status = desiredStatus;
        if (becameTerminal)
        {
            run.FinishedAt = DateTime.UtcNow;
        }
        this.store.UpdateRun(run);

        // Sub-workflow auto-resume — only on terminal transition. If a RunWorkflow action started
        // this run in waitForCompletion mode, the parent's step is parked in Waiting against
        // ParentStepId; flip it on the matching port so the parent run continues.
        if (becameTerminal && run.ParentStepId is not null)
        {
            await this.ResumeParentStepAsync(run, cancellationToken);
        }
    }

    public async Task OnRunFinalizedAsync(Guid runId, CancellationToken cancellationToken)
    {
        // Caller (FinishRun branch in worker / IWorkflowCanceller) has already set run.Status /
        // FinishedAt / AbortReason / ReturnValue. We only need to drive the parent-resume
        // cascade — same path as the natural-completion case in CheckRunCompletionAsync.
        var run = await this.store.GetRunAsync(runId, cancellationToken);
        if (run is null || run.ParentStepId is null) return;

        await this.ResumeParentStepAsync(run, cancellationToken);
    }

    /// <summary>
    /// Drives the parent-side completion of a sub-workflow. C1 (ADR-027): instead of poking the
    /// store's atomic resume directly, this routes through <see cref="IWorkflowResumer"/> so the
    /// non-timeout resume path is uniform and <c>RunWorkflow.OnStepResumedAsync</c> isn't dead
    /// code. The computed <c>success</c>/<c>failed</c> port (from the child's terminal status) is
    /// passed as the command port; the child summary is passed as the resume payload, which the
    /// action echoes and the resumer stamps on the parent step's outputs (same shape as before).
    /// The resumer also drives the parent fan-out + run-completion recheck, so a chain of waiting
    /// parents unwinds within the same batch (same recursion as the prior direct path — the only
    /// addition is the action's pass-through OnStepResumed + port re-validation in between).
    /// <para>
    /// The resumer commits the resume as its own atomic storage transaction, and that flush
    /// deliberately carries this scope's already-staged changes with it — the child's terminal
    /// transition and the parent's resume land in one commit. In grandparent chains the nested
    /// resume participates in the outermost resume's transaction instead. Resolved lazily from
    /// the scope to avoid the resumer↔fanout constructor cycle.
    /// </para>
    /// </summary>
    private async Task ResumeParentStepAsync(WorkflowRunRecord childRun, CancellationToken cancellationToken)
    {
        using var scope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["ChildRunId"] = childRun.Id,
            ["ParentRunId"] = childRun.ParentRunId,
            ["ParentStepId"] = childRun.ParentStepId,
            ["ChildStatus"] = childRun.Status,
        });

        var port = childRun.Status == WorkflowRunStatus.Completed
            ? RunWorkflowPorts.Success
            : RunWorkflowPorts.Failed;

        // returnValue is the only payload surfaced to the parent. It's populated by an explicit
        // FinishRun action in the child; runs that completed naturally (no FinishRun) get null.
        // No fallback to the child's steps_outputs by design — keep the parent's contract clean
        // and decoupled from the child's internal node names.
        // Convert to plain CLR (Dict / List / scalar) so the receiving step's outputs round-trip
        // cleanly through JsonSerializer when the resumer normalizes + stamps them.
        var returnValueObj = childRun.ReturnValue is { } rv
            ? ExpressionModelBuilder.JsonElementToClr(rv)
            : null;

        var summary = new Dictionary<string, object?>
        {
            ["childRunId"] = childRun.Id,
            ["childStatus"] = childRun.Status,
            ["childAbortReason"] = childRun.AbortReason,
            ["returnValue"] = returnValueObj,
        };
        // Serialize the summary to a JsonElement so it flows through the resumer's payload contract
        // (NormalizeOutputs flattens the object → the same dict the store stamped pre-ADR-027). enum
        // childStatus / Guid childRunId serialize identically here as they did via the store.
        var payload = JsonSerializer.SerializeToElement(summary, WorkflowJsonOptions.Default);

        // Resolve the resumer from the current scope (lazy — see field comment). The resumer wins
        // the same atomic Waiting-guard the store call won before, then runs RunWorkflow's
        // pass-through OnStepResumed and drives parent fan-out + completion.
        var resumer = this.services.GetRequiredService<IWorkflowResumer>();
        var result = await resumer.ResumeAsync(
            new WorkflowResumeCommand
            {
                RunId = childRun.ParentRunId!.Value,
                StepId = childRun.ParentStepId!.Value,
                TenantId = childRun.TenantId,
                Port = port,
                Payload = payload,
            },
            cancellationToken);

        if (!result.Succeeded)
        {
            // Parent step is no longer Waiting (manual resume / sweeper dead-letter), or some other
            // resumer-level failure. Either way the parent fan-out has already been driven by
            // whatever did the prior transition; nothing more to do here — log loud, same as before.
            this.logger.LogWarning(
                "Sub-workflow {ChildRunId} finished but parent step {ParentStepId} could not be auto-resumed ({Reason}: {Message}); skipping.",
                childRun.Id, childRun.ParentStepId, result.Reason, result.Message);
        }
    }
}

/// <summary>
/// Port ids fired by <c>RunWorkflowAction</c>. Lifted out of the action so fan-out can resume
/// a sub-workflow's parent without taking a dependency on the engine's action library.
/// </summary>
internal static class RunWorkflowPorts
{
    /// <summary>Fire-and-forget mode: child dispatched, parent moves on immediately.</summary>
    public const string Started = "started";

    /// <summary>Wait mode: the child run reached <c>Completed</c>.</summary>
    public const string Success = "success";

    /// <summary>Wait mode: the child run reached <c>Failed</c> (its own logic failed / it was cancelled).</summary>
    public const string Failed = "failed";

    /// <summary>
    /// The RunWorkflow action itself couldn't do its job — dispatch failed (no matching
    /// definition, empty graph, nesting / sub-run caps, unexpected dispatcher error). Distinct
    /// from <see cref="Failed"/>, which reports the CHILD's own terminal failure.
    /// </summary>
    public const string Error = "error";

    /// <summary>
    /// Wait-mode deadline elapsed before the child reached a terminal state — same pattern as
    /// WaitSignal's <c>timedOut</c>. The child keeps running; only the parent stops waiting.
    /// </summary>
    public const string TimedOut = "timedOut";
}
