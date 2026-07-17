using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
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
/// Worker behaviour around the single-port-per-step contract:
///  - successful step fires exactly the named edge (no auto-fire of any other declared port);
///  - Dead steps don't fan out;
///  - transient retries don't fan out either;
///  - <c>FinishRun</c>-style terminators flip the run Completed and stamp <c>ReturnValue</c>;
///  - suspends park the step Waiting and persist bookmarks on the same flow;
///  - <c>ForEach</c> iterates correctly across visits and surfaces non-array / cap-exceeded
///    inputs as non-transient OnError.
/// Ported from the origin project's suite; drives the REAL worker/fan-out via the internal
/// <c>ExecuteOneAsync</c> seam against in-memory fakes.
/// </summary>
public class WorkflowEngineWorkerTests
{
    private const string TestKind = "TestAction";

    /// <summary>Engine settings shared between worker and fan-out across all tests.</summary>
    private static readonly WorkflowEngineSettings WorkerSettings = new()
    {
        MaxAttempts = 5,
        BatchSize = 1,
        PollIntervalSeconds = 1,
        MaxStepsPerRun = 1000,
        MaxVisitsPerNode = 100,
        MaxLoopIterations = 25,
        BackoffSeconds = new[] { 1, 2, 5 },
    };

    [Fact]
    public async Task Success_fires_only_the_named_port_edge()
    {
        // Action returns "success" — the engine must walk only that edge, even if other edges
        // declared on the same node exist (e.g. the "extra" edge here).
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("success", "Success", ActionPortKind.Normal),
                new ActionPortDescriptor("extra", "Extra", ActionPortKind.Normal),
            },
            actionResult: ActionExecutionResult.OnPort("success"));

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Completed, step.Status);
        Assert.Equal("success", step.OutputPort);
        Assert.Single(store.AddedSteps);
        Assert.Equal("success", store.AddedSteps[0].TriggerPort);
    }

    [Fact]
    public async Task Dead_step_does_not_fan_out_at_all()
    {
        // Non-transient error → Dead on first attempt. No successor steps must be created.
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("success", "Success", ActionPortKind.Normal),
                new ActionPortDescriptor("error", "Error", ActionPortKind.Error),
            },
            actionResult: ActionExecutionResult.OnError("simulated failure", transient: false));

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Dead, step.Status);
        Assert.Null(step.OutputPort);
        Assert.Empty(store.AddedSteps);
    }

    [Fact]
    public async Task Transient_failure_below_max_attempts_retries_without_firing_edges()
    {
        // Transient error and we're still under MaxAttempts → step goes back to Pending. No
        // successor edges fire.
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("success", "Success", ActionPortKind.Normal),
                new ActionPortDescriptor("error", "Error", ActionPortKind.Error),
            },
            actionResult: ActionExecutionResult.OnError("transient blip", transient: true));

        // AttemptCount=1 means this is the first attempt — settings.MaxAttempts is 5.
        step.AttemptCount = 1;

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Pending, step.Status);
        Assert.Null(step.OutputPort);
        Assert.Empty(store.AddedSteps);
        Assert.Equal("transient blip", step.LastError);
    }

    // -----------------------------------------------------------------------
    // Retry checkpoint — outputs persisted across attempts of one step execution
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Transient_error_outputs_are_persisted_as_the_retry_checkpoint()
    {
        // A multi-side-effect action failed partway and reported what it already did — the
        // checkpoint must land on the step row so the next attempt can read it.
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("success", "Success", ActionPortKind.Normal),
                new ActionPortDescriptor("error", "Error", ActionPortKind.Error),
            },
            actionResult: ActionExecutionResult.OnError(
                "email send failed", outputs: new { dbRowId = 42, emailSent = false }, transient: true));

        step.AttemptCount = 1;

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Pending, step.Status);
        Assert.NotNull(step.Outputs);
        Assert.Equal(42, step.Outputs!.Value.GetProperty("dbRowId").GetInt32());
    }

    [Fact]
    public async Task Transient_error_without_outputs_keeps_the_previous_checkpoint()
    {
        // Attempt 2 crashed before producing a checkpoint — erasing attempt 1's progress record
        // would make attempt 3 redo completed side effects.
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("success", "Success", ActionPortKind.Normal),
                new ActionPortDescriptor("error", "Error", ActionPortKind.Error),
            },
            actionResult: ActionExecutionResult.OnError("crashed early", transient: true));

        step.AttemptCount = 2;
        step.Outputs = JsonSerializer.SerializeToElement(new { dbRowId = 42 }, WorkflowJsonOptions.Default);

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Pending, step.Status);
        Assert.Equal(42, step.Outputs!.Value.GetProperty("dbRowId").GetInt32());
    }

    [Fact]
    public async Task Next_attempt_receives_the_checkpoint_via_context()
    {
        // The replay half of the channel: whatever the row carries arrives in
        // ActionContext.PriorAttemptOutputs together with the attempt number.
        var capture = new ContextCaptureAction();
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdgesWithAction(capture);

        step.AttemptCount = 2;
        step.Outputs = JsonSerializer.SerializeToElement(new { dbRowId = 42 }, WorkflowJsonOptions.Default);

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.NotNull(capture.SeenPriorOutputs);
        Assert.Equal(42, capture.SeenPriorOutputs!.Value.GetProperty("dbRowId").GetInt32());
        Assert.Equal(2, capture.SeenAttemptCount);
    }

    [Fact]
    public async Task First_attempt_sees_no_checkpoint()
    {
        var capture = new ContextCaptureAction();
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdgesWithAction(capture);

        step.AttemptCount = 1;

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Null(capture.SeenPriorOutputs);
        Assert.Equal(1, capture.SeenAttemptCount);
    }

    // -----------------------------------------------------------------------
    // RetryExhaustedPort — fallback branch instead of dead-letter
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Transient_failure_with_fallback_port_still_retries_below_max_attempts()
    {
        // The fallback port only matters at exhaustion — while attempts remain, the step
        // retries exactly like a plain transient error, no edges fire.
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("success", "Success", ActionPortKind.Normal),
                new ActionPortDescriptor("error", "Error", ActionPortKind.Error),
            },
            actionResult: ActionExecutionResult.OnError(
                "transient blip", transient: true, retryExhaustedPort: "error"));

        step.AttemptCount = 1; // first of MaxAttempts=5

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Pending, step.Status);
        Assert.Null(step.OutputPort);
        Assert.Empty(store.AddedSteps);
    }

    [Fact]
    public async Task Exhausted_transient_failure_with_fallback_port_takes_the_branch()
    {
        // Attempts spent → instead of Dead, the step completes on the declared fallback port:
        // the run continues down that edge, the last attempt's outputs are stamped, and
        // LastError keeps the failure visible in the trace.
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("success", "Success", ActionPortKind.Normal),
                new ActionPortDescriptor("error", "Error", ActionPortKind.Error),
            },
            actionResult: ActionExecutionResult.OnError(
                "still failing",
                outputs: new { reason = "upstream_down" },
                transient: true,
                retryExhaustedPort: "error"));

        step.AttemptCount = 5; // == MaxAttempts → exhausted

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Completed, step.Status);
        Assert.Equal("error", step.OutputPort);
        Assert.Equal("still failing", step.LastError);
        Assert.Equal("upstream_down", step.Outputs!.Value.GetProperty("reason").GetString());

        var successor = Assert.Single(store.AddedSteps);
        Assert.Equal("error", successor.TriggerPort);
        Assert.Equal(step.Id, successor.PredecessorExecutionId);
    }

    [Fact]
    public async Task Non_transient_failure_with_fallback_port_takes_the_branch_immediately()
    {
        // Deterministic failure = exhausted at once: no retries are burned, the fallback
        // branch fires on the first attempt.
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("success", "Success", ActionPortKind.Normal),
                new ActionPortDescriptor("error", "Error", ActionPortKind.Error),
            },
            actionResult: ActionExecutionResult.OnError(
                "deterministic failure", transient: false, retryExhaustedPort: "error"));

        step.AttemptCount = 1;

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Completed, step.Status);
        Assert.Equal("error", step.OutputPort);
        var successor = Assert.Single(store.AddedSteps);
        Assert.Equal("error", successor.TriggerPort);
    }

    // -----------------------------------------------------------------------
    // ForEach
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ForEach_first_visit_with_array_fires_iterate_with_index_zero()
    {
        // First execution: no previous outputs, so cfg.Items is resolved to the array. Fire
        // "iterate" with index=0 + the first element.
        var resolvedItems = JsonElementArray("a", "b", "c");
        var ctx = ForEachContext(resolvedItems, previousOutputs: null);

        var action = new ForEachActionType(Options.Create(WorkerSettings));
        var result = await action.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("iterate", result.OutputPort);
        Assert.NotNull(result.Outputs);
        var outputs = ToJson(result.Outputs!);
        Assert.Equal(0, outputs.GetProperty("index").GetInt32());
        Assert.Equal("a", outputs.GetProperty("item").GetString());
        Assert.Equal(3, outputs.GetProperty("total").GetInt32());
        Assert.True(outputs.GetProperty("isFirst").GetBoolean());
        Assert.False(outputs.GetProperty("isLast").GetBoolean());
        Assert.Equal(1, outputs.GetProperty("nextIndex").GetInt32());
    }

    [Fact]
    public async Task ForEach_subsequent_visit_advances_index_and_uses_frozen_items()
    {
        // Previous outputs say nextIndex=2 and carry frozen items=[a,b,c]. Even though
        // cfg.Items is now resolved to a different array (simulating a non-deterministic
        // expression), the action sticks with the frozen value.
        var prevOutputs = JsonObject(
            ("index", 1),
            ("items", new[] { "a", "b", "c" }),
            ("total", 3),
            ("nextIndex", 2));
        var newResolvedItems = JsonElementArray("x", "y"); // different from frozen
        var ctx = ForEachContext(newResolvedItems, previousOutputs: prevOutputs);

        var action = new ForEachActionType(Options.Create(WorkerSettings));
        var result = await action.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("iterate", result.OutputPort);
        var outputs = ToJson(result.Outputs!);
        Assert.Equal(2, outputs.GetProperty("index").GetInt32());
        Assert.Equal("c", outputs.GetProperty("item").GetString());
        Assert.Equal(3, outputs.GetProperty("total").GetInt32());
        Assert.False(outputs.GetProperty("isFirst").GetBoolean());
        Assert.True(outputs.GetProperty("isLast").GetBoolean());
    }

    [Fact]
    public async Task ForEach_when_index_past_end_fires_done()
    {
        var prevOutputs = JsonObject(
            ("index", 2),
            ("items", new[] { "a", "b", "c" }),
            ("total", 3),
            ("nextIndex", 3)); // we're done
        var ctx = ForEachContext(JsonElementArray("a", "b", "c"), previousOutputs: prevOutputs);

        var action = new ForEachActionType(Options.Create(WorkerSettings));
        var result = await action.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("done", result.OutputPort);
        var outputs = ToJson(result.Outputs!);
        Assert.Equal(3, outputs.GetProperty("total").GetInt32());
        Assert.True(outputs.GetProperty("completed").GetBoolean());
    }

    [Fact]
    public async Task ForEach_empty_array_fires_done_immediately()
    {
        var ctx = ForEachContext(JsonElementArray(), previousOutputs: null);

        var action = new ForEachActionType(Options.Create(WorkerSettings));
        var result = await action.ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal("done", result.OutputPort);
    }

    [Fact]
    public async Task ForEach_non_array_input_surfaces_non_transient_error()
    {
        // Resolved cfg.Items is a string that isn't valid JSON array — should OnError.
        var ctx = ForEachContext(resolved: (object)"not an array", previousOutputs: null);

        var action = new ForEachActionType(Options.Create(WorkerSettings));
        var result = await action.ExecuteAsync(ctx, CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
        Assert.Null(result.OutputPort);
    }

    [Fact]
    public async Task ForEach_array_exceeds_loop_cap_surfaces_non_transient_error()
    {
        // Cap=25, give 26 — should OnError with the cap message.
        var settings = new WorkflowEngineSettings { MaxLoopIterations = 25 };
        var resolved = JsonElementArray(Enumerable.Range(0, 26).Select(i => i.ToString()).ToArray());
        var ctx = ForEachContext(resolved, previousOutputs: null);

        var action = new ForEachActionType(Options.Create(settings));
        var result = await action.ExecuteAsync(ctx, CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
        Assert.Contains("exceeds", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // FinishRun
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FinishRun_action_returns_terminates_run_with_resolved_payload()
    {
        var action = new FinishRunActionType();
        var ctx = new ActionContext<FinishRunConfig>
        {
            Config = new FinishRunConfig
            {
                ReturnValue = new Expr<object> { Engine = "static", Resolved = new { ok = true, n = 42 } },
            },
            RunId = Guid.NewGuid(),
            StepExecutionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            NodeKey = "finish_1",
            StepsOutputs = JsonDocument.Parse("{}").RootElement,
        };

        var result = await action.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(result.TerminatesRun);
        Assert.Null(result.OutputPort);
        Assert.NotNull(result.ReturnValue);
        var payload = ToJson(result.ReturnValue!);
        Assert.True(payload.GetProperty("ok").GetBoolean());
        Assert.Equal(42, payload.GetProperty("n").GetInt32());
    }

    [Fact]
    public async Task FinishRun_action_with_null_payload_still_terminates()
    {
        var action = new FinishRunActionType();
        var ctx = new ActionContext<FinishRunConfig>
        {
            Config = new FinishRunConfig(),
            RunId = Guid.NewGuid(),
            StepExecutionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            NodeKey = "finish_1",
            StepsOutputs = JsonDocument.Parse("{}").RootElement,
        };

        var result = await action.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(result.TerminatesRun);
        Assert.Null(result.ReturnValue);
    }

    [Fact]
    public async Task Worker_terminates_run_to_completed_and_stamps_return_value()
    {
        // TerminatesRun result → step Completed, run.Status=Completed, run.ReturnValue serialized,
        // no successor steps fired even if edges exist on the graph.
        var (worker, store, registry, builder, run, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("ignored1", "Ignored1", ActionPortKind.Normal),
                new ActionPortDescriptor("ignored2", "Ignored2", ActionPortKind.Normal),
            },
            actionResult: ActionExecutionResult.OnFinish(new { result = "ok", count = 7 }));

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Completed, step.Status);
        Assert.Null(step.OutputPort);
        Assert.NotNull(step.Outputs);
        Assert.Empty(store.AddedSteps); // No fan-out — run is terminal.

        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
        Assert.NotNull(run.FinishedAt);
        Assert.NotNull(run.ReturnValue);
        var payload = run.ReturnValue!.Value;
        Assert.Equal("ok", payload.GetProperty("result").GetString());
        Assert.Equal(7, payload.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Worker_terminates_run_with_null_return_value_leaves_run_return_value_null()
    {
        // FinishRun without a payload → run.ReturnValue stays null. Parent (if any) would later
        // see return_value: null in its resume payload.
        var (worker, store, registry, builder, run, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("a", "A", ActionPortKind.Normal),
                new ActionPortDescriptor("b", "B", ActionPortKind.Normal),
            },
            actionResult: ActionExecutionResult.OnFinish(returnValue: null));

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
        Assert.Null(run.ReturnValue);
        Assert.Empty(store.AddedSteps);
    }

    // -----------------------------------------------------------------------
    // Suspend with bookmark (generic signal-wait)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Suspend_with_bookmarks_parks_step_waiting_and_persists_bookmarks()
    {
        // Action suspends AND registers a bookmark. The worker must park the step Waiting and hand
        // the registrations to the store on the same flow as the step update.
        var registrations = new[] { new WorkflowBookmarkRegistration("submission:123", "signalled") };
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("signalled", "Signalled", ActionPortKind.Normal),
                new ActionPortDescriptor("timedOut", "Timed out", ActionPortKind.Error),
            },
            actionResult: ActionExecutionResult.OnSuspend(timeoutSeconds: 3600, bookmarks: registrations));

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Waiting, step.Status);
        Assert.Null(step.OutputPort);
        Assert.Empty(store.AddedSteps); // suspend fires no successor
        var (bookmarkedStep, persisted) = Assert.Single(store.AddedBookmarks);
        Assert.Equal(step.Id, bookmarkedStep.Id);
        Assert.Equal("submission:123", Assert.Single(persisted).CorrelationKey);
        Assert.Equal("signalled", persisted[0].ResumePort);
    }

    [Fact]
    public async Task Suspend_without_bookmarks_persists_none()
    {
        // A plain suspend (Approve / Delay) registers no bookmarks — the store's AddBookmarks must
        // not be invoked, so the bookmark table stays untouched.
        var (worker, store, registry, builder, _, step) = SetupSingleSourceTwoEdges(
            sourceKind: TestKind,
            ports: new[]
            {
                new ActionPortDescriptor("done", "Done", ActionPortKind.Normal),
                new ActionPortDescriptor("other", "Other", ActionPortKind.Normal),
            },
            actionResult: ActionExecutionResult.OnSuspend(timeoutSeconds: 60));

        var fanOut = MakeFanOut(store, builder, registry);
        await worker.ExecuteOneAsync(step, store, registry, fanOut, CancellationToken.None);

        Assert.Equal(StepExecutionStatus.Waiting, step.Status);
        Assert.Empty(store.AddedBookmarks);
    }

    // -----------------------------------------------------------------------
    // Delay
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Delay_action_suspends_with_configured_seconds()
    {
        // First (and only) execute: action parks the step until the deadline. The result is
        // a Suspend with the configured timeout — sweeper later fires OnTimeoutAsync to wake up.
        var action = new DelayActionType();
        var ctx = new ActionContext<DelayConfig>
        {
            Config = new DelayConfig { Seconds = new Expr<int?> { Engine = "static", Resolved = 90 } },
            RunId = Guid.NewGuid(),
            StepExecutionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            NodeKey = "delay_1",
            StepsOutputs = JsonDocument.Parse("{}").RootElement,
        };

        var result = await action.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(result.IsSuspended);
        Assert.Equal(90, result.SuspendTimeoutSeconds);
        Assert.NotNull(result.Outputs);
        var outputs = ToJson(result.Outputs!);
        Assert.Equal(90, outputs.GetProperty("waitSeconds").GetInt32());
        Assert.True(outputs.TryGetProperty("requestedAt", out _));
    }

    [Fact]
    public async Task Delay_action_on_timeout_fires_done_port()
    {
        // Sweeper's wake-up call: action returns the success port — for Delay the timer IS the
        // happy path, no fallback / error semantics.
        var action = new DelayActionType();
        var ctx = new ActionContext
        {
            Config = new DelayConfig { Seconds = new Expr<int?> { Engine = "static", Resolved = 5 } },
            RunId = Guid.NewGuid(),
            StepExecutionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            NodeKey = "delay_1",
            StepsOutputs = JsonDocument.Parse("{}").RootElement,
        };

        var result = await action.OnStepTimedOutAsync(ctx, CancellationToken.None);

        Assert.Equal("done", result.OutputPort);
        Assert.False(result.IsSuspended);
        Assert.Null(result.Error);
        Assert.NotNull(result.Outputs);
        var outputs = ToJson(result.Outputs!);
        Assert.True(outputs.TryGetProperty("firedAt", out _));
    }

    [Fact]
    public async Task Delay_action_with_unresolved_seconds_fails_non_transient()
    {
        // Expression resolved to null (or the field is absent) — there is no meaningful delay
        // to wait; fail loud instead of suspending forever / firing immediately.
        var action = new DelayActionType();
        var ctx = new ActionContext<DelayConfig>
        {
            Config = new DelayConfig { Seconds = new Expr<int?> { Engine = "static", Resolved = null } },
            RunId = Guid.NewGuid(),
            StepExecutionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            NodeKey = "delay_1",
            StepsOutputs = JsonDocument.Parse("{}").RootElement,
        };

        var result = await action.ExecuteAsync(ctx, CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
        Assert.False(result.IsSuspended);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public async Task Delay_action_with_non_positive_seconds_fails_non_transient(int seconds)
    {
        var action = new DelayActionType();
        var ctx = new ActionContext<DelayConfig>
        {
            Config = new DelayConfig { Seconds = new Expr<int?> { Engine = "static", Resolved = seconds } },
            RunId = Guid.NewGuid(),
            StepExecutionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            NodeKey = "delay_1",
            StepsOutputs = JsonDocument.Parse("{}").RootElement,
        };

        var result = await action.ExecuteAsync(ctx, CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
        Assert.False(result.IsSuspended);
        Assert.Null(result.OutputPort);
    }

    // -----------------------------------------------------------------------
    // Test harness
    // -----------------------------------------------------------------------

    // Empty provider — these worker-execute tests never trigger ResumeParentStepAsync (no
    // ParentStepId on any step), so the fan-out never resolves IWorkflowResumer from it.
    private static readonly IServiceProvider EmptyProvider = new ServiceCollection().BuildServiceProvider();

    private static WorkflowFanOut MakeFanOut(
        FakeStore store, FakeBuilder builder, FakeRegistry registry)
        => new(
            store,
            builder,
            Options.Create(WorkerSettings),
            EmptyProvider,
            NullLogger<WorkflowFanOut>.Instance);

    private static (WorkflowEngineWorker Worker, FakeStore Store, FakeRegistry Registry, FakeBuilder Builder,
        WorkflowRunRecord Run, WorkflowStepRecord Step) SetupSingleSourceTwoEdges(
        string sourceKind,
        IReadOnlyList<ActionPortDescriptor> ports,
        ActionExecutionResult actionResult)
    {
        var sourceNode = new WorkflowNode { Id = "src", Kind = sourceKind, Key = "src" };
        var firstTarget = new WorkflowNode { Id = "succ", Kind = sourceKind, Key = "succ" };
        var secondTarget = new WorkflowNode { Id = "alt", Kind = sourceKind, Key = "alt" };

        var graph = new WorkflowGraph
        {
            Nodes = { sourceNode, firstTarget, secondTarget },
            Edges =
            {
                new WorkflowEdge { From = new() { NodeId = "src", Port = ports[0].Id }, To = "succ" },
                new WorkflowEdge { From = new() { NodeId = "src", Port = ports[1].Id }, To = "alt" },
            },
            StartNodeId = "src",
        };

        return BuildHarness(graph, sourceNode, sourceKind, ports, actionResult);
    }

    private static readonly JsonElement EmptyJsonObject = JsonDocument.Parse("{}").RootElement;

    private static (WorkflowEngineWorker Worker, FakeStore Store, FakeRegistry Registry, FakeBuilder Builder,
        WorkflowRunRecord Run, WorkflowStepRecord Step) BuildHarness(
        WorkflowGraph graph,
        WorkflowNode sourceNode,
        string sourceKind,
        IReadOnlyList<ActionPortDescriptor> ports,
        ActionExecutionResult actionResult)
    {
        // Default(JsonElement) is Undefined and Utf8JsonWriter blows up on serialise — make sure
        // every node has an explicit (empty) config object before snapshotting the graph.
        foreach (var node in graph.Nodes)
        {
            if (node.Config.ValueKind == JsonValueKind.Undefined)
            {
                node.Config = EmptyJsonObject;
            }
        }

        var run = new WorkflowRunRecord
        {
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            TriggerKind = "Test",
            WorkflowSnapshot = JsonSerializer.Serialize(graph, WorkflowJsonOptions.Default),
            StaticContext = EmptyJsonObject,
            StepsOutputs = EmptyJsonObject,
            Status = WorkflowRunStatus.Running,
            StartedAt = DateTime.UtcNow,
        };

        var step = new WorkflowStepRecord
        {
            RunId = run.Id,
            TenantId = run.TenantId,
            NodeId = sourceNode.Id,
            Kind = sourceNode.Kind,
            ResolvedConfig = EmptyJsonObject,
            Status = StepExecutionStatus.Running,
            NextAttemptAt = DateTime.UtcNow,
        };

        var registry = new FakeRegistry(sourceKind, ports, actionResult);
        var store = new FakeStore(run);
        var builder = new FakeBuilder();

        var worker = new WorkflowEngineWorker(
            scopeFactory: null!,   // ExecuteOneAsync doesn't use it.
            lifetime: null!,       // ditto — only ExecuteAsync touches lifetime.
            workSignal: new WorkflowWorkSignal(), // only the idle wait in WorkerLoopAsync uses it.
            logger: NullLogger<WorkflowEngineWorker>.Instance,
            settings: Options.Create(WorkerSettings));

        return (worker, store, registry, builder, run, step);
    }

    /// <summary>Same single-source harness, but the registry wraps a REAL action instance.</summary>
    private static (WorkflowEngineWorker Worker, FakeStore Store, FakeRegistry Registry, FakeBuilder Builder,
        WorkflowRunRecord Run, WorkflowStepRecord Step) SetupSingleSourceTwoEdgesWithAction(IActionType action)
    {
        var sourceNode = new WorkflowNode { Id = "src", Kind = action.Kind, Key = "src", Config = EmptyJsonObject };
        var graph = new WorkflowGraph
        {
            Nodes =
            {
                sourceNode,
                new WorkflowNode { Id = "succ", Kind = action.Kind, Key = "succ", Config = EmptyJsonObject },
            },
            Edges =
            {
                new WorkflowEdge { From = new() { NodeId = "src", Port = action.OutputPorts[0].Id }, To = "succ" },
            },
            StartNodeId = "src",
        };

        var run = new WorkflowRunRecord
        {
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            TriggerKind = "Test",
            WorkflowSnapshot = JsonSerializer.Serialize(graph, WorkflowJsonOptions.Default),
            StaticContext = EmptyJsonObject,
            StepsOutputs = EmptyJsonObject,
            Status = WorkflowRunStatus.Running,
            StartedAt = DateTime.UtcNow,
        };

        var step = new WorkflowStepRecord
        {
            RunId = run.Id,
            TenantId = run.TenantId,
            NodeId = sourceNode.Id,
            Kind = action.Kind,
            ResolvedConfig = EmptyJsonObject,
            Status = StepExecutionStatus.Running,
            NextAttemptAt = DateTime.UtcNow,
        };

        var registry = new FakeRegistry(action);
        var store = new FakeStore(run);
        var builder = new FakeBuilder();

        var worker = new WorkflowEngineWorker(
            scopeFactory: null!,
            lifetime: null!,
            workSignal: new WorkflowWorkSignal(),
            logger: NullLogger<WorkflowEngineWorker>.Instance,
            settings: Options.Create(WorkerSettings));

        return (worker, store, registry, builder, run, step);
    }

    /// <summary>Captures the retry-checkpoint fields of the context it was dispatched with.</summary>
    private sealed class ContextCaptureAction : ActionType<object>
    {
        public JsonElement? SeenPriorOutputs { get; private set; }

        public int SeenAttemptCount { get; private set; }

        public override string Kind => "ContextCapture";

        public override string DisplayName => this.Kind;

        public override IReadOnlyList<ActionPortDescriptor> OutputPorts { get; } = new[]
        {
            new ActionPortDescriptor("success", "Success", ActionPortKind.Normal),
            new ActionPortDescriptor("error", "Error", ActionPortKind.Error),
        };

        public override Task<ActionExecutionResult> ExecuteAsync(
            ActionContext<object> context, CancellationToken cancellationToken)
        {
            this.SeenPriorOutputs = context.PriorAttemptOutputs;
            this.SeenAttemptCount = context.AttemptCount;
            return Task.FromResult(this.Port("success"));
        }
    }

    // ----- ForEach helpers -----

    private static ActionContext<ForEachConfig> ForEachContext(object? resolved, JsonElement? previousOutputs)
    {
        var stepsOutputs = previousOutputs is null
            ? JsonDocument.Parse("{}").RootElement
            : JsonDocument.Parse($"{{\"foreach_1\":{previousOutputs.Value.GetRawText()}}}").RootElement;

        return new ActionContext<ForEachConfig>
        {
            Config = new ForEachConfig { Items = new Expr<object> { Engine = "static", Resolved = resolved } },
            RunId = Guid.NewGuid(),
            StepExecutionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            NodeKey = "foreach_1",
            StepsOutputs = stepsOutputs,
        };
    }

    private static JsonElement JsonElementArray(params string[] items)
        => JsonDocument.Parse(JsonSerializer.Serialize(items)).RootElement;

    private static JsonElement JsonObject(params (string Key, object Value)[] kvs)
    {
        var dict = kvs.ToDictionary(kv => kv.Key, kv => kv.Value);
        return JsonDocument.Parse(JsonSerializer.Serialize(dict)).RootElement;
    }

    private static JsonElement ToJson(object outputs)
        => JsonDocument.Parse(JsonSerializer.Serialize(outputs)).RootElement;
}
