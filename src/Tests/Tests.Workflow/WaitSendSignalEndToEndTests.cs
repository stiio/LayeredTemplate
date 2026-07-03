using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Engine.Actions;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// End-to-end-ish wiring of the generic <c>WaitSignal</c> / <c>SendSignal</c> pair over the REAL
/// <see cref="WorkflowSignaler"/> fan-out (with an in-memory bookmark store + recording resumer):
///  - a WaitSignal suspend registers a bookmark whose key a SendSignal on the same key resumes;
///  - the re-entrancy case: a SendSignal that resumes a waiter behaves — the resume is guarded
///    (one resume per bookmark), the bookmark is consumed, and a second signal is a no-op (no
///    double-resume, no synchronous infinite cascade).
/// </summary>
public class WaitSendSignalEndToEndTests
{
    private static readonly Guid Tenant = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task SendSignal_on_a_WaitSignal_registered_key_resumes_that_exact_waiter()
    {
        // 1) WaitSignal suspends — capture the bookmark it would register and seed the store with it,
        //    exactly as the worker's suspend branch (store.AddBookmarks) would.
        var wait = new WaitSignalActionType();
        var suspend = await wait.ExecuteAsync(
            WaitContext("order:42"), CancellationToken.None);
        var registered = Assert.Single(suspend.Bookmarks!);

        var bookmark = new WorkflowBookmarkRecord
        {
            Id = Guid.CreateVersion7(),
            TenantId = Tenant,
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            CorrelationKey = registered.CorrelationKey,
            ResumePort = registered.ResumePort,
            CreatedAt = DateTime.UtcNow,
        };
        var store = new FakeStore();
        store.Bookmarks.Add(bookmark);
        var resumer = new FakeResumer();
        var signaler = new WorkflowSignaler(store, resumer, NullLogger<WorkflowSignaler>.Instance);

        // 2) SendSignal on the same key → fan-out resumes the exact frozen (run, step) on `signaled`.
        var send = new SendSignalActionType(
            new SingleSignalerScopeFactory(signaler), NullLogger<SendSignalActionType>.Instance);
        var sent = await send.ExecuteAsync(
            SendContext(key: "order:42", payload: """{"amount":1000}"""), CancellationToken.None);

        Assert.Equal("sent", sent.OutputPort);
        Assert.Equal(1, ToJson(sent.Outputs!).GetProperty("delivered").GetInt32());

        var cmd = Assert.Single(resumer.Commands);
        Assert.Equal(bookmark.RunId, cmd.RunId);
        Assert.Equal(bookmark.StepId, cmd.StepId);
        Assert.Equal(Tenant, cmd.TenantId);
        Assert.Equal("signaled", cmd.Port);                 // the bookmark's resume port
        Assert.Equal(1000, cmd.Payload!.Value.GetProperty("amount").GetInt32()); // payload reached the resumer
        Assert.Contains(bookmark.Id, store.DeletedBookmarkIds); // consumed bookmark eagerly reaped
    }

    [Fact]
    public async Task Re_entrant_send_signal_resumes_each_waiter_once_with_no_double_resume()
    {
        // Re-entrancy guard: model the cascade where a resumed waiter would itself emit a signal.
        // The resumer guard (WHERE status='waiting') means each bookmark resumes at most once; the
        // consumed bookmark is deleted, so a SECOND signal on the same key finds nothing — no
        // synchronous infinite loop, no double-process.
        var b = new WorkflowBookmarkRecord
        {
            Id = Guid.CreateVersion7(),
            TenantId = Tenant,
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            CorrelationKey = "loop:key",
            ResumePort = "signaled",
            CreatedAt = DateTime.UtcNow,
        };
        var store = new FakeStore();
        store.Bookmarks.Add(b);
        var resumer = new FakeResumer();
        var signaler = new WorkflowSignaler(store, resumer, NullLogger<WorkflowSignaler>.Instance);
        var send = new SendSignalActionType(
            new SingleSignalerScopeFactory(signaler), NullLogger<SendSignalActionType>.Instance);

        var first = await send.ExecuteAsync(SendContext("loop:key", payload: null), CancellationToken.None);
        Assert.Equal(1, ToJson(first.Outputs!).GetProperty("delivered").GetInt32());

        // The hypothetical resumed step's own SendSignal on the same key — bookmark already consumed.
        var second = await send.ExecuteAsync(SendContext("loop:key", payload: null), CancellationToken.None);
        Assert.Equal(0, ToJson(second.Outputs!).GetProperty("delivered").GetInt32());

        Assert.Single(resumer.Commands); // resumed exactly once — no double-resume
    }

    private static ActionContext<WaitSignalConfig> WaitContext(string key) => new()
    {
        Config = new WaitSignalConfig
        {
            Keys = new List<WaitSignalKey> { new() { Key = new Expr<string> { Engine = "static", Resolved = key } } },
        },
        RunId = Guid.NewGuid(),
        StepExecutionId = Guid.NewGuid(),
        TenantId = Tenant,
        DefinitionId = Guid.NewGuid(),
        NodeKey = "wait_1",
        StepsOutputs = JsonDocument.Parse("{}").RootElement,
    };

    private static ActionContext<SendSignalConfig> SendContext(string key, string? payload) => new()
    {
        Config = new SendSignalConfig
        {
            Key = new Expr<string> { Engine = "static", Resolved = key },
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
}
