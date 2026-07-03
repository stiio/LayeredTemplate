using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Engine.Actions;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Generic <c>WaitSignal</c> engine action — the domain-agnostic suspend-on-N-keys primitive
/// beneath consumer adapters. Covers: resolve keys → suspend with one bookmark per DISTINCT key
/// on the <c>signaled</c> port (wait-for-ANY); empty/zero keys → non-transient OnError (can't
/// wait on nothing — no silent forever-park); UNSET timeout = wait indefinitely (null), explicit
/// timeout honoured; OnStepResumed → <c>signaled</c> echoing the signal payload; OnStepTimedOut
/// → <c>timedOut</c>; declares exactly the two ports.
/// </summary>
public class WaitSignalActionTypeTests
{
    [Fact]
    public async Task Suspends_with_one_bookmark_per_key_on_the_signaled_port_wait_for_any()
    {
        var action = new WaitSignalActionType();
        var result = await action.ExecuteAsync(
            Context(timeoutSeconds: null, "order:42", "webhook:abc"),
            CancellationToken.None);

        Assert.True(result.IsSuspended);
        Assert.Null(result.OutputPort); // suspend fires no successor

        Assert.NotNull(result.Bookmarks);
        Assert.Equal(2, result.Bookmarks!.Count);
        Assert.All(result.Bookmarks, b => Assert.Equal("signaled", b.ResumePort));
        Assert.Equal(
            new[] { "order:42", "webhook:abc" }.OrderBy(x => x),
            result.Bookmarks.Select(b => b.CorrelationKey).OrderBy(x => x));

        // Pre-suspend metadata carries only a count — opaque keys are NOT echoed (could be PHI).
        var outputs = ToJson(result.Outputs!);
        Assert.Equal("signal", outputs.GetProperty("waitingFor").GetString());
        Assert.Equal(2, outputs.GetProperty("keyCount").GetInt32());
    }

    [Fact]
    public async Task Dedups_and_trims_keys_to_one_bookmark_per_distinct_value()
    {
        var action = new WaitSignalActionType();
        var result = await action.ExecuteAsync(
            Context(timeoutSeconds: null, "K", "  K ", "\tK\n", "other"),
            CancellationToken.None);

        Assert.True(result.IsSuspended);
        Assert.Equal(
            new[] { "K", "other" }.OrderBy(x => x),
            result.Bookmarks!.Select(b => b.CorrelationKey).OrderBy(x => x));
    }

    [Fact]
    public async Task Unset_timeout_waits_indefinitely()
    {
        var action = new WaitSignalActionType();
        var result = await action.ExecuteAsync(
            Context(timeoutSeconds: null, "K"),
            CancellationToken.None);

        Assert.Null(result.SuspendTimeoutSeconds); // null = wait forever (low-level primitive)
    }

    [Theory]
    [InlineData(0)]    // non-positive also means "no deadline"
    [InlineData(-5)]
    public async Task Non_positive_timeout_waits_indefinitely(int seconds)
    {
        var action = new WaitSignalActionType();
        var result = await action.ExecuteAsync(
            Context(timeoutSeconds: seconds, "K"),
            CancellationToken.None);

        Assert.Null(result.SuspendTimeoutSeconds);
    }

    [Fact]
    public async Task Explicit_positive_timeout_is_honoured()
    {
        var action = new WaitSignalActionType();
        var result = await action.ExecuteAsync(
            Context(timeoutSeconds: 7200, "K"),
            CancellationToken.None);

        Assert.Equal(7200, result.SuspendTimeoutSeconds);
    }

