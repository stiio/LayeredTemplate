using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions.Engines;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Two-phase resolution of transient config fields — the mechanism that keeps secrets and heavy
/// payloads (base64 files) out of the database:
///   - wire format: the `transient` flag round-trips; a transient resolved value can NEVER be
///     serialized (belt-and-braces converter guard)
///   - build phase (ResolveConfigAsync): non-transient leaves resolve, transient are skipped
///   - execute phase (ResolveTransientAsync): ONLY transient leaves resolve, persisted values
///     stay untouched; [TransientExpr] forces transiency over the whole property subtree
///   - StepExecutionBuilder persists the transient field as its raw expression (no `resolved`)
///     and rejects oversized resolved configs (MaxResolvedConfigChars guardrail)
///   - step executor: transient fields materialise just before the action runs; a resolution
///     failure is a retryable step error, not an instant Dead.
/// </summary>
public class TransientExprTests
{
    // ----- wire format -----

    [Fact]
    public void Transient_flag_round_trips_through_wire_format()
    {
        var expr = JsonSerializer.Deserialize<Expr<string>>(
            """{"engine":"static","value":"s3cr3t","transient":true}""", WorkflowJsonOptions.Default)!;

        Assert.True(expr.Transient);
        Assert.Equal("s3cr3t", expr.Value);

        var back = JsonSerializer.Serialize(expr, WorkflowJsonOptions.Default);
        Assert.Contains("\"transient\":true", back);
    }

    [Fact]
    public void Absent_transient_key_means_false()
    {
        var expr = JsonSerializer.Deserialize<Expr<string>>(
            """{"engine":"static","value":"x"}""", WorkflowJsonOptions.Default)!;
        Assert.False(expr.Transient);
    }

    [Fact]
    public void Transient_resolved_value_is_never_serialized()
    {
        var expr = new Expr<string> { Value = "raw", Transient = true, Resolved = "MATERIALISED" };
        var json = JsonSerializer.Serialize(expr, WorkflowJsonOptions.Default);

        Assert.DoesNotContain("MATERIALISED", json);
        Assert.DoesNotContain("resolved", json);

        // Control: a non-transient resolved value keeps serializing (that's the audit record).
        var plain = new Expr<string> { Value = "raw", Resolved = "VISIBLE" };
        Assert.Contains("VISIBLE", JsonSerializer.Serialize(plain, WorkflowJsonOptions.Default));
    }

    // ----- resolver phases -----

    [Fact]
    public async Task Build_phase_resolves_plain_and_skips_transient()
    {
        var config = JsonSerializer.Deserialize<JsonElement>("""
            {
              "plain": {"engine":"static","value":"hello"},
              "secret": {"engine":"static","value":"s3cr3t","transient":true},
              "forcedSecret": {"engine":"static","value":"forced"}
            }
            """);

        var resolved = (TransientTestConfig)await NewResolver().ResolveConfigAsync(
            config, typeof(TransientTestConfig), new Dictionary<string, object?>(), NewContext(), CancellationToken.None);

        Assert.Equal("hello", resolved.Plain.Resolved);
        Assert.Null(resolved.Secret.Resolved);           // instance flag
        Assert.Null(resolved.ForcedSecret!.Resolved);    // [TransientExpr] — flag not needed on the wire
    }

    [Fact]
    public async Task Execute_phase_resolves_only_transient_and_keeps_persisted_values()
    {
        var config = new TransientTestConfig
        {
            // Simulates a build-phase artifact loaded from resolved_config: the persisted value
            // is the audit record and must NOT be re-evaluated at execute time.
            Plain = new() { Value = "would-give-this", Resolved = "persisted" },
            Secret = new() { Value = "s3cr3t", Transient = true },
            ForcedSecret = new() { Value = "forced" },
            Items = new() { new() { Value = new() { Value = "item-secret" } } },
        };

        await NewResolver().ResolveTransientAsync(
            config, () => new Dictionary<string, object?>(), NewContext(), CancellationToken.None);

        Assert.Equal("persisted", config.Plain.Resolved);
        Assert.Equal("s3cr3t", config.Secret.Resolved);
        Assert.Equal("forced", config.ForcedSecret!.Resolved);
        // The attribute covers the property's whole subtree — list items included.
        Assert.Equal("item-secret", config.Items![0].Value.Resolved);
    }

