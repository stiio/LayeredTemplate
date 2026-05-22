using System.Diagnostics;
using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Default <see cref="IWorkflowResumer"/>. Glues together the store (atomic transition), the
/// action-type registry (port validation), and the fan-out service (successor enqueue + run
/// completion check). Stays trigger- and tenant-agnostic: callers tell it which tenant they're
/// authorised for; the resumer rejects mismatches up-front.
/// </summary>
internal class WorkflowResumer : IWorkflowResumer
{
    private readonly IWorkflowStore store;
    private readonly IWorkflowFanOut fanOut;
    private readonly IActionTypeRegistry registry;
    private readonly ILogger<WorkflowResumer> logger;

    public WorkflowResumer(
        IWorkflowStore store,
        IWorkflowFanOut fanOut,
        IActionTypeRegistry registry,
        ILogger<WorkflowResumer> logger)
    {
        this.store = store;
        this.fanOut = fanOut;
        this.registry = registry;
        this.logger = logger;
    }

    public async Task<WorkflowResumeResult> ResumeAsync(
        WorkflowResumeCommand command, CancellationToken cancellationToken, bool flush = true)
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

        var portMeta = this.registry.GetPort(step.Kind, command.Port);
        if (portMeta is null)
        {
            activity?.SetTag(WorkflowTags.Outcome, nameof(WorkflowResumeFailureReason.InvalidPort));
            return WorkflowResumeResult.Failure(
                WorkflowResumeFailureReason.InvalidPort,
                $"Port '{command.Port}' is not declared by action '{step.Kind}'.");
        }

        var outputs = NormalizeOutputs(command.Outputs);
        var resumed = await this.store.TryResumeWaitingStepAsync(
            command.StepId,
            command.Port,
            outputs,
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

        await this.fanOut.EnqueueNextStepAsync(resumed, command.Port, cancellationToken);
        await this.fanOut.CheckRunCompletionAsync(resumed, cancellationToken);
        activity?.SetTag(WorkflowTags.Outcome, "Success");

        // Plugin's DbContext is independent from the consumer's. By default we flush so the
        // resume call is a self-contained unit of work; engine-internal callers (fan-out
        // auto-resume on sub-workflow completion) pass flush=false because the surrounding
        // worker batch flushes once at the end.
        if (flush)
        {
            await this.store.SaveChangesAsync(cancellationToken);
        }

        return WorkflowResumeResult.Success();
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