    [Fact]
    public async Task No_keys_fails_non_transient_instead_of_waiting_forever()
    {
        var action = new WaitSignalActionType();
        var result = await action.ExecuteAsync(
            new ActionContext<WaitSignalConfig>
            {
                Config = new WaitSignalConfig { Keys = new List<WaitSignalKey>() },
                RunId = Guid.NewGuid(),
                StepExecutionId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                DefinitionId = Guid.NewGuid(),
                NodeKey = "wait_1",
                StepsOutputs = JsonDocument.Parse("{}").RootElement,
            },
            CancellationToken.None);

        Assert.False(result.IsSuspended);
        Assert.Null(result.Bookmarks);
        Assert.Null(result.OutputPort);
        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task All_blank_keys_resolve_to_zero_and_fail(string? blank)
    {
        var action = new WaitSignalActionType();
        var result = await action.ExecuteAsync(
            Context(timeoutSeconds: null, blank),
            CancellationToken.None);

        Assert.False(result.IsSuspended);
        Assert.Null(result.Bookmarks);
        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
    }

    [Fact]
    public async Task Key_longer_than_the_column_width_fails_non_transient_and_does_not_suspend()
    {
        // A key over workflow_bookmark.correlation_key's varchar(256) would blow up with a
        // DbUpdateException at suspend time → dead-letter; the guard fails loud here instead.
        var action = new WaitSignalActionType();
        var overLong = new string('k', WorkflowBookmarkRegistration.MaxCorrelationKeyLength + 1);

        var result = await action.ExecuteAsync(
            Context(timeoutSeconds: null, "ok", overLong),
            CancellationToken.None);

        Assert.False(result.IsSuspended);
        Assert.Null(result.Bookmarks); // no bookmark registered — never reached OnSuspend
        Assert.Null(result.OutputPort);
        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
    }

    [Fact]
    public async Task Key_exactly_at_the_column_width_still_suspends()
    {
        var action = new WaitSignalActionType();
        var maxKey = new string('k', WorkflowBookmarkRegistration.MaxCorrelationKeyLength);

        var result = await action.ExecuteAsync(
            Context(timeoutSeconds: null, maxKey),
            CancellationToken.None);

        Assert.True(result.IsSuspended);
        Assert.Single(result.Bookmarks!);
        Assert.Equal(maxKey, result.Bookmarks![0].CorrelationKey);
    }

    [Fact]
    public async Task On_step_resumed_fires_signaled_and_echoes_the_payload()
    {
        var action = new WaitSignalActionType();
        var payload = JsonDocument.Parse("""{"amount":1000}""").RootElement;
        var result = await action.OnStepResumedAsync(
            ResumeContext(),
            payload,
            port: null,            // fixed-port action ignores the caller-supplied port
            CancellationToken.None);

        Assert.Equal("signaled", result.OutputPort);
        Assert.False(result.IsSuspended);
        Assert.Null(result.Error);
        // The signal payload is echoed verbatim onto the step's outputs.
        Assert.Equal(1000, ToJson(result.Outputs!).GetProperty("amount").GetInt32());
    }

    [Fact]
    public async Task On_step_resumed_ignores_a_caller_supplied_port_and_always_signals()
    {
        var action = new WaitSignalActionType();
        var result = await action.OnStepResumedAsync(
            ResumeContext(), payload: null, port: "timedOut", CancellationToken.None);

        Assert.Equal("signaled", result.OutputPort); // fixed port — caller's "timedOut" is ignored
    }

    [Fact]
    public async Task On_timeout_fires_the_timed_out_port()
    {
        var action = new WaitSignalActionType();
        var result = await action.OnStepTimedOutAsync(ResumeContext(), CancellationToken.None);

        Assert.Equal("timedOut", result.OutputPort);
        Assert.False(result.IsSuspended);
        Assert.Null(result.Error);
        Assert.True(ToJson(result.Outputs!).TryGetProperty("timedOutAt", out _));
    }

    [Fact]
    public void Declares_exactly_the_signaled_and_timed_out_ports()
    {
        var action = new WaitSignalActionType();
        Assert.Equal("WaitSignal", action.Kind);
        Assert.Equal(new[] { "signaled", "timedOut" }, action.OutputPorts.Select(p => p.Id).ToArray());
        Assert.Equal(ActionPortKind.Normal, action.OutputPorts[0].Kind);
        Assert.Equal(ActionPortKind.Error, action.OutputPorts[1].Kind);
    }

    private static ActionContext<WaitSignalConfig> Context(int? timeoutSeconds, params string?[] keys) => new()
    {
        Config = new WaitSignalConfig
        {
            Keys = keys
                .Select(k => new WaitSignalKey
                {
                    Key = k is null ? null : new Expr<string> { Engine = "static", Resolved = k },
                })
                .ToList(),
            TimeoutSeconds = timeoutSeconds,
        },
        RunId = Guid.NewGuid(),
        StepExecutionId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        DefinitionId = Guid.NewGuid(),
        NodeKey = "wait_1",
        StepsOutputs = JsonDocument.Parse("{}").RootElement,
    };

    private static ActionContext ResumeContext() => new()
    {
        Config = new WaitSignalConfig(),
        RunId = Guid.NewGuid(),
        StepExecutionId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        DefinitionId = Guid.NewGuid(),
        NodeKey = "wait_1",
        StepsOutputs = JsonDocument.Parse("{}").RootElement,
    };

    private static JsonElement ToJson(object outputs)
        => JsonDocument.Parse(JsonSerializer.Serialize(outputs)).RootElement;
}