    [Fact]
    public async Task Model_factory_is_not_invoked_when_config_has_no_transient_leaves()
    {
        var config = new TransientTestConfig
        {
            Plain = new() { Value = "x", Resolved = "x" },
            Secret = new() { Value = "y" }, // no transient flag
        };

        await NewResolver().ResolveTransientAsync(
            config,
            () => throw new InvalidOperationException("model must not be built for transient-free configs"),
            NewContext(),
            CancellationToken.None);
    }

    [Fact]
    public async Task Execute_phase_failure_surfaces_expression_resolution_exception()
    {
        var config = new TransientTestConfig { Secret = new() { Value = "s3cr3t", Transient = true } };
        var engineless = new ExpressionResolver(Enumerable.Empty<IExpressionEngine>());

        await Assert.ThrowsAsync<ExpressionResolutionException>(() => engineless.ResolveTransientAsync(
            config, () => new Dictionary<string, object?>(), NewContext(), CancellationToken.None).AsTask());
    }

    // ----- step builder: persistence shape + size guardrail -----

    [Fact]
    public async Task Builder_persists_transient_field_as_expression_without_resolved_value()
    {
        var (builder, run, node) = NewBuilder(configJson: """
            {
              "plain": {"engine":"static","value":"hello"},
              "secret": {"engine":"static","value":"s3cr3t","transient":true}
            }
            """);

        var step = await builder.TryBuildAsync(run, node, null, null, new Dictionary<string, object?>(), CancellationToken.None);

        Assert.NotNull(step);
        Assert.Equal(StepExecutionStatus.Pending, step!.Status);

        var persisted = step.ResolvedConfig;
        Assert.Equal("hello", persisted.GetProperty("plain").GetProperty("resolved").GetString());

        var secret = persisted.GetProperty("secret");
        Assert.True(secret.GetProperty("transient").GetBoolean());
        Assert.False(secret.TryGetProperty("resolved", out _));      // the value never lands in storage
        Assert.Equal("s3cr3t", secret.GetProperty("value").GetString()); // the expression does
    }

    [Fact]
    public async Task Builder_rejects_resolved_config_over_the_size_cap()
    {
        var huge = new string('a', 500);
        var (builder, run, node) = NewBuilder(
            configJson: JsonSerializer.Serialize(new { plain = new { engine = "static", value = huge } }),
            maxResolvedConfigChars: 100);

        var step = await builder.TryBuildAsync(run, node, null, null, new Dictionary<string, object?>(), CancellationToken.None);

        Assert.NotNull(step);
        Assert.Equal(StepExecutionStatus.Dead, step!.Status);
        Assert.Contains("transient", step.LastError, StringComparison.OrdinalIgnoreCase);
        // The oversized payload itself must not be persisted on the dead row.
        Assert.DoesNotContain(huge, step.ResolvedConfig.GetRawText());
    }

    // ----- executor: late materialisation + retry semantics -----

    [Fact]
    public async Task Executor_materialises_transient_fields_before_the_action_runs()
    {
        var (executor, step, capture) = ExecutorHarness(maxAttempts: 1, NewResolver());

        await executor.ExecuteAsync(step, WorkflowStepLane.Any, CancellationToken.None);

        Assert.NotNull(capture.Seen);
        Assert.Equal("persisted", capture.Seen!.Plain.Resolved);   // from resolved_config as-is
        Assert.Equal("s3cr3t", capture.Seen.Secret.Resolved);      // materialised just-in-time
        Assert.Equal(StepExecutionStatus.Completed, step.Status);
    }

    [Fact]
    public async Task Executor_treats_transient_resolution_failure_as_retryable_error()
    {
        // Engine-less resolver: the transient leaf's engine lookup fails deterministically.
        var (executor, step, capture) = ExecutorHarness(
            maxAttempts: 3, new ExpressionResolver(Enumerable.Empty<IExpressionEngine>()));

        await executor.ExecuteAsync(step, WorkflowStepLane.Any, CancellationToken.None);

        Assert.Null(capture.Seen); // the action never ran — no side effects on a broken config
        Assert.Equal(StepExecutionStatus.Pending, step.Status); // retry scheduled, not Dead
        Assert.Contains("No engine registered", step.LastError);
        Assert.True(step.NextAttemptAt > DateTime.UtcNow, "retry must be scheduled with backoff");
    }

