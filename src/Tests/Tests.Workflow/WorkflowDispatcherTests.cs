using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Microsoft.Extensions.Options;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Sub-workflow dispatcher rules:
///  - depth &gt; <c>MaxNestingLevel</c> short-circuits with <c>NestingLimitExceeded</c>;
///  - the per-parent sub-run cap fires before the definition lookup, and only for sub-dispatches;
///  - parent linkage (<c>ParentRunId</c>, <c>ParentStepId</c>, <c>NestingLevel</c>) is copied
///    onto the new run via the runner;
///  - missing definition returns <c>NotConfigured</c>, not <c>EmptyGraph</c>;
///  - <c>flush</c> controls the dispatcher's own SaveChanges: default true = self-contained
///    unit of work, false = staged for the caller's flush (the RunWorkflow atomic-dispatch mode).
/// </summary>
public class WorkflowDispatcherTests
{
    [Fact]
    public async Task Refuses_dispatch_when_nesting_level_exceeds_cap()
    {
        var settings = new WorkflowEngineSettings { MaxNestingLevel = 3 };
        var store = new FakeStore();
        var runner = new FakeRunner();
        var dispatcher = new WorkflowDispatcher(store, runner, Options.Create(settings));

        var result = await dispatcher.DispatchAsync(
            new WorkflowDispatchRequest
            {
                TenantId = Guid.NewGuid(),
                OwnerKind = "Form",
                OwnerId = Guid.NewGuid(),
                TriggerKind = "SubWorkflow",
                Variables = null,
                NestingLevel = 4,
            },
            CancellationToken.None);

        Assert.Equal(WorkflowDispatchOutcome.NestingLimitExceeded, result.Outcome);
        Assert.False(store.FindDefinitionCalled); // Cap check fires before the DB roundtrip.
        Assert.False(runner.StartCalled);
    }

    [Fact]
    public async Task Allows_dispatch_at_exactly_the_cap()
    {
        var settings = new WorkflowEngineSettings { MaxNestingLevel = 3 };
        var run = MakeRun();
        var store = new FakeStore { Definition = MakeDefinition() };
        var runner = new FakeRunner(run);
        var dispatcher = new WorkflowDispatcher(store, runner, Options.Create(settings));

        var result = await dispatcher.DispatchAsync(
            new WorkflowDispatchRequest
            {
                TenantId = run.TenantId,
                OwnerKind = "Form",
                OwnerId = Guid.NewGuid(),
                TriggerKind = "SubWorkflow",
                Variables = null,
                NestingLevel = 3,
            },
            CancellationToken.None);

        Assert.Equal(WorkflowDispatchOutcome.Started, result.Outcome);
        Assert.Equal(run.Id, result.RunId);
        Assert.True(runner.StartCalled);
    }

    [Fact]
    public async Task Propagates_parent_fields_into_the_intent()
    {
        var settings = new WorkflowEngineSettings { MaxNestingLevel = 5 };
        var store = new FakeStore { Definition = MakeDefinition() };
        var runner = new FakeRunner(MakeRun());
        var dispatcher = new WorkflowDispatcher(store, runner, Options.Create(settings));

        var parentRunId = Guid.NewGuid();
        var parentStepId = Guid.NewGuid();

        await dispatcher.DispatchAsync(
            new WorkflowDispatchRequest
            {
                TenantId = Guid.NewGuid(),
                OwnerKind = "Form",
                OwnerId = Guid.NewGuid(),
                TriggerKind = "SubWorkflow",
                Variables = null,
                NestingLevel = 2,
                ParentRunId = parentRunId,
                ParentStepId = parentStepId,
            },
            CancellationToken.None);

        Assert.NotNull(runner.LastIntent);
        Assert.Equal(2, runner.LastIntent!.NestingLevel);
        Assert.Equal(parentRunId, runner.LastIntent!.ParentRunId);
        Assert.Equal(parentStepId, runner.LastIntent!.ParentStepId);
    }

    [Fact]
    public async Task Refuses_dispatch_when_parent_already_at_sub_run_cap()
    {
        // Cap=3, parent already has 3 direct children → next dispatch is refused.
        var settings = new WorkflowEngineSettings { MaxNestingLevel = 5, MaxSubRunsPerRun = 3 };
        var store = new FakeStore { Definition = MakeDefinition(), ChildRunCount = 3 };
        var runner = new FakeRunner();
        var dispatcher = new WorkflowDispatcher(store, runner, Options.Create(settings));

        var result = await dispatcher.DispatchAsync(
            new WorkflowDispatchRequest
            {
                TenantId = Guid.NewGuid(),
                OwnerKind = "Form",
                OwnerId = Guid.NewGuid(),
                TriggerKind = "SubWorkflow",
                Variables = null,
                NestingLevel = 1,
                ParentRunId = Guid.NewGuid(),
            },
            CancellationToken.None);

        Assert.Equal(WorkflowDispatchOutcome.SubRunLimitExceeded, result.Outcome);
        Assert.True(store.CountChildRunsCalled);
        Assert.False(store.FindDefinitionCalled); // Cap fires before definition lookup.
        Assert.False(runner.StartCalled);
    }

