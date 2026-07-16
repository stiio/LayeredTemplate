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
            workSignal: new WorkflowWorkSignal(),
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

    // -----------------------------------------------------------------------
    // Expired-step revert — compensating write when timeout handling fails
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Failed_timeout_handling_parks_step_back_to_waiting_with_backoff()
    {
        // The sweep's claim flipped the step to Running; the handler then failed. Without the
        // revert the row would be stuck forever (run is Suspended → stale-fail skips it, no
        // claim path touches 'running'). The revert re-parks it Waiting with an attempt counted
        // so the next maintenance pass retries the timeout.
        var (worker, store, _, step) = BuildSweep(new DelayActionType(), seedExpired: false);
        step.Status = StepExecutionStatus.Running; // as the claim left it
        step.AttemptCount = 0;
        store.Steps.Add(step);

        await worker.RevertExpiredStepCoreAsync(step.Id, "db blip", store, MakeFanOut(store, new FakeRegistry()));

        Assert.Equal(StepExecutionStatus.Waiting, step.Status);
        Assert.Equal(1, step.AttemptCount);
        Assert.True(step.NextAttemptAt > DateTime.UtcNow, "retry must be scheduled with backoff");
        Assert.Contains("db blip", step.LastError);
    }

    [Fact]
    public async Task Failed_timeout_handling_dead_letters_at_max_attempts()
    {
        var (worker, store, _, step) = BuildSweep(new DelayActionType(), seedExpired: false);
        step.Status = StepExecutionStatus.Running;
        step.AttemptCount = Settings.MaxAttempts - 1; // this failure is the last allowed
        store.Steps.Add(step);

        await worker.RevertExpiredStepCoreAsync(step.Id, "still broken", store, MakeFanOut(store, new FakeRegistry()));

        Assert.Equal(StepExecutionStatus.Dead, step.Status);
        Assert.NotNull(step.CompletedAt);
        Assert.Contains("still broken", step.LastError);
    }

    [Fact]
    public async Task Shutdown_interruption_reparks_immediately_without_consuming_an_attempt()
    {
        var (worker, store, _, step) = BuildSweep(new DelayActionType(), seedExpired: false);
        step.Status = StepExecutionStatus.Running;
        step.AttemptCount = 2;
        store.Steps.Add(step);

        await worker.RevertExpiredStepCoreAsync(step.Id, failure: null, store, MakeFanOut(store, new FakeRegistry()));

        Assert.Equal(StepExecutionStatus.Waiting, step.Status);
        Assert.Equal(2, step.AttemptCount); // a deploy is not a handler failure
        Assert.True(step.NextAttemptAt <= DateTime.UtcNow, "shutdown re-park retries immediately");
    }

    [Fact]
    public async Task Cancellation_mid_batch_reverts_unprocessed_claimed_steps()
    {
        // Two expired steps claimed in one pass; handling the first triggers shutdown. The
        // second was already flipped to Running by the claim — without the batch revert it
        // would be stuck forever (run Suspended → stale-fail skips it). The remainder pass
        // must park it back to Waiting untouched, while the first keeps its real outcome.
        var cts = new CancellationTokenSource();
        var action = new CancelOnTimeoutAction(cts);
        var (worker, store, registry, step1) = BuildSweep(action);
        var step2 = new WorkflowStepRecord
        {
            RunId = step1.RunId,
            TenantId = step1.TenantId,
            NodeId = "n1",
            Kind = action.Kind,
            ResolvedConfig = EmptyObject,
            Status = StepExecutionStatus.Waiting,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
        };
        store.ExpiredWaitingSteps.Enqueue(step2);

        await worker.SweepExpiredWaitingStepsOnceAsync(store, registry, MakeFanOut(store, registry), cts.Token);

        Assert.Equal(StepExecutionStatus.Completed, step1.Status); // finished before the signal
        Assert.Equal(StepExecutionStatus.Waiting, step2.Status);   // reverted, not wedged in Running
        Assert.Equal(0, step2.AttemptCount);                       // shutdown consumes no attempt
        Assert.True(step2.NextAttemptAt <= DateTime.UtcNow, "reverted step retries on the next sweep");
    }

    [Fact]
    public async Task Revert_leaves_steps_that_already_progressed_untouched()
    {
        // Another path (operator resume, concurrent finalize) moved the step on between the
        // failure and the revert — the guard must not clobber the newer state.
        var (worker, store, _, step) = BuildSweep(new DelayActionType(), seedExpired: false);
        step.Status = StepExecutionStatus.Completed;
        step.AttemptCount = 1;
        store.Steps.Add(step);

        await worker.RevertExpiredStepCoreAsync(step.Id, "late failure", store, MakeFanOut(store, new FakeRegistry()));

        Assert.Equal(StepExecutionStatus.Completed, step.Status);
        Assert.Equal(1, step.AttemptCount);
    }

    /// <summary>
    /// Fires its <c>done</c> port on timeout but ALSO signals shutdown — simulates the stop
    /// token firing while a sweep batch is mid-flight.
    /// </summary>
    private sealed class CancelOnTimeoutAction : ActionType<object>
    {
        private readonly CancellationTokenSource cts;

        public CancelOnTimeoutAction(CancellationTokenSource cts) => this.cts = cts;

        public override string Kind => "CancelOnTimeout";

        public override string DisplayName => this.Kind;

        public override IReadOnlyList<ActionPortDescriptor> OutputPorts { get; } = new[]
        {
            new ActionPortDescriptor("done", "Done", ActionPortKind.Normal),
        };

        public override Task<ActionExecutionResult> ExecuteAsync(
            ActionContext<object> context, CancellationToken cancellationToken)
            => Task.FromResult(this.Suspend());

        public override Task<ActionExecutionResult> OnStepTimedOutAsync(
            ActionContext context, CancellationToken cancellationToken)
        {
            this.cts.Cancel();
            return Task.FromResult(this.Port("done"));
        }
    }

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