    // ----- resumer: pre-guard transient resolution -----

    [Fact]
    public async Task Resume_with_failing_transient_resolution_fails_cleanly_and_keeps_step_waiting()
    {
        // Engine-less resolver: the transient leaf's engine lookup fails deterministically —
        // stands in for "secret store is down right now".
        var (resumer, step, probe) = ResumerHarness(new ExpressionResolver(Enumerable.Empty<IExpressionEngine>()));

        var result = await resumer.ResumeAsync(
            new WorkflowResumeCommand { RunId = step.RunId, StepId = step.Id, TenantId = step.TenantId, Port = "done" },
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(WorkflowResumeFailureReason.ConfigResolutionFailed, result.Reason);
        // The failure happened BEFORE the guard: the step is untouched and retryable, the hook
        // never ran — no exception escaped to poison a signaler / parent-resume chain.
        Assert.Equal(StepExecutionStatus.Waiting, step.Status);
        Assert.Null(probe.SeenOnResume);
    }

    [Fact]
    public async Task Resume_materialises_transient_fields_for_the_resume_hook()
    {
        var (resumer, step, probe) = ResumerHarness(NewResolver());

        var result = await resumer.ResumeAsync(
            new WorkflowResumeCommand { RunId = step.RunId, StepId = step.Id, TenantId = step.TenantId, Port = "done" },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(probe.SeenOnResume);
        Assert.Equal("s3cr3t", probe.SeenOnResume!.Secret.Resolved);
        Assert.Equal(StepExecutionStatus.Completed, step.Status);
    }

    // ----- harness -----

    private static readonly JsonElement EmptyJsonObject = JsonDocument.Parse("{}").RootElement;

    private static ExpressionResolver NewResolver() =>
        new(new IExpressionEngine[] { new StaticExpressionEngine() });

    private static ExpressionEvaluationContext NewContext() => new()
    {
        TenantId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
    };

    private static WorkflowRunRecord NewRun(string snapshot = "{}") => new()
    {
        TenantId = Guid.NewGuid(),
        DefinitionId = Guid.NewGuid(),
        TriggerKind = "Test",
        WorkflowSnapshot = snapshot,
        StaticContext = EmptyJsonObject,
        StepsOutputs = EmptyJsonObject,
        Status = WorkflowRunStatus.Running,
        StartedAt = DateTime.UtcNow,
    };

    private static (StepExecutionBuilder Builder, WorkflowRunRecord Run, WorkflowNode Node) NewBuilder(
        string configJson, int? maxResolvedConfigChars = null)
    {
        var settings = new WorkflowEngineSettings();
        if (maxResolvedConfigChars is { } cap)
        {
            settings.MaxResolvedConfigChars = cap;
        }

        var builder = new StepExecutionBuilder(
            NewResolver(),
            new FakeRegistry(new CaptureAction()),
            Options.Create(settings),
            NullLogger<StepExecutionBuilder>.Instance);

        var node = new WorkflowNode
        {
            Id = "n1",
            Kind = CaptureAction.KindName,
            Key = "n1",
            Config = JsonSerializer.Deserialize<JsonElement>(configJson),
        };

        return (builder, NewRun(), node);
    }

    private static (WorkflowStepExecutor Executor, WorkflowStepRecord Step, CaptureAction Capture) ExecutorHarness(
        int maxAttempts, ExpressionResolver resolver)
    {
        var capture = new CaptureAction();
        var registry = new FakeRegistry(capture);

        // Single-node graph, no edges — fan-out no-ops after the step completes.
        var graph = new WorkflowGraph
        {
            Nodes = { new WorkflowNode { Id = "n1", Kind = CaptureAction.KindName, Key = "n1", Config = EmptyJsonObject } },
            StartNodeId = "n1",
        };
        var run = NewRun(JsonSerializer.Serialize(graph, WorkflowJsonOptions.Default));
        var store = new FakeStore(run);

        // resolved_config exactly as the builder would persist it: plain carries its build-time
        // value, secret carries only the expression + flag.
        var step = new WorkflowStepRecord
        {
            RunId = run.Id,
            TenantId = run.TenantId,
            NodeId = "n1",
            Kind = CaptureAction.KindName,
            ResolvedConfig = JsonSerializer.Deserialize<JsonElement>("""
                {
                  "plain": {"engine":"static","value":"would-give-this","resolved":"persisted"},
                  "secret": {"engine":"static","value":"s3cr3t","transient":true}
                }
                """),
            Status = StepExecutionStatus.Running,
            NextAttemptAt = DateTime.UtcNow,
            AttemptCount = 1,
        };

        var settings = new WorkflowEngineSettings { MaxAttempts = maxAttempts };
        var fanOut = new WorkflowFanOut(
            store, new FakeBuilder(), Options.Create(settings),
            new ServiceCollection().BuildServiceProvider(), NullLogger<WorkflowFanOut>.Instance);
        var executor = new WorkflowStepExecutor(
            store, registry, fanOut, resolver,
            Options.Create(settings), NullLogger<WorkflowStepExecutor>.Instance);

        return (executor, step, capture);
    }

    private static (WorkflowResumer Resumer, WorkflowStepRecord Step, ResumeProbeAction Probe) ResumerHarness(
        ExpressionResolver resolver)
    {
        var probe = new ResumeProbeAction();
        var run = NewRun();
        var step = new WorkflowStepRecord
        {
            RunId = run.Id,
            TenantId = run.TenantId,
            NodeId = "n1",
            Kind = ResumeProbeAction.KindName,
            ResolvedConfig = JsonSerializer.Deserialize<JsonElement>("""
                {"secret":{"engine":"static","value":"s3cr3t","transient":true}}
                """),
            Status = StepExecutionStatus.Waiting,
            NextAttemptAt = DateTime.MaxValue,
        };

        var store = new FakeStore(run);
        store.Steps.Add(step);

        var settings = new WorkflowEngineSettings();
        var fanOut = new WorkflowFanOut(
            store, new FakeBuilder(), Options.Create(settings),
            new ServiceCollection().BuildServiceProvider(), NullLogger<WorkflowFanOut>.Instance);
        var resumer = new WorkflowResumer(
            store, fanOut, new FakeRegistry(probe), resolver, NullLogger<WorkflowResumer>.Instance);

        return (resumer, step, probe);
    }

    // ----- test doubles -----

    public class TransientTestConfig
    {
        public Expr<string> Plain { get; set; } = new();

        public Expr<string> Secret { get; set; } = new();

        [TransientExpr]
        public Expr<string>? ForcedSecret { get; set; }

        [TransientExpr]
        public List<Item>? Items { get; set; }

        public class Item
        {
            public Expr<string> Value { get; set; } = new();
        }
    }

    /// <summary>Suspends on execute; captures the (transient-resolved) config on resume.</summary>
    private sealed class ResumeProbeAction : ActionType<TransientTestConfig>
    {
        public const string KindName = "TransientResumeProbe";

        public TransientTestConfig? SeenOnResume { get; private set; }

        public override string Kind => KindName;

        public override string DisplayName => KindName;

        public override IReadOnlyList<ActionPortDescriptor> OutputPorts { get; } = new[]
        {
            new ActionPortDescriptor("done", "Done", ActionPortKind.Normal),
        };

        public override Task<ActionExecutionResult> ExecuteAsync(
            ActionContext<TransientTestConfig> context, CancellationToken cancellationToken)
            => Task.FromResult(this.Suspend());

        public override Task<ActionExecutionResult> OnStepResumedAsync(
            ActionContext context, JsonElement? payload, string? port, CancellationToken cancellationToken)
        {
            this.SeenOnResume = (TransientTestConfig)context.Config;
            return Task.FromResult(ActionExecutionResult.OnPort(port ?? "done", null));
        }
    }

    private sealed class CaptureAction : ActionType<TransientTestConfig>
    {
        public const string KindName = "TransientCapture";

        public TransientTestConfig? Seen { get; private set; }

        public override string Kind => KindName;

        public override string DisplayName => KindName;

        public override IReadOnlyList<ActionPortDescriptor> OutputPorts { get; } = new[]
        {
            new ActionPortDescriptor("done", "Done", ActionPortKind.Normal),
        };

        public override Task<ActionExecutionResult> ExecuteAsync(
            ActionContext<TransientTestConfig> context, CancellationToken cancellationToken)
        {
            this.Seen = context.Config;
            return Task.FromResult(ActionExecutionResult.OnPort("done", null));
        }
    }
}
