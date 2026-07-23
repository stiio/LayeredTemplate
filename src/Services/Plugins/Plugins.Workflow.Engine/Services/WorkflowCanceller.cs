using System.Diagnostics;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Default <see cref="IWorkflowCanceller"/>. Tenant-checks the caller, flips the run to
/// <c>Failed</c>, and drives sub-workflow auto-resume so a parent waiting on this child sees
/// the cancellation propagate up its <c>failed</c> port.
/// <para>
/// Cancel does NOT touch step rows. In-flight actions run to completion and write whatever
/// they actually produced (the trace shows what really executed). The next step that would
/// have started sees <c>run.Status == Failed</c> in <see cref="WorkflowStepExecutor.ExecuteAsync"/>
/// and short-circuits to <c>step.Status = Dead</c>. Net effect: cancel honors the operator's
/// intent at the run level while preserving accurate step history.
/// </para>
/// <para>
/// There IS a microsecond race window where the worker's terminal-status write
/// (<c>running → completed</c>) can land just after cancel's <c>running → failed</c>, with
/// last-write-wins on the run row. Accepted as a non-issue: the window between worker's fresh
/// <c>GetRunAsync</c> and its <c>SaveChanges</c> is in single-digit milliseconds, and cancel
/// is an admin op that rarely fires concurrently with active work on the exact same run.
/// </para>
/// </summary>
internal class WorkflowCanceller : IWorkflowCanceller
{
    private const string DefaultReason = "cancelled";

    private readonly IWorkflowStore store;
    private readonly IWorkflowFanOut fanOut;
    private readonly ILogger<WorkflowCanceller> logger;

    public WorkflowCanceller(
        IWorkflowStore store,
        IWorkflowFanOut fanOut,
        ILogger<WorkflowCanceller> logger)
    {
        this.store = store;
        this.fanOut = fanOut;
        this.logger = logger;
    }

    public async Task<WorkflowCancelResult> CancelAsync(
        WorkflowCancelCommand command, CancellationToken cancellationToken)
    {
        using var scope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["RunId"] = command.RunId,
            ["TenantId"] = command.TenantId,
        });
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.run.cancel", ActivityKind.Internal);
        activity?.SetTag(WorkflowTags.RunId, command.RunId);
        activity?.SetTag(WorkflowTags.TenantId, command.TenantId);

        var run = await this.store.GetRunAsync(command.RunId, cancellationToken);
        // Tenant mismatch reported as NotFound to avoid leaking run existence across tenants.
        if (run is null || run.TenantId != command.TenantId)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowCancelOutcome.NotFound));
            return WorkflowCancelResult.NotFound();
        }
        if (run.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowCancelOutcome.AlreadyTerminal));
            return WorkflowCancelResult.AlreadyTerminal();
        }

        var trimmedReason = TrimReason(command.Reason);

        run.Status = WorkflowRunStatus.Failed;
        run.AbortReason = $"cancelled: {trimmedReason}";
        run.FinishedAt = DateTime.UtcNow;
        this.store.UpdateRun(run);

        // Sub-workflow cascade: parent run waiting on this child gets resumed on its `failed`
        // port. ResumeParentStepAsync exposes childStatus='failed' + childAbortReason='cancelled: ...'
        // so the parent's Switch / error-branch can route accordingly.
        if (run.ParentStepId is not null)
        {
            await this.fanOut.OnRunFinalizedAsync(run.Id, cancellationToken);
        }

        // Plugin's DbContext is independent — flush the cancel as a self-contained unit of work.
        await this.store.SaveChangesAsync(cancellationToken);

        this.logger.LogInformation("Run cancelled (reason: {Reason})", trimmedReason);

        activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowCancelOutcome.Cancelled));
        return WorkflowCancelResult.Cancelled();
    }

    private static string TrimReason(string? reason)
    {
        // 200-char column on AbortReason; reserve some room for the "cancelled: " prefix.
        const int Max = 180;
        var raw = string.IsNullOrWhiteSpace(reason) ? DefaultReason : reason.Trim();
        return raw.Length <= Max ? raw : raw[..Max];
    }
}
