using System.Diagnostics;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Latch semantics of <c>WorkflowWorkSignal</c> — the piece LISTEN/NOTIFY correctness hangs on:
///   - a pulse with nobody waiting is remembered (no lost wakeup between claim and wait)
///   - a pulse during a wait wakes it early; no pulse → the wait runs to its timeout
///   - lane routing: fast wakes FastOnly + Any, long wakes LongOnly + Any, Any wakes everyone
///   - a woken wait re-arms its own lane only — a fast waiter must not steal a latched long
///     pulse from the long pool
///   - wake-all: every concurrent waiter of a generation wakes on one pulse
///   - cancellation surfaces as OperationCanceledException.
/// Timing convention: "must wake" paths get a 10s ceiling and are asserted to finish within
/// 2s (real wakes are sub-millisecond — the margin only absorbs CI scheduling noise);
/// "must NOT wake" paths assert the wait is still pending after a 100ms grace.
/// </summary>
public class WorkflowWorkSignalTests
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan WakeBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan NoWakeGrace = TimeSpan.FromMilliseconds(100);

    [Fact]
    public async Task Wait_without_pulse_returns_after_timeout()
    {
        var signal = new WorkflowWorkSignal();
        var stopwatch = Stopwatch.StartNew();

        await signal.WaitForWorkAsync(WorkflowStepLane.Any, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        // Task.Delay never completes early; small lower bound guards against a busted latch
        // that would return instantly.
        Assert.True(stopwatch.ElapsedMilliseconds >= 40, $"returned after {stopwatch.ElapsedMilliseconds}ms — latch fired without a pulse");
    }

    [Fact]
    public async Task Pulse_before_wait_is_latched_and_wait_returns_immediately()
    {
        var signal = new WorkflowWorkSignal();

        signal.Pulse(WorkflowStepLane.FastOnly);

        await signal.WaitForWorkAsync(WorkflowStepLane.FastOnly, Ceiling, CancellationToken.None)
            .WaitAsync(WakeBudget);
    }

    [Fact]
    public async Task Pulse_during_wait_wakes_it_early()
    {
        var signal = new WorkflowWorkSignal();
        var wait = signal.WaitForWorkAsync(WorkflowStepLane.FastOnly, Ceiling, CancellationToken.None);

        signal.Pulse(WorkflowStepLane.FastOnly);

        await wait.WaitAsync(WakeBudget);
    }

    [Fact]
    public async Task Fast_pulse_does_not_wake_long_waiter()
    {
        var signal = new WorkflowWorkSignal();
        using var cts = new CancellationTokenSource();
        var wait = signal.WaitForWorkAsync(WorkflowStepLane.LongOnly, Ceiling, cts.Token);

        signal.Pulse(WorkflowStepLane.FastOnly);
        await Task.Delay(NoWakeGrace);

        Assert.False(wait.IsCompleted);
        cts.Cancel(); // don't leave the 10s wait running past the test
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }

    [Theory]
    [InlineData(WorkflowStepLane.FastOnly)]
    [InlineData(WorkflowStepLane.LongOnly)]
    public async Task Lane_pulse_wakes_any_waiter(WorkflowStepLane pulsedLane)
    {
        // Single-pool mode: the one pool waits in Any and must react to both lane payloads.
        var signal = new WorkflowWorkSignal();
        var wait = signal.WaitForWorkAsync(WorkflowStepLane.Any, Ceiling, CancellationToken.None);

        signal.Pulse(pulsedLane);

        await wait.WaitAsync(WakeBudget);
    }

    [Fact]
    public async Task Any_pulse_wakes_both_dedicated_pools()
    {
        // Reconnect-gap catch-up pulses Any; both pools must wake.
        var signal = new WorkflowWorkSignal();
        var fastWait = signal.WaitForWorkAsync(WorkflowStepLane.FastOnly, Ceiling, CancellationToken.None);
        var longWait = signal.WaitForWorkAsync(WorkflowStepLane.LongOnly, Ceiling, CancellationToken.None);

        signal.Pulse(WorkflowStepLane.Any);

        await Task.WhenAll(fastWait, longWait).WaitAsync(WakeBudget);
    }

    [Fact]
    public async Task Woken_wait_rearms_its_lane()
    {
        var signal = new WorkflowWorkSignal();
        signal.Pulse(WorkflowStepLane.FastOnly);
        await signal.WaitForWorkAsync(WorkflowStepLane.FastOnly, Ceiling, CancellationToken.None)
            .WaitAsync(WakeBudget);

        // The latch was consumed — a second wait must block again until a fresh pulse.
        using var cts = new CancellationTokenSource();
        var second = signal.WaitForWorkAsync(WorkflowStepLane.FastOnly, Ceiling, cts.Token);
        await Task.Delay(NoWakeGrace);
        Assert.False(second.IsCompleted);

        signal.Pulse(WorkflowStepLane.FastOnly);
        await second.WaitAsync(WakeBudget);
    }

    [Fact]
    public async Task Fast_waiter_does_not_consume_latched_long_pulse()
    {
        var signal = new WorkflowWorkSignal();
        signal.Pulse(WorkflowStepLane.Any); // latches both lanes

        // Fast waiter wakes and re-arms ONLY the fast lane...
        await signal.WaitForWorkAsync(WorkflowStepLane.FastOnly, Ceiling, CancellationToken.None)
            .WaitAsync(WakeBudget);

        // ...so the long pool's latch must still be set for its own (later) wait.
        await signal.WaitForWorkAsync(WorkflowStepLane.LongOnly, Ceiling, CancellationToken.None)
            .WaitAsync(WakeBudget);
    }

    [Fact]
    public async Task One_pulse_wakes_every_concurrent_waiter_of_the_lane()
    {
        var signal = new WorkflowWorkSignal();
        var first = signal.WaitForWorkAsync(WorkflowStepLane.FastOnly, Ceiling, CancellationToken.None);
        var second = signal.WaitForWorkAsync(WorkflowStepLane.FastOnly, Ceiling, CancellationToken.None);

        signal.Pulse(WorkflowStepLane.FastOnly);

        await Task.WhenAll(first, second).WaitAsync(WakeBudget);
    }

    [Fact]
    public async Task Cancellation_throws_operation_canceled()
    {
        var signal = new WorkflowWorkSignal();
        using var cts = new CancellationTokenSource();
        var wait = signal.WaitForWorkAsync(WorkflowStepLane.Any, Ceiling, cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait.WaitAsync(WakeBudget));
    }
}
