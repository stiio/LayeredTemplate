using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Actions;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Generic <c>SendSignal</c> engine action — emits a signal that fan-out-resumes every run
/// waiting on the resolved key. Covers: resolves key + payload and calls
/// <c>IWorkflowSignaler.SignalAsync(tenant, key, payload)</c> on a SEPARATE DI scope (re-entrancy
/// safety) then fires <c>sent</c> with delivered/stale; empty/blank key → non-transient OnError;
/// payload parse-as-JSON (object/scalar) else wrap-as-string; a signaler exception routes to the
/// <c>error</c> port (best-effort-but-surfaced); declares exactly the two ports.
/// </summary>
public class SendSignalActionTypeTests
{
    private static readonly Guid Tenant = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task Resolves_key_and_payload_calls_signaler_and_fires_sent_with_delivered_and_stale()
    {
        var fake = new FakeSignaler(new WorkflowSignalResult(Delivered: 3, Stale: 1));
        var action = NewAction(fake);

        var result = await action.ExecuteAsync(
            Context(key: "order:42", payload: """{"status":"paid"}"""),
            CancellationToken.None);

        Assert.Equal("sent", result.OutputPort);
        Assert.False(result.IsSuspended);
        Assert.Null(result.Error);

        // The exact resolved key + tenant reached the signaler.
        Assert.Equal(Tenant, fake.LastTenant);
        Assert.Equal("order:42", fake.LastKey);
        // Object payload flows through verbatim so a waiting step reads steps.<key>.status.
        Assert.NotNull(fake.LastPayload);
        Assert.Equal("paid", fake.LastPayload!.Value.GetProperty("status").GetString());

        var outputs = ToJson(result.Outputs!);
        Assert.Equal(3, outputs.GetProperty("delivered").GetInt32());
        Assert.Equal(1, outputs.GetProperty("stale").GetInt32());
    }

