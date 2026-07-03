using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Cancel semantics: tenant-checked NotFound, idempotent on terminal runs, run-only state
/// flip on success (steps are not touched — they finish naturally), parent resume cascade
/// for sub-workflows.
/// </summary>
public class WorkflowCancellerTests
{
    [Fact]
    public async Task Returns_not_found_when_run_missing()
    {
        var store = new FakeStore();
        var fanOut = new FakeFanOut();
        var canceller = new WorkflowCanceller(store, fanOut, NullLogger<WorkflowCanceller>.Instance);

        var result = await canceller.CancelAsync(
            new WorkflowCancelCommand { RunId = Guid.NewGuid(), TenantId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Equal(WorkflowCancelOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Returns_not_found_when_tenant_mismatches()
    {
        var run = new WorkflowRunRecord { TenantId = Guid.NewGuid(), Status = WorkflowRunStatus.Running };
        var store = new FakeStore(run);
        var fanOut = new FakeFanOut();
        var canceller = new WorkflowCanceller(store, fanOut, NullLogger<WorkflowCanceller>.Instance);

        var result = await canceller.CancelAsync(
            new WorkflowCancelCommand { RunId = run.Id, TenantId = Guid.NewGuid() }, // different tenant
            CancellationToken.None);

        Assert.Equal(WorkflowCancelOutcome.NotFound, result.Outcome);
        Assert.Equal(WorkflowRunStatus.Running, run.Status);
    }

    [Theory]
    [InlineData(WorkflowRunStatus.Completed)]
    [InlineData(WorkflowRunStatus.Failed)]
    public async Task Returns_already_terminal_for_completed_or_failed(string status)
    {
        var run = new WorkflowRunRecord { TenantId = Guid.NewGuid(), Status = status };
        var store = new FakeStore(run);
        var fanOut = new FakeFanOut();
        var canceller = new WorkflowCanceller(store, fanOut, NullLogger<WorkflowCanceller>.Instance);

        var result = await canceller.CancelAsync(
            new WorkflowCancelCommand { RunId = run.Id, TenantId = run.TenantId },
            CancellationToken.None);

        Assert.Equal(WorkflowCancelOutcome.AlreadyTerminal, result.Outcome);
        Assert.Equal(status, run.Status); // unchanged
    }

    [Fact]
    public async Task Cancels_running_run_flips_to_failed_with_reason()
    {
        var run = new WorkflowRunRecord
        {
            TenantId = Guid.NewGuid(),
            Status = WorkflowRunStatus.Running,
        };
        var store = new FakeStore(run);
        var fanOut = new FakeFanOut();
        var canceller = new WorkflowCanceller(store, fanOut, NullLogger<WorkflowCanceller>.Instance);

        var result = await canceller.CancelAsync(
            new WorkflowCancelCommand { RunId = run.Id, TenantId = run.TenantId, Reason = "operator decision" },
            CancellationToken.None);

        Assert.Equal(WorkflowCancelOutcome.Cancelled, result.Outcome);
        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.NotNull(run.FinishedAt);
        Assert.Contains("cancelled:", run.AbortReason);
        Assert.Contains("operator decision", run.AbortReason);
        Assert.Equal(0, fanOut.OnRunFinalizedCallCount); // top-level run, no parent to resume
        Assert.Equal(1, store.SaveCount); // cancel is a self-contained unit of work
    }

    [Fact]
    public async Task Cancels_suspended_run()
    {
        // Run waiting on an external signal — Suspended status. Cancel should still flip to Failed.
        var run = new WorkflowRunRecord
        {
            TenantId = Guid.NewGuid(),
            Status = WorkflowRunStatus.Suspended,
        };
        var store = new FakeStore(run);
        var fanOut = new FakeFanOut();
        var canceller = new WorkflowCanceller(store, fanOut, NullLogger<WorkflowCanceller>.Instance);

        var result = await canceller.CancelAsync(
            new WorkflowCancelCommand { RunId = run.Id, TenantId = run.TenantId },
            CancellationToken.None);

        Assert.Equal(WorkflowCancelOutcome.Cancelled, result.Outcome);
        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
    }

    [Fact]
    public async Task Sub_workflow_run_triggers_parent_resume_cascade()
    {
        // Run is a child started by RunWorkflow wait-mode → ParentStepId set → cancel must
        // drive OnRunFinalizedAsync so the parent's branch on `failed` port can react.
        var parentStepId = Guid.NewGuid();
        var run = new WorkflowRunRecord
        {
            TenantId = Guid.NewGuid(),
            Status = WorkflowRunStatus.Running,
            ParentRunId = Guid.NewGuid(),
            ParentStepId = parentStepId,
        };
        var store = new FakeStore(run);
        var fanOut = new FakeFanOut();
        var canceller = new WorkflowCanceller(store, fanOut, NullLogger<WorkflowCanceller>.Instance);

        var result = await canceller.CancelAsync(
            new WorkflowCancelCommand { RunId = run.Id, TenantId = run.TenantId },
            CancellationToken.None);

        Assert.Equal(WorkflowCancelOutcome.Cancelled, result.Outcome);
        Assert.Equal(1, fanOut.OnRunFinalizedCallCount);
        Assert.Equal(run.Id, fanOut.LastFinalizedRunId);
    }
}
