using System.Diagnostics;
using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Default <see cref="IWorkflowSignaler"/>. Generic fan-out over the bookmarks registered for an
/// opaque (tenant, correlation key) pair: looks them up via the store, resumes each frozen step
/// through the shared <see cref="IWorkflowResumer"/>, and deletes consumed / stale bookmarks. Stays
/// domain-agnostic — keys are plain strings — and tenant-scoped on both the lookup AND every
/// resume (the resumer re-checks the run's tenant, and we always pass the trusted param tenant).
/// </summary>
internal class WorkflowSignaler : IWorkflowSignaler
{
    private readonly IWorkflowStore store;
    private readonly IWorkflowResumer resumer;
    private readonly ILogger<WorkflowSignaler> logger;

    public WorkflowSignaler(
        IWorkflowStore store,
        IWorkflowResumer resumer,
        ILogger<WorkflowSignaler> logger)
    {
        this.store = store;
        this.resumer = resumer;
        this.logger = logger;
    }

    public async Task<WorkflowSignalResult> SignalAsync(
        Guid tenantId, string correlationKey, JsonElement? payload, CancellationToken cancellationToken)
    {
        // Hash the key for the log scope: generic WaitSignal/SendSignal keys are author-controlled and
        // could carry PHI. A stable non-reversible token still lets ops correlate this signal to the
        // matching suspend log (same key → same hash) without exposing the raw value (PHI-hardening).
        using var scope = this.logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = tenantId,
            ["CorrelationKeyHash"] = CorrelationKeyLog.Hash(correlationKey),
        });
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            "workflow.signal", ActivityKind.Internal);
        activity?.SetTag(WorkflowTags.TenantId, tenantId);

        // Tenant-scoped lookup — MANDATORY: a key in another tenant must never surface here.
        var bookmarks = await this.store.FindBookmarksAsync(tenantId, correlationKey, cancellationToken);
        if (bookmarks.Count == 0)
        {
            activity?.SetTag(WorkflowTags.Outcome, "NoBookmarks");
            return new WorkflowSignalResult(Delivered: 0, Stale: 0);
        }

        int delivered = 0;
        int stale = 0;
        var consumed = new List<Guid>(capacity: bookmarks.Count);

        foreach (var bookmark in bookmarks)
        {
            // Resume the EXACT frozen step the bookmark recorded — never a re-derived one. TenantId
            // is ALWAYS the param tenant (never the bookmark's own value blindly): even though the
            // lookup was tenant-scoped, passing the trusted param keeps the resumer's run-tenant
            // re-check honest and forecloses any cross-tenant resume. Each resume commits as its
            // own atomic transaction, so one poisoned run can't roll back the others.
            var resume = await this.resumer.ResumeAsync(
                new WorkflowResumeCommand
                {
                    RunId = bookmark.RunId,
                    StepId = bookmark.StepId,
                    TenantId = tenantId,
                    Port = bookmark.ResumePort,
                    Payload = payload,
                },
                cancellationToken);

            if (resume.Succeeded)
            {
                delivered++;
                consumed.Add(bookmark.Id);
            }
            else if (resume.Reason is WorkflowResumeFailureReason.StepNotWaiting
                     or WorkflowResumeFailureReason.ConcurrencyConflict)
            {
                // Step already left Waiting (resumed elsewhere / timed out / swept). The bookmark is
                // garbage — count it Stale and delete it eagerly; no double-resume happens.
                stale++;
                consumed.Add(bookmark.Id);
            }
            else
            {
                // RunNotFound / StepNotFound / InvalidPort — a genuinely broken bookmark (run purged
                // mid-signal, port no longer declared, …); the reconciliation sweep / FK cascade
                // reaps it. ConfigResolutionFailed — an environmental transient-resolution failure:
                // the step is still Waiting and the bookmark stays LIVE, so a future signal on the
                // same key retries the delivery (the wait timeout is the backstop if none comes).
                // Either way: don't consume, log loud.
                this.logger.LogWarning(
                    "Bookmark {BookmarkId} on run {RunId} step {StepId} could not be resumed ({Reason}); bookmark left in place.",
                    bookmark.Id, bookmark.RunId, bookmark.StepId, resume.Reason);
            }
        }

        // Eager cleanup of resumed + stale bookmarks. Optimization only — the reconciliation sweep
        // is the correctness backstop. One set-based delete keeps it cheap.
        if (consumed.Count > 0)
        {
            await this.store.DeleteBookmarksAsync(consumed, cancellationToken);
            await this.store.SaveChangesAsync(cancellationToken);
        }

        activity?.SetTag(WorkflowTags.Outcome, "Signalled");
        this.logger.LogInformation(
            "Signal delivered to {Delivered} run(s), {Stale} stale bookmark(s) reaped.", delivered, stale);
        return new WorkflowSignalResult(delivered, stale);
    }
}