    [Fact]
    public async Task Allows_dispatch_when_parent_below_sub_run_cap()
    {
        // Cap=3, parent has 2 direct children → next dispatch goes through.
        var settings = new WorkflowEngineSettings { MaxNestingLevel = 5, MaxSubRunsPerRun = 3 };
        var run = MakeRun();
        var store = new FakeStore { Definition = MakeDefinition(), ChildRunCount = 2 };
        var runner = new FakeRunner(run);
        var dispatcher = new WorkflowDispatcher(store, runner, Options.Create(settings));

        var result = await dispatcher.DispatchAsync(
            new WorkflowDispatchRequest
            {
                TenantId = run.TenantId,
                OwnerKind = "Form",
                OwnerId = Guid.NewGuid(),
                TriggerKind = "SubWorkflow",
                Variables = null,
                NestingLevel = 1,
                ParentRunId = Guid.NewGuid(),
            },
            CancellationToken.None);

        Assert.Equal(WorkflowDispatchOutcome.Started, result.Outcome);
        Assert.True(store.CountChildRunsCalled);
        Assert.True(runner.StartCalled);
    }

    [Fact]
    public async Task Top_level_dispatch_skips_sub_run_cap_check()
    {
        // No ParentRunId means top-level (form submit / manual API). Cap doesn't apply, count
        // isn't even queried — we don't want to add latency to the form-submit path.
        var settings = new WorkflowEngineSettings { MaxSubRunsPerRun = 0 };
        var run = MakeRun();
        var store = new FakeStore { Definition = MakeDefinition(), ChildRunCount = 999 };
        var runner = new FakeRunner(run);
        var dispatcher = new WorkflowDispatcher(store, runner, Options.Create(settings));

        var result = await dispatcher.DispatchAsync(
            new WorkflowDispatchRequest
            {
                TenantId = run.TenantId,
                OwnerKind = "Form",
                OwnerId = Guid.NewGuid(),
                TriggerKind = "SubmissionCompleted",
                Variables = null,
                // ParentRunId omitted → top-level
            },
            CancellationToken.None);

        Assert.Equal(WorkflowDispatchOutcome.Started, result.Outcome);
        Assert.False(store.CountChildRunsCalled);
    }

    [Fact]
    public async Task Returns_not_configured_when_definition_missing()
    {
        var settings = new WorkflowEngineSettings { MaxNestingLevel = 3 };
        var store = new FakeStore();
        var runner = new FakeRunner();
        var dispatcher = new WorkflowDispatcher(store, runner, Options.Create(settings));

        var result = await dispatcher.DispatchAsync(
            new WorkflowDispatchRequest
            {
                TenantId = Guid.NewGuid(),
                OwnerKind = "Form",
                OwnerId = Guid.NewGuid(),
                TriggerKind = "SubWorkflow",
                Variables = null,
            },
            CancellationToken.None);

        Assert.Equal(WorkflowDispatchOutcome.NotConfigured, result.Outcome);
        Assert.False(runner.StartCalled);
    }

    // -----------------------------------------------------------------------
    // flush semantics — atomic RunWorkflow dispatch mode
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Default_flush_saves_as_a_self_contained_unit_of_work()
    {
        var run = MakeRun();
        var store = new FakeStore { Definition = MakeDefinition() };
        var dispatcher = new WorkflowDispatcher(store, new FakeRunner(run), Options.Create(new WorkflowEngineSettings()));

        var result = await dispatcher.DispatchAsync(TopLevelRequest(run.TenantId), CancellationToken.None);

        Assert.Equal(WorkflowDispatchOutcome.Started, result.Outcome);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Flush_false_stages_without_saving()
    {
        // The RunWorkflow action's mode: the child run must stay STAGED so it commits atomically
        // with the dispatching step's own transition in the worker's per-step flush — never
        // becoming claimable before the parent's state is durable.
        var run = MakeRun();
        var store = new FakeStore { Definition = MakeDefinition() };
        var dispatcher = new WorkflowDispatcher(store, new FakeRunner(run), Options.Create(new WorkflowEngineSettings()));

        var result = await dispatcher.DispatchAsync(TopLevelRequest(run.TenantId), CancellationToken.None, flush: false);

        Assert.Equal(WorkflowDispatchOutcome.Started, result.Outcome);
        Assert.Equal(0, store.SaveCount);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static WorkflowDispatchRequest TopLevelRequest(Guid tenantId) => new()
    {
        TenantId = tenantId,
        OwnerKind = "Form",
        OwnerId = Guid.NewGuid(),
        TriggerKind = "SubmissionCompleted",
        Variables = null,
    };

    private static WorkflowRunRecord MakeRun() => new()
    {
        TenantId = Guid.NewGuid(),
        DefinitionId = Guid.NewGuid(),
        TriggerKind = "SubWorkflow",
        StartedAt = DateTime.UtcNow,
        Status = WorkflowRunStatus.Running,
    };

    private static WorkflowDefinition MakeDefinition() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        OwnerKind = "Form",
        OwnerId = Guid.NewGuid(),
        TriggerKind = "SubWorkflow",
        Graph = new WorkflowGraph(),
    };
}
