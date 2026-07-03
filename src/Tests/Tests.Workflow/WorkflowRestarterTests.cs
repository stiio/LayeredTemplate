using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Restart semantics: tenant-checked NotFound, mode dispatch (snapshot vs current definition),
/// graph resolution failures (snapshot malformed, definition gone), top-level reset on the
/// new run (NestingLevel=0, parent fields cleared), variables extracted from old static_context.
/// </summary>
public class WorkflowRestarterTests
{
    [Fact]
    public async Task Returns_not_found_when_run_missing()
    {
        var store = new FakeStore();
        var runner = new FakeRunner();
        var restarter = new WorkflowRestarter(store, runner, NullLogger<WorkflowRestarter>.Instance);

        var result = await restarter.RestartAsync(
            new WorkflowRestartCommand { RunId = Guid.NewGuid(), TenantId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Equal(WorkflowRestartOutcome.NotFound, result.Outcome);
        Assert.False(runner.StartCalled);
    }

    [Fact]
    public async Task Returns_not_found_when_tenant_mismatches()
    {
        var run = MakeOldRun(snapshot: GoodSnapshot());
        var store = new FakeStore(run);
        var runner = new FakeRunner();
        var restarter = new WorkflowRestarter(store, runner, NullLogger<WorkflowRestarter>.Instance);

        var result = await restarter.RestartAsync(
            new WorkflowRestartCommand { RunId = run.Id, TenantId = Guid.NewGuid() }, // different
            CancellationToken.None);

        Assert.Equal(WorkflowRestartOutcome.NotFound, result.Outcome);
        Assert.False(runner.StartCalled);
    }

    [Fact]
    public async Task Snapshot_mode_uses_frozen_workflow_snapshot()
    {
        var run = MakeOldRun(snapshot: GoodSnapshot());
        var store = new FakeStore(run);
        var newRun = new WorkflowRunRecord { TenantId = run.TenantId };
        var runner = new FakeRunner(newRun);
        var restarter = new WorkflowRestarter(store, runner, NullLogger<WorkflowRestarter>.Instance);

        var result = await restarter.RestartAsync(
            new WorkflowRestartCommand
            {
                RunId = run.Id,
                TenantId = run.TenantId,
                Mode = WorkflowRestartMode.UseSnapshot,
            },
            CancellationToken.None);

        Assert.Equal(WorkflowRestartOutcome.Started, result.Outcome);
        Assert.Equal(newRun.Id, result.NewRunId);
        Assert.Equal(run.Id, result.OldRunId);
        Assert.False(store.GetDefinitionByIdCalled); // snapshot path doesn't fetch live def
        Assert.NotNull(runner.LastDefinition);
        Assert.Equal(run.DefinitionId, runner.LastDefinition!.Id);
        Assert.Equal("start", runner.LastDefinition!.Graph.StartNodeId); // came from snapshot
        Assert.Equal(1, store.SaveCount); // restart is a self-contained unit of work
    }

    [Fact]
    public async Task Snapshot_mode_returns_malformed_when_snapshot_isnt_json()
    {
        var run = MakeOldRun(snapshot: "this is not json {{{ <broken>");
        var store = new FakeStore(run);
        var runner = new FakeRunner();
        var restarter = new WorkflowRestarter(store, runner, NullLogger<WorkflowRestarter>.Instance);

        var result = await restarter.RestartAsync(
            new WorkflowRestartCommand { RunId = run.Id, TenantId = run.TenantId },
            CancellationToken.None);

        Assert.Equal(WorkflowRestartOutcome.SnapshotMalformed, result.Outcome);
        Assert.False(runner.StartCalled);
    }

    [Fact]
    public async Task Current_definition_mode_loads_live_definition()
    {
        var run = MakeOldRun(snapshot: GoodSnapshot());
        // Live definition has a different start node — proves we used the live one, not snapshot.
        var liveDef = new WorkflowDefinition
        {
            Id = run.DefinitionId,
            TenantId = run.TenantId,
            OwnerKind = "Form",
            OwnerId = Guid.NewGuid(),
            TriggerKind = "SubmissionCompleted",
            Graph = new WorkflowGraph
            {
                Nodes = { new WorkflowNode { Id = "live_start", Kind = "Transform", Key = "live_start" } },
                StartNodeId = "live_start",
            },
        };
        var store = new FakeStore(run) { LiveDefinition = liveDef };
        var newRun = new WorkflowRunRecord { TenantId = run.TenantId };
        var runner = new FakeRunner(newRun);
        var restarter = new WorkflowRestarter(store, runner, NullLogger<WorkflowRestarter>.Instance);

        var result = await restarter.RestartAsync(
            new WorkflowRestartCommand
            {
                RunId = run.Id,
                TenantId = run.TenantId,
                Mode = WorkflowRestartMode.UseCurrentDefinition,
            },
            CancellationToken.None);

        Assert.Equal(WorkflowRestartOutcome.Started, result.Outcome);
        Assert.True(store.GetDefinitionByIdCalled);
        Assert.Equal("live_start", runner.LastDefinition!.Graph.StartNodeId);
    }

    [Fact]
    public async Task Current_definition_mode_returns_definition_gone_when_deleted()
    {
        var run = MakeOldRun(snapshot: GoodSnapshot());
        var store = new FakeStore(run); // LiveDefinition not seeded — definition was deleted
        var runner = new FakeRunner();
        var restarter = new WorkflowRestarter(store, runner, NullLogger<WorkflowRestarter>.Instance);

        var result = await restarter.RestartAsync(
            new WorkflowRestartCommand
            {
                RunId = run.Id,
                TenantId = run.TenantId,
                Mode = WorkflowRestartMode.UseCurrentDefinition,
            },
            CancellationToken.None);

        Assert.Equal(WorkflowRestartOutcome.DefinitionGone, result.Outcome);
        Assert.False(runner.StartCalled);
    }

    [Fact]
    public async Task New_run_is_top_level_regardless_of_old_runs_parentage()
    {
        // Old run was a sub-workflow child (NestingLevel=2, ParentRunId set). Restart must
        // create a top-level run regardless — the original parent has long since moved on.
        var run = MakeOldRun(snapshot: GoodSnapshot(), nestingLevel: 2,
            parentRunId: Guid.NewGuid(), parentStepId: Guid.NewGuid());

        var store = new FakeStore(run);
        var newRun = new WorkflowRunRecord { TenantId = run.TenantId };
        var runner = new FakeRunner(newRun);
        var restarter = new WorkflowRestarter(store, runner, NullLogger<WorkflowRestarter>.Instance);

        await restarter.RestartAsync(
            new WorkflowRestartCommand { RunId = run.Id, TenantId = run.TenantId },
            CancellationToken.None);

        Assert.NotNull(runner.LastIntent);
        Assert.Equal(0, runner.LastIntent!.NestingLevel);
        Assert.Null(runner.LastIntent.ParentRunId);
        Assert.Null(runner.LastIntent.ParentStepId);
    }

    [Fact]
    public async Task Variables_carried_forward_from_vars_namespace()
    {
        // static_context has { trigger, vars }. Restarter pulls everything under `vars` back
        // into the new intent's Variables; `trigger` is engine-owned and re-emitted by the
        // Runner from intent fields, so it's never carried forward.
        var staticContext = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["trigger"] = new Dictionary<string, object?> { ["kind"] = "stale" },
            ["vars"] = new Dictionary<string, object?>
            {
                ["answers"] = new Dictionary<string, object?> { ["email"] = "x@y.com" },
                ["meta"] = new Dictionary<string, object?> { ["foo"] = "bar" },
            },
        });
        var run = MakeOldRun(snapshot: GoodSnapshot(), staticContext: staticContext);

        var store = new FakeStore(run);
        var runner = new FakeRunner(new WorkflowRunRecord { TenantId = run.TenantId });
        var restarter = new WorkflowRestarter(store, runner, NullLogger<WorkflowRestarter>.Instance);

        await restarter.RestartAsync(
            new WorkflowRestartCommand { RunId = run.Id, TenantId = run.TenantId },
            CancellationToken.None);

        Assert.NotNull(runner.LastIntent);
        var vars = runner.LastIntent!.Variables;
        Assert.NotNull(vars);
        Assert.Equal(JsonValueKind.Object, vars!.Value.ValueKind);
        Assert.True(vars.Value.TryGetProperty("answers", out _));
        Assert.True(vars.Value.TryGetProperty("meta", out _));
        // `trigger` lives in its own namespace; it never enters Variables (which maps to `vars`).
        Assert.False(vars.Value.TryGetProperty("trigger", out _));
    }

