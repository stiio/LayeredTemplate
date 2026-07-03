using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Storage.EFCore;
using LayeredTemplate.Plugins.Workflow.Storage.EFCore.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// The claimable-lanes predicate of <c>WorkflowWorkNotifyInterceptor</c> — the crux of the
/// producer side of LISTEN/NOTIFY. A false negative here silently degrades every dispatch to
/// fallback-poll latency; a false positive spams wakes. Claimable = tracked step written as
/// <c>pending</c> with <c>next_attempt_at</c> due NOW:
///   - added / modified pending steps with a due next_attempt_at → notify (fast vs long lane
///     by IsLongRunning)
///   - retry rows (pending, FUTURE next_attempt_at) → no notify (nobody could claim them yet)
///   - non-pending statuses and merely-read (Unchanged) rows → no notify.
/// Uses the EF InMemory provider purely to get a real change tracker; nothing is saved.
/// </summary>
public class WorkflowWorkNotifyInterceptorTests
{
    [Fact]
    public void Added_due_pending_fast_step_notifies_fast_lane()
    {
        using var context = NewContext();
        context.WorkflowStepExecutions.Add(NewStep(StepExecutionStatus.Pending, dueNow: true, isLongRunning: false));

        var lanes = WorkflowWorkNotifyInterceptor.CollectClaimableLanes(context);

        Assert.NotNull(lanes);
        Assert.True(lanes.Fast);
        Assert.False(lanes.Long);
    }

    [Fact]
    public void Added_due_pending_long_step_notifies_long_lane()
    {
        using var context = NewContext();
        context.WorkflowStepExecutions.Add(NewStep(StepExecutionStatus.Pending, dueNow: true, isLongRunning: true));

        var lanes = WorkflowWorkNotifyInterceptor.CollectClaimableLanes(context);

        Assert.NotNull(lanes);
        Assert.False(lanes.Fast);
        Assert.True(lanes.Long);
    }

    [Fact]
    public void Mixed_fast_and_long_steps_notify_both_lanes()
    {
        using var context = NewContext();
        context.WorkflowStepExecutions.Add(NewStep(StepExecutionStatus.Pending, dueNow: true, isLongRunning: false));
        context.WorkflowStepExecutions.Add(NewStep(StepExecutionStatus.Pending, dueNow: true, isLongRunning: true));

        var lanes = WorkflowWorkNotifyInterceptor.CollectClaimableLanes(context);

        Assert.NotNull(lanes);
        Assert.True(lanes.Fast);
        Assert.True(lanes.Long);
    }

    [Fact]
    public void Retry_row_with_future_next_attempt_does_not_notify()
    {
        // Failed attempt re-scheduled with backoff: pending, but claimable only later. A wake
        // now would find nothing; the fallback poll picks it up when due.
        using var context = NewContext();
        context.WorkflowStepExecutions.Add(NewStep(StepExecutionStatus.Pending, dueNow: false, isLongRunning: false));

        Assert.Null(WorkflowWorkNotifyInterceptor.CollectClaimableLanes(context));
    }

    [Theory]
    [InlineData(StepExecutionStatus.Running)]
    [InlineData(StepExecutionStatus.Waiting)]
    [InlineData(StepExecutionStatus.Completed)]
    [InlineData(StepExecutionStatus.Failed)]
    [InlineData(StepExecutionStatus.Dead)]
    public void Non_pending_statuses_do_not_notify(string status)
    {
        using var context = NewContext();
        context.WorkflowStepExecutions.Add(NewStep(status, dueNow: true, isLongRunning: false));

        Assert.Null(WorkflowWorkNotifyInterceptor.CollectClaimableLanes(context));
    }

    [Fact]
    public void Unchanged_tracked_pending_step_does_not_notify()
    {
        // A merely-read row (e.g. loaded by a query earlier in the scope) isn't being written
        // by this flush — notifying for it would fire on every unrelated save.
        using var context = NewContext();
        var entry = context.WorkflowStepExecutions.Attach(NewStep(StepExecutionStatus.Pending, dueNow: true, isLongRunning: false));
        entry.State = EntityState.Unchanged;

        Assert.Null(WorkflowWorkNotifyInterceptor.CollectClaimableLanes(context));
    }

    [Fact]
    public void Step_modified_back_to_pending_notifies()
    {
        // Release-style transition (running → pending) through the tracker must count the same
        // as an insert: the row becomes claimable by this flush.
        using var context = NewContext();
        var step = NewStep(StepExecutionStatus.Running, dueNow: true, isLongRunning: false);
        var entry = context.WorkflowStepExecutions.Attach(step);
        entry.State = EntityState.Unchanged;

        step.Status = StepExecutionStatus.Pending;

        var lanes = WorkflowWorkNotifyInterceptor.CollectClaimableLanes(context);

        Assert.NotNull(lanes);
        Assert.True(lanes.Fast);
    }

    [Fact]
    public void Empty_tracker_does_not_notify()
    {
        using var context = NewContext();
        Assert.Null(WorkflowWorkNotifyInterceptor.CollectClaimableLanes(context));
    }

    // ----- helpers -----

    private static WorkflowDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WorkflowDbContext(options);
    }

    private static WorkflowStepExecution NewStep(string status, bool dueNow, bool isLongRunning) => new()
    {
        RunId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        NodeId = "n1",
        Kind = "Transform",
        Status = status,
        IsLongRunning = isLongRunning,
        NextAttemptAt = dueNow ? DateTime.UtcNow.AddSeconds(-1) : DateTime.UtcNow.AddMinutes(5),
    };
}
