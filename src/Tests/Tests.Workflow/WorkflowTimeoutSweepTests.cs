using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Engine;
using LayeredTemplate.Plugins.Workflow.Engine.Actions;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// The engine timeout sweep (<c>WorkflowEngineWorker.SweepExpiredWaitingStepsOnceAsync</c>, the
/// maintenance-loop pass) routes each expired Waiting step through its action's
/// <c>OnStepTimedOutAsync</c> and applies the result. Proves the per-action timeout policy
/// end-to-end through the worker (claim → OnStepTimedOut → ApplyResult):
///  - a graceful-port action (Delay) → step Completed on its <c>done</c> port (the timer IS the
///    happy path);
///  - a no-override action (base default) → step Dead, non-transient (no timeout → retry →
///    re-suspend loop).
/// </summary>
public sealed class WorkflowTimeoutSweepTests
{
    private static readonly WorkflowEngineSettings Settings = new()
    {
        MaxAttempts = 5,
        BatchSize = 10,
        PollIntervalSeconds = 1,
        MaxStepsPerRun = 1000,
        MaxVisitsPerNode = 100,
        MaxLoopIterations = 25,
        BackoffSeconds = new[] { 1, 2, 5 },
    };

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement;

    [Fact]
    public async Task Sweep_fires_delay_done_port_on_timeout()
    {
        // Delay's timer IS the happy path — OnStepTimedOut returns "done", so the swept step Completes.
        var (worker, store, registry, step) = BuildSweep(new DelayActionType());

        await worker.SweepExpiredWaitingStepsOnceAsync(store, registry, MakeFanOut(store, registry), CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Completed, step.Status);
        Assert.Equal("done", step.OutputPort);
    }

    [Fact]
    public async Task Sweep_sends_a_no_override_action_dead_non_transient()
    {
        // A suspending action that never overrides OnStepTimedOut → the base default raises a
        // non-transient OnError, landing the swept step Dead (no port). Non-transient is
        // load-bearing: a transient default would loop timeout → retry → re-suspend.
        var (worker, store, registry, step) = BuildSweep(new NoTimeoutAction());

        await worker.SweepExpiredWaitingStepsOnceAsync(store, registry, MakeFanOut(store, registry), CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Dead, step.Status);
        Assert.Null(step.OutputPort);
        Assert.NotNull(step.LastError);
    }

    [Fact]
    public async Task Sweep_with_nothing_expired_is_a_no_op()
    {
        // Empty queue → the sweep claims nothing and touches nothing.
        var (worker, store, registry, step) = BuildSweep(new DelayActionType(), seedExpired: false);

        await worker.SweepExpiredWaitingStepsOnceAsync(store, registry, MakeFanOut(store, registry), CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Waiting, step.Status);
        Assert.Empty(store.UpdatedSteps);
    }

    // ── harness ─────────────────────────────────────────────────────────────

    private static (WorkflowEngineWorker Worker, FakeStore Store, FakeRegistry Registry, WorkflowStepRecord Step) BuildSweep(
        IActionType action,
        bool seedExpired = true)
    {
        var node = new WorkflowNode { Id = "n1", Key = "n1", Kind = action.Kind, Config = EmptyObject };
        var graph = new WorkflowGraph { Nodes = { node }, StartNodeId = "n1" };

        var run = new WorkflowRunRecord
        {
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            TriggerKind = "Test",
            WorkflowSnapshot = JsonSerializer.Serialize(graph, WorkflowJsonOptions.Default),
            StaticContext = EmptyObject,
            StepsOutputs = EmptyObject,
            Status = WorkflowRunStatus.Suspended,
            StartedAt = DateTime.UtcNow,
        };

        var step = new WorkflowStepRecord
        {
            RunId = run.Id,
            TenantId = run.TenantId,
            NodeId = "n1",
            Kind = action.Kind,
            ResolvedConfig = EmptyObject,
            Status = StepExecutionStatus.Waiting,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1), // expired
        };

        var registry = new FakeRegistry(action);
        var store = new FakeStore(run);
        if (seedExpired)
        {
            store.ExpiredWaitingSteps.Enqueue(step);
        }

        var worker = new WorkflowEngineWorker(
            scopeFactory: null!,
            lifetime: null!,
            logger: NullLogger<WorkflowEngineWorker>.Instance,
            settings: Options.Create(Settings));

        return (worker, store, registry, step);
    }

    private static WorkflowFanOut MakeFanOut(FakeStore store, FakeRegistry registry) => new(
        store,
        new FakeBuilder(),
        Options.Create(Settings),
        new ServiceCollection().BuildServiceProvider(),
        NullLogger<WorkflowFanOut>.Instance);

    /// <summary>A suspending action that never overrides the timeout hook — exercises the base default.</summary>
    private sealed class NoTimeoutAction : ActionType<object>
    {
        public override string Kind => "NoTimeout";

        public override string DisplayName => "No timeout";

        public override IReadOnlyList<ActionPortDescriptor> OutputPorts =>
            new[] { new ActionPortDescriptor("done", "Done", ActionPortKind.Normal) };

        public override Task<ActionExecutionResult> ExecuteAsync(ActionContext<object> context, CancellationToken ct)
            => Task.FromResult(this.Suspend());
    }
}
