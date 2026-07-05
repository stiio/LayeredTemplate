using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine;
using LayeredTemplate.Plugins.Workflow.Engine.Actions;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// The action step-lifecycle contract (<c>OnStepResumedAsync</c> / <c>OnStepTimedOutAsync</c>)
/// and the resume path that routes through it:
///  - the base defaults are loud non-transient OnError (a suspending action MUST override);
///  - a resumed action's chosen port + the seeded payload outputs land on the step exactly as
///    stamped by the atomic guard (pass-through Approve-style + fixed-port WaitForm-style);
///  - the sub-workflow parent-resume pass-through fires success / failed verbatim;
///  - the whole resume is ONE storage transaction: success commits it, post-guard failures
///    (undeclared action port, hook throwing) leave it uncommitted (rollback → step stays
///    Waiting in the real store), pre-guard failures never open one, and an ambient
///    transaction (chain unwind) is joined without a nested commit.
/// </summary>
public class WorkflowResumerStepLifecycleTests
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

    // -----------------------------------------------------------------------
    // Base defaults — fail loud, non-transient
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Default_on_step_resumed_returns_non_transient_on_error()
    {
        var action = new NoLifecycleAction();
        var result = await action.OnStepResumedAsync(
            NewContext(), payload: null, port: "whatever", CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
        Assert.Null(result.OutputPort);
        Assert.False(result.IsSuspended);
    }

    [Fact]
    public async Task Default_on_step_timed_out_returns_non_transient_on_error()
    {
        var action = new NoLifecycleAction();
        var result = await action.OnStepTimedOutAsync(NewContext(), CancellationToken.None);

        Assert.NotNull(result.Error);
        // Non-transient is load-bearing: a transient default would loop timeout → retry → re-suspend.
        Assert.False(result.IsTransient);
        Assert.Null(result.OutputPort);
        Assert.False(result.IsSuspended);
    }

    // -----------------------------------------------------------------------
    // Resume routes through OnStepResumed — port + outputs match the guard's stamp
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Pass_through_resume_stamps_caller_port_and_payload_object()
    {
        // Approve-style pass-through: the caller-chosen port wins, and an object payload is
        // flattened onto the step's outputs (steps.<key>.fieldName).
        var ports = new[]
        {
            new ActionPortDescriptor("approved", "Approved", ActionPortKind.Normal),
            new ActionPortDescriptor("rejected", "Rejected", ActionPortKind.Normal),
        };
        var action = new PassThroughAction("Approve", ports);
        var (resumer, store, step) = BuildResumer(action);

        var payload = JsonObject(("resumedBy", "user@x"), ("note", "looks good"));
        var result = await resumer.ResumeAsync(
            Command(step.RunId, step.Id, step.TenantId, "approved", payload),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(action.Resumed);
        Assert.Equal("approved", action.SeenPort);
        Assert.Equal(StepExecutionStatus.Completed, step.Status);
        Assert.Equal("approved", step.OutputPort);
        var outputs = step.Outputs!.Value;
        Assert.Equal("user@x", outputs.GetProperty("resumedBy").GetString());
        Assert.Equal("looks good", outputs.GetProperty("note").GetString());
    }

    [Fact]
    public async Task Fixed_port_resume_ignores_caller_port_when_action_pins_it()
    {
        // WaitForm-style fixed port: the action returns "submitted" regardless of the caller-supplied
        // port. (The pre-guard validation still checks the CALLER port — so the caller must pass a
        // declared port; here both "submitted" and "timedOut" are declared.)
        var ports = new[]
        {
            new ActionPortDescriptor("submitted", "Submitted", ActionPortKind.Normal),
            new ActionPortDescriptor("timedOut", "Timed out", ActionPortKind.Error),
        };
        var action = new FixedPortAction("WaitForm", ports, firedPort: "submitted");
        var (resumer, store, step) = BuildResumer(action);

        var payload = JsonObject(("answer", "42"));
        var result = await resumer.ResumeAsync(
            Command(step.RunId, step.Id, step.TenantId, port: "submitted", payload),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(action.Resumed);
        Assert.Equal("submitted", step.OutputPort);
        Assert.Equal("42", step.Outputs!.Value.GetProperty("answer").GetString());
    }

    [Fact]
    public async Task Resume_with_scalar_payload_keeps_it_under_value_key()
    {
        // Non-object payload preservation — the NormalizeOutputs contract: a scalar is stashed
        // under steps.<key>.value rather than silently dropped.
        var ports = new[] { new ActionPortDescriptor("done", "Done", ActionPortKind.Normal) };
        var action = new PassThroughAction("Pass", ports);
        var (resumer, store, step) = BuildResumer(action);

        var payload = JsonDocument.Parse("\"hello\"").RootElement;
        var result = await resumer.ResumeAsync(
            Command(step.RunId, step.Id, step.TenantId, "done", payload),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("hello", step.Outputs!.Value.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Resume_with_caller_port_not_declared_fails_invalid_port_step_stays_waiting()
    {
        // Pre-guard validation of the caller port: an undeclared port surfaces InvalidPort WITHOUT
        // winning the guard — the step is never flipped and no transaction is even opened.
        var ports = new[] { new ActionPortDescriptor("approved", "Approved", ActionPortKind.Normal) };
        var action = new PassThroughAction("Approve", ports);
        var (resumer, store, step) = BuildResumer(action);

        var result = await resumer.ResumeAsync(
            Command(step.RunId, step.Id, step.TenantId, port: "bogus", payload: null),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(WorkflowResumeFailureReason.InvalidPort, result.Reason);
        Assert.False(action.Resumed);                            // OnStepResumed never invoked
        Assert.Equal(StepExecutionStatus.Waiting, step.Status);  // guard never won
        Assert.Empty(store.Transactions);                        // pre-guard exit → no transaction
    }

    [Fact]
    public async Task Resume_of_already_resumed_step_is_step_not_waiting()
    {
        var ports = new[] { new ActionPortDescriptor("approved", "Approved", ActionPortKind.Normal) };
        var action = new PassThroughAction("Approve", ports);
        var (resumer, store, step) = BuildResumer(action);
        step.Status = StepExecutionStatus.Completed; // someone else already resumed it

        var result = await resumer.ResumeAsync(
            Command(step.RunId, step.Id, step.TenantId, "approved", payload: null),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(WorkflowResumeFailureReason.StepNotWaiting, result.Reason);
        Assert.False(action.Resumed);
        Assert.Empty(store.Transactions);
    }

    // -----------------------------------------------------------------------
    // Sub-workflow parent-resume pass-through
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunWorkflow_resume_is_pass_through_on_success_and_failed_ports()
    {
        // The RunWorkflow action's OnStepResumed echoes the port FanOut computes from the child's
        // terminal status. Direct unit assertion of the pass-through.
        var action = new RunWorkflowActionType(
            services: new ServiceCollection().BuildServiceProvider(),
            store: new FakeStore(NewRun()),
            logger: NullLogger<RunWorkflowActionType>.Instance);

        var summary = JsonObject(("childStatus", "completed"), ("returnValue", "ok"));
        var result = await action.OnStepResumedAsync(NewContext(), summary, port: "success", CancellationToken.None);
        Assert.Equal("success", result.OutputPort);

        var failed = await action.OnStepResumedAsync(NewContext(), summary, port: "failed", CancellationToken.None);
        Assert.Equal("failed", failed.OutputPort);
    }

    // -----------------------------------------------------------------------
    // Transaction semantics — resume is one atomic unit of work
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Successful_resume_commits_its_transaction_and_flushes_inside_it()
    {
        var ports = new[] { new ActionPortDescriptor("done", "Done", ActionPortKind.Normal) };
        var action = new PassThroughAction("Pass", ports);
        var (resumer, store, step) = BuildResumer(action);

        var result = await resumer.ResumeAsync(
            Command(step.RunId, step.Id, step.TenantId, "done", payload: null),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var tx = Assert.Single(store.Transactions);
        Assert.True(tx.Committed);
        Assert.True(tx.Disposed);
        Assert.Equal(1, store.SaveCount); // flush is unconditional and happens inside the transaction
    }

    [Fact]
    public async Task Action_returning_undeclared_port_leaves_the_transaction_uncommitted()
    {
        // Post-guard defensive failure: the action fires a port it never declared. The resumer
        // must NOT commit — the uncommitted transaction disposes into a rollback, so in the real
        // store the guard's Waiting → Completed flip is undone and the step stays resumable.
        var ports = new[] { new ActionPortDescriptor("done", "Done", ActionPortKind.Normal) };
        var action = new FixedPortAction("Rogue", ports, firedPort: "undeclared");
        var (resumer, store, step) = BuildResumer(action);

        var result = await resumer.ResumeAsync(
            Command(step.RunId, step.Id, step.TenantId, port: "done", payload: null),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(WorkflowResumeFailureReason.InvalidPort, result.Reason);
        var tx = Assert.Single(store.Transactions);
        Assert.False(tx.Committed);
        Assert.True(tx.Disposed);
        Assert.Equal(0, store.SaveCount); // nothing flushed on the failure path
    }

    [Fact]
    public async Task Action_hook_throwing_leaves_the_transaction_uncommitted()
    {
        // OnStepResumedAsync throws → the exception propagates to the caller AND the transaction
        // disposes uncommitted (rollback). Pre-transaction behaviour wedged the step as
        // Completed-without-port; now the real store rolls the guard back to Waiting.
        var ports = new[] { new ActionPortDescriptor("done", "Done", ActionPortKind.Normal) };
        var action = new ThrowingResumeAction("Boom", ports);
        var (resumer, store, step) = BuildResumer(action);

        await Assert.ThrowsAsync<InvalidOperationException>(() => resumer.ResumeAsync(
            Command(step.RunId, step.Id, step.TenantId, "done", payload: null),
            CancellationToken.None));

        var tx = Assert.Single(store.Transactions);
        Assert.False(tx.Committed);
        Assert.True(tx.Disposed);
    }

    [Fact]
    public async Task Resume_inside_ambient_transaction_participates_without_committing()
    {
        // Chain-unwind mode: BeginTransactionAsync reports an ambient transaction (null handle).
        // The resume must still stage + flush, but the commit belongs to the outermost owner.
        var ports = new[] { new ActionPortDescriptor("done", "Done", ActionPortKind.Normal) };
        var action = new PassThroughAction("Pass", ports);
        var (resumer, store, step) = BuildResumer(action);
        store.SimulateAmbientTransaction = true;

        var result = await resumer.ResumeAsync(
            Command(step.RunId, step.Id, step.TenantId, "done", payload: null),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(store.Transactions);  // participated — never opened its own
        Assert.Equal(1, store.SaveCount);  // but still flushed into the ambient transaction
        Assert.Equal(StepExecutionStatus.Completed, step.Status);
    }

    // -----------------------------------------------------------------------
    // Helpers + fakes
    // -----------------------------------------------------------------------

    private static (WorkflowResumer Resumer, FakeStore Store, WorkflowStepRecord Step) BuildResumer(IActionType action)
    {
        // Single-node graph so the fan-out has a snapshot to parse (no successor edges — the resume
        // just completes the waiting step; we assert the step stamp, not enqueue).
        var node = new WorkflowNode { Id = "n1", Key = "n1", Kind = action.Kind, Config = EmptyObject };
        var graph = new WorkflowGraph { Nodes = { node }, StartNodeId = "n1" };

        var run = NewRun(JsonSerializer.Serialize(graph, WorkflowJsonOptions.Default));

        var step = new WorkflowStepRecord
        {
            RunId = run.Id,
            TenantId = run.TenantId,
            NodeId = "n1",
            Kind = action.Kind,
            ResolvedConfig = EmptyObject,
            Status = StepExecutionStatus.Waiting,
            NextAttemptAt = DateTime.MaxValue,
        };

        var store = new FakeStore(run);
        store.Steps.Add(step);

        var registry = new FakeRegistry(action);
        var fanOut = new WorkflowFanOut(
            store, new FakeBuilder(), Options.Create(Settings),
            new ServiceCollection().BuildServiceProvider(), NullLogger<WorkflowFanOut>.Instance);
        // Engine-less resolver: lifecycle configs here carry no transient fields, so the
        // execute-time transient pass is a pure reflection walk that never hits an engine.
        var resumer = new WorkflowResumer(
            store, fanOut, registry,
            new ExpressionResolver(Enumerable.Empty<IExpressionEngine>()),
            NullLogger<WorkflowResumer>.Instance);
        return (resumer, store, step);
    }

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement;

    private static WorkflowRunRecord NewRun(string snapshot = "{}") => new()
    {
        TenantId = Guid.NewGuid(),
        DefinitionId = Guid.NewGuid(),
        TriggerKind = "Test",
        WorkflowSnapshot = snapshot,
        StaticContext = EmptyObject,
        StepsOutputs = EmptyObject,
        Status = WorkflowRunStatus.Suspended,
        StartedAt = DateTime.UtcNow,
    };

    private static ActionContext NewContext() => new()
    {
        Config = new object(),
        RunId = Guid.NewGuid(),
        StepExecutionId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        DefinitionId = Guid.NewGuid(),
        NodeKey = "n1",
        StepsOutputs = EmptyObject,
    };

    private static WorkflowResumeCommand Command(
        Guid runId, Guid stepId, Guid tenantId, string port, JsonElement? payload) => new()
    {
        RunId = runId,
        StepId = stepId,
        TenantId = tenantId,
        Port = port,
        Payload = payload,
    };

    private static JsonElement JsonObject(params (string Key, object Value)[] kvs)
        => JsonDocument.Parse(JsonSerializer.Serialize(kvs.ToDictionary(kv => kv.Key, kv => kv.Value))).RootElement;

    /// <summary>A non-suspending action that never overrides the lifecycle hooks — exercises the base defaults.</summary>
    private sealed class NoLifecycleAction : ActionType<object>
    {
        public override string Kind => "NoLifecycle";

        public override string DisplayName => "No lifecycle";

        public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Array.Empty<ActionPortDescriptor>();

        public override Task<ActionExecutionResult> ExecuteAsync(ActionContext<object> context, CancellationToken ct)
            => Task.FromResult(this.Suspend());
    }

    /// <summary>Echoes the caller-supplied port + payload — Approve / RunWorkflow-wait shape.</summary>
    private sealed class PassThroughAction : ActionType<object>
    {
        public PassThroughAction(string kind, IReadOnlyList<ActionPortDescriptor> ports)
        {
            this.Kind = kind;
            this.OutputPorts = ports;
        }

        public override string Kind { get; }

        public override string DisplayName => this.Kind;

        public override IReadOnlyList<ActionPortDescriptor> OutputPorts { get; }

        public bool Resumed { get; private set; }

        public string? SeenPort { get; private set; }

        public override Task<ActionExecutionResult> ExecuteAsync(ActionContext<object> context, CancellationToken ct)
            => Task.FromResult(this.Suspend());

        public override Task<ActionExecutionResult> OnStepResumedAsync(
            ActionContext context, JsonElement? payload, string? port, CancellationToken ct)
        {
            this.Resumed = true;
            this.SeenPort = port;
            return Task.FromResult(ActionExecutionResult.OnPort(port ?? "approved", payload));
        }
    }

    /// <summary>Always fires a fixed port, ignoring the caller's — WaitForm / task-action shape.</summary>
    private sealed class FixedPortAction : ActionType<object>
    {
        private readonly string firedPort;

        public FixedPortAction(string kind, IReadOnlyList<ActionPortDescriptor> ports, string firedPort)
        {
            this.Kind = kind;
            this.OutputPorts = ports;
            this.firedPort = firedPort;
        }

        public override string Kind { get; }

        public override string DisplayName => this.Kind;

        public override IReadOnlyList<ActionPortDescriptor> OutputPorts { get; }

        public bool Resumed { get; private set; }

        public override Task<ActionExecutionResult> ExecuteAsync(ActionContext<object> context, CancellationToken ct)
            => Task.FromResult(this.Suspend());

        public override Task<ActionExecutionResult> OnStepResumedAsync(
            ActionContext context, JsonElement? payload, string? port, CancellationToken ct)
        {
            this.Resumed = true;
            return Task.FromResult(ActionExecutionResult.OnPort(this.firedPort, payload));
        }
    }

    /// <summary>Resume hook that blows up — exercises the transaction rollback on hook exceptions.</summary>
    private sealed class ThrowingResumeAction : ActionType<object>
    {
        public ThrowingResumeAction(string kind, IReadOnlyList<ActionPortDescriptor> ports)
        {
            this.Kind = kind;
            this.OutputPorts = ports;
        }

        public override string Kind { get; }

        public override string DisplayName => this.Kind;

        public override IReadOnlyList<ActionPortDescriptor> OutputPorts { get; }

        public override Task<ActionExecutionResult> ExecuteAsync(ActionContext<object> context, CancellationToken ct)
            => Task.FromResult(this.Suspend());

        public override Task<ActionExecutionResult> OnStepResumedAsync(
            ActionContext context, JsonElement? payload, string? port, CancellationToken ct)
            => throw new InvalidOperationException("resume hook exploded");
    }
}
