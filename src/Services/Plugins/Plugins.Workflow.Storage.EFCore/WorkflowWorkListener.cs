using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// Consumer half of the LISTEN/NOTIFY work push: one dedicated, non-pooled connection per
/// process that LISTENs on the configured channel and pulses <see cref="IWorkflowWorkSignal"/>
/// for each received lane payload. Everything here is best-effort by design — Postgres
/// notifications are not durable (anything sent while this connection is down is gone
/// forever), so:
/// <list type="bullet">
/// <item>after every (re)connect the listener pulses ALL lanes once — work committed during
/// the gap gets claimed immediately instead of waiting out the fallback poll;</item>
/// <item>connection failures reconnect with capped exponential backoff; while down, workers
/// simply run on the fallback <c>PollIntervalSeconds</c>;</item>
/// <item>keepalives are forced on this connection so a silently dropped socket (NAT / LB idle
/// timeout) is detected instead of listening into the void.</item>
/// </list>
/// LISTEN needs a session-scoped connection: PgBouncer in transaction/statement pooling mode
/// cannot carry it — the listener will keep failing (and logging), workers degrade to polling.
/// Starts without waiting for the host startup barrier: LISTEN touches no tables, and
/// attaching early means dispatches from other processes during our startup aren't missed.
/// </summary>
internal sealed class WorkflowWorkListener : BackgroundService
{
    private static readonly TimeSpan MinReconnectDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);

    private readonly string connectionString;
    private readonly string channel;
    private readonly IWorkflowWorkSignal signal;
    private readonly ILogger<WorkflowWorkListener> logger;

    public WorkflowWorkListener(
        string connectionString,
        string channel,
        IWorkflowWorkSignal signal,
        ILogger<WorkflowWorkListener> logger)
    {
        // Dedicated long-lived session: opt out of the pool (a LISTEN connection held for the
        // process lifetime shouldn't occupy a pool slot) and force keepalive probing.
        this.connectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            KeepAlive = 30,
        }.ConnectionString;
        this.channel = channel;
        this.signal = signal;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reconnectDelay = MinReconnectDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(this.connectionString);
                connection.Notification += (_, e) => this.signal.Pulse(LaneFromPayload(e.Payload));

                await connection.OpenAsync(stoppingToken);

                // Channel name is validated as a plain identifier at composition time
                // (AddEfCoreStorage), so interpolating it into LISTEN is safe.
                await using (var listen = new NpgsqlCommand($"LISTEN {this.channel}", connection))
                {
                    await listen.ExecuteNonQueryAsync(stoppingToken);
                }

                this.logger.LogInformation(
                    "Workflow work listener attached to channel '{Channel}'", this.channel);
                reconnectDelay = MinReconnectDelay;

                // Reconnect-gap catch-up: notifications sent while we weren't attached are lost
                // forever — wake every lane once so that work is claimed now, not a poll later.
                this.signal.Pulse(WorkflowStepLane.Any);

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Blocks reading the socket; received notifications fire the handler above.
                    // Keepalive failures / broken sockets surface here as exceptions.
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "Workflow work listener lost its connection; reconnecting in {Delay} (workers fall back to interval polling meanwhile)",
                    reconnectDelay);

                try
                {
                    await Task.Delay(reconnectDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                reconnectDelay = TimeSpan.FromTicks(Math.Min(reconnectDelay.Ticks * 2, MaxReconnectDelay.Ticks));
            }
        }
    }

    private static WorkflowStepLane LaneFromPayload(string payload) => payload switch
    {
        WorkflowWorkNotifyInterceptor.FastLanePayload => WorkflowStepLane.FastOnly,
        WorkflowWorkNotifyInterceptor.LongLanePayload => WorkflowStepLane.LongOnly,
        // Unknown sender / empty payload — be liberal, wake everyone.
        _ => WorkflowStepLane.Any,
    };
}