    [Fact]
    public async Task Returns_empty_graph_when_runner_returns_null()
    {
        var run = MakeOldRun(snapshot: GoodSnapshot());
        var store = new FakeStore(run);
        var runner = new FakeRunner(returnRun: null);  // runner refused (e.g. no start node)
        var restarter = new WorkflowRestarter(store, runner, NullLogger<WorkflowRestarter>.Instance);

        var result = await restarter.RestartAsync(
            new WorkflowRestartCommand { RunId = run.Id, TenantId = run.TenantId },
            CancellationToken.None);

        Assert.Equal(WorkflowRestartOutcome.EmptyGraph, result.Outcome);
    }

    // ----- Helpers -----

    private static readonly JsonElement EmptyObject = JsonSerializer.SerializeToElement(new { });

    private static WorkflowRunRecord MakeOldRun(
        string snapshot,
        string staticContext = "{}",
        int nestingLevel = 0,
        Guid? parentRunId = null,
        Guid? parentStepId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        DefinitionId = Guid.NewGuid(),
        TriggerKind = "SubmissionCompleted",
        TriggerSourceKind = "Submission",
        TriggerSourceId = Guid.NewGuid(),
        IsDryRun = false,
        ActorUserId = Guid.NewGuid(),
        WorkflowSnapshot = snapshot,
        StaticContext = JsonDocument.Parse(staticContext).RootElement.Clone(),
        StepsOutputs = EmptyObject,
        Status = WorkflowRunStatus.Failed,
        StartedAt = DateTime.UtcNow,
        NestingLevel = nestingLevel,
        ParentRunId = parentRunId,
        ParentStepId = parentStepId,
    };

    private static string GoodSnapshot() => JsonSerializer.Serialize(new
    {
        nodes = new[] { new { id = "start", kind = "Transform", key = "start" } },
        edges = Array.Empty<object>(),
        startNodeId = "start",
    });
}