    [Fact]
    public async Task Trims_the_resolved_key_before_signalling()
    {
        var fake = new FakeSignaler(new WorkflowSignalResult(0, 0));
        var action = NewAction(fake);

        await action.ExecuteAsync(Context(key: "  K \n", payload: null), CancellationToken.None);

        Assert.Equal("K", fake.LastKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_key_fails_non_transient_and_never_signals(string? key)
    {
        var fake = new FakeSignaler(new WorkflowSignalResult(0, 0));
        var action = NewAction(fake);

        var result = await action.ExecuteAsync(Context(key: key, payload: "x"), CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
        Assert.Null(result.OutputPort);
        Assert.False(fake.Called); // never reached the signaler
    }

    [Fact]
    public async Task Null_payload_signals_with_no_payload()
    {
        var fake = new FakeSignaler(new WorkflowSignalResult(0, 0));
        var action = NewAction(fake);

        await action.ExecuteAsync(Context(key: "K", payload: null), CancellationToken.None);

        Assert.True(fake.Called);
        Assert.Null(fake.LastPayload);
    }

    [Fact]
    public async Task Non_json_payload_is_wrapped_as_a_json_string_not_dropped()
    {
        var fake = new FakeSignaler(new WorkflowSignalResult(0, 0));
        var action = NewAction(fake);

        await action.ExecuteAsync(Context(key: "K", payload: "just a string"), CancellationToken.None);

        Assert.NotNull(fake.LastPayload);
        Assert.Equal(JsonValueKind.String, fake.LastPayload!.Value.ValueKind);
        Assert.Equal("just a string", fake.LastPayload.Value.GetString());
    }

    [Fact]
    public async Task Json_scalar_payload_flows_through_as_that_scalar()
    {
        var fake = new FakeSignaler(new WorkflowSignalResult(0, 0));
        var action = NewAction(fake);

        await action.ExecuteAsync(Context(key: "K", payload: "1234"), CancellationToken.None);

        Assert.NotNull(fake.LastPayload);
        Assert.Equal(JsonValueKind.Number, fake.LastPayload!.Value.ValueKind);
        Assert.Equal(1234, fake.LastPayload.Value.GetInt32());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("x")]
    public async Task Key_longer_than_the_column_width_fails_non_transient_and_never_signals(string? payload)
    {
        // Symmetric with the WaitSignal guard: no bookmark could exist for a key over the
        // workflow_bookmark.correlation_key varchar(256), so signalling it can only ever deliver 0.
        // Fail loud (non-transient) rather than fan out a guaranteed-empty lookup.
        var fake = new FakeSignaler(new WorkflowSignalResult(0, 0));
        var action = NewAction(fake);
        var overLong = new string('k', WorkflowBookmarkRegistration.MaxCorrelationKeyLength + 1);

        var result = await action.ExecuteAsync(Context(key: overLong, payload: payload), CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
        Assert.Null(result.OutputPort);
        Assert.False(fake.Called); // never reached the signaler
    }

    [Fact]
    public async Task Key_exactly_at_the_column_width_still_signals()
    {
        var fake = new FakeSignaler(new WorkflowSignalResult(1, 0));
        var action = NewAction(fake);
        var maxKey = new string('k', WorkflowBookmarkRegistration.MaxCorrelationKeyLength);

        var result = await action.ExecuteAsync(Context(key: maxKey, payload: null), CancellationToken.None);

        Assert.Equal("sent", result.OutputPort);
        Assert.True(fake.Called);
        Assert.Equal(maxKey, fake.LastKey);
    }

    [Fact]
    public async Task Signaler_exception_routes_to_the_error_port_without_leaking_the_raw_message()
    {
        // The raw exception message (an Npgsql/PostgresException can carry query / table / schema
        // fragments) must never reach the run record or UI — only a stable reason + coarse type hint.
        const string secret = "relation \"submission\" does not exist";
        var fake = new FakeSignaler(_ => throw new InvalidOperationException(secret));
        var action = NewAction(fake);

        var result = await action.ExecuteAsync(Context(key: "K", payload: null), CancellationToken.None);

        Assert.Equal("error", result.OutputPort);
        Assert.False(result.IsSuspended);
        Assert.Null(result.Error); // surfaced via the Error-kind PORT, not the dead-letter Error field

        var outputs = ToJson(result.Outputs!);
        Assert.Equal("signal_failed", outputs.GetProperty("reason").GetString());
        // Coarse type hint only — safe for triage without exposing internals.
        Assert.Equal(nameof(InvalidOperationException), outputs.GetProperty("errorType").GetString());
        // The raw message is NOT surfaced: no `message` field, and the secret appears nowhere in the
        // serialized output (e.g. smuggled into another field).
        Assert.False(outputs.TryGetProperty("message", out _));
        Assert.DoesNotContain(secret, outputs.GetRawText());
        Assert.DoesNotContain("submission", outputs.GetRawText());
    }

    [Fact]
    public void Declares_exactly_the_sent_and_error_ports()
    {
        var action = NewAction(new FakeSignaler(new WorkflowSignalResult(0, 0)));
        Assert.Equal("SendSignal", action.Kind);
        Assert.Equal(new[] { "sent", "error" }, action.OutputPorts.Select(p => p.Id).ToArray());
        Assert.Equal(ActionPortKind.Normal, action.OutputPorts[0].Kind);
        Assert.Equal(ActionPortKind.Error, action.OutputPorts[1].Kind);
    }

    private static SendSignalActionType NewAction(IWorkflowSignaler signaler) =>
        new(new SingleSignalerScopeFactory(signaler), NullLogger<SendSignalActionType>.Instance);

    private static ActionContext<SendSignalConfig> Context(string? key, string? payload) => new()
    {
        Config = new SendSignalConfig
        {
            Key = key is null ? null : new Expr<string> { Engine = "static", Resolved = key },
            Payload = payload is null ? null : new Expr<string> { Engine = "static", Resolved = payload },
        },
        RunId = Guid.NewGuid(),
        StepExecutionId = Guid.NewGuid(),
        TenantId = Tenant,
        DefinitionId = Guid.NewGuid(),
        NodeKey = "send_1",
        StepsOutputs = JsonDocument.Parse("{}").RootElement,
    };

    private static JsonElement ToJson(object outputs)
        => JsonDocument.Parse(JsonSerializer.Serialize(outputs)).RootElement;

    // ----- fakes -----

    /// <summary>Records the args SendSignal hands the signaler; the action resolves it from a child scope.</summary>
    private sealed class FakeSignaler : IWorkflowSignaler
    {
        private readonly Func<string, WorkflowSignalResult> outcome;

        public FakeSignaler(WorkflowSignalResult result) => this.outcome = _ => result;

        public FakeSignaler(Func<string, WorkflowSignalResult> outcome) => this.outcome = outcome;

        public bool Called { get; private set; }

        public Guid LastTenant { get; private set; }

        public string? LastKey { get; private set; }

        public JsonElement? LastPayload { get; private set; }

        public Task<WorkflowSignalResult> SignalAsync(
            Guid tenantId, string correlationKey, JsonElement? payload, CancellationToken cancellationToken)
        {
            this.Called = true;
            this.LastTenant = tenantId;
            this.LastKey = correlationKey;
            this.LastPayload = payload?.Clone();
            return Task.FromResult(this.outcome(correlationKey));
        }
    }
}
