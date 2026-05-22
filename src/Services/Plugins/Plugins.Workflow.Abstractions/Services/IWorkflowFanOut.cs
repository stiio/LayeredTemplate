using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;

namespace LayeredTemplate.Plugins.Workflow.Abstractions.Services;

/// <summary>
/// Walks the run's edges from a just-completed step and stages a successor step record on the
/// store for the fired output port. Shared between the engine worker (regular execution) and
/// any external code path that finalises a step out-of-band — most notably the resume API for
/// <see cref="Actions.ActionExecutionResult.IsSuspended"/> steps. Each step has at most one fan-out
/// edge (one port → one successor); the engine never spawns parallel branches itself.
/// </summary>
public interface IWorkflowFanOut
{
    /// <summary>
    /// Enqueue the successor step for <paramref name="completedStep"/>'s fired
    /// <paramref name="firedPort"/>. The method also folds <paramref name="completedStep"/>'s
    /// outputs into the run's <c>steps_outputs</c> JSON map (keyed by node key) so downstream
    /// expressions see them. No-op when <paramref name="firedPort"/> is null or no edge matches.
    /// </summary>
    /// <remarks>
    /// Caller is responsible for flushing via <c>IWorkflowStore.SaveChangesAsync</c>; this
    /// method only stages.
    /// </remarks>
    Task EnqueueNextStepAsync(
        WorkflowStepRecord completedStep,
        string? firedPort,
        CancellationToken cancellationToken);

    /// <summary>
    /// Re-evaluates whether a run can transition to <c>Completed</c> / <c>Failed</c> after the
    /// given step finished. Drives the parent-resume path internally on terminal transitions.
    /// </summary>
    Task CheckRunCompletionAsync(
        WorkflowStepRecord justFinished,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hand-off used by callers that have already flipped the run to a terminal status —
    /// <c>FinishRun</c> action via the worker's TerminatesRun branch, or the operator-driven
    /// <c>IWorkflowCanceller</c>. Drives the parent-resume path without re-running the
    /// run-completion bookkeeping. The current run row is expected to have its final
    /// <c>Status</c>, <c>FinishedAt</c>, <c>AbortReason</c> (for cancel) and
    /// <c>ReturnValue</c> (for FinishRun) staged before this call.
    /// </summary>
    Task OnRunFinalizedAsync(
        Guid runId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the parsed <see cref="WorkflowGraph"/> for <paramref name="run"/>, deserialising
    /// the run's frozen <c>WorkflowSnapshot</c> on first access and caching the result for the
    /// lifetime of this <c>IWorkflowFanOut</c> instance (= one worker batch / one resume call).
    /// Eliminates per-step re-parsing of the snapshot in hot paths like fan-out edge walking
    /// and node-key resolution. Returns <c>null</c> when the snapshot can't be parsed.
    /// </summary>
    Task<WorkflowGraph?> GetGraphAsync(
        WorkflowRunRecord run,
        CancellationToken cancellationToken);
}
