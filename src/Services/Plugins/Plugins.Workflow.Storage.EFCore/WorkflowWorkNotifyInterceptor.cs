using System.Runtime.CompilerServices;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Storage.EFCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// Producer half of the LISTEN/NOTIFY work push. After every successful flush that wrote steps
/// claimable right now (<c>pending</c> with a due <c>next_attempt_at</c>), sends
/// <c>pg_notify(channel, lane)</c> on the SAME connection. That single choice yields the right
/// transactionality in both flush modes for free: under an ambient store transaction (resumer
/// unit of work, dispatcher's staged child) the notify joins it and Postgres delivers only on
/// commit — a rollback sends nothing; without one the flush has already committed and the
/// notify goes out immediately.
/// <para>
/// One producer point covers every path that creates claimable work — dispatcher, worker
/// fan-out, resumer, signaler, maintenance sweep — because they all stage step rows through
/// the change tracker. Retry rows (pending with a FUTURE <c>next_attempt_at</c>) are
/// deliberately not notified: nobody could claim them yet; the fallback poll picks them up
/// when due. The payload carries only the lane (<c>"fast"</c> / <c>"long"</c>) — never run
/// data, so no PHI crosses the notification channel. Postgres de-duplicates identical
/// (channel, payload) pairs within one transaction: a 10-step fan-out costs one notification.
/// </para>
/// <para>
/// Notify failures are swallowed with a warning: the flush itself succeeded, and a missed
/// notification only costs poll-interval latency. The instance is shared across DbContexts
/// (interceptors live on singleton DbContextOptions), hence the per-context capture table
/// instead of instance fields.
/// </para>
/// </summary>
internal sealed class WorkflowWorkNotifyInterceptor : SaveChangesInterceptor
{
    internal const string FastLanePayload = "fast";
    internal const string LongLanePayload = "long";

    private readonly string channel;

    // Lanes are computed BEFORE the save (afterwards entry states reset to Unchanged) but sent
    // AFTER it succeeded. ConditionalWeakTable: a context whose save threw (capture never
    // consumed) drops its entry with the context itself instead of leaking.
    private readonly ConditionalWeakTable<DbContext, ClaimableLanes> captures = new();

    public WorkflowWorkNotifyInterceptor(string channel)
    {
        this.channel = channel;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            var lanes = CollectClaimableLanes(context);
            if (lanes is not null)
            {
                this.captures.AddOrUpdate(context, lanes);
            }
            else
            {
                this.captures.Remove(context);
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && this.captures.TryGetValue(context, out var lanes))
        {
            this.captures.Remove(context);
            await this.NotifyAsync(context, lanes, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Scans the tracker for steps that are claimable the moment this flush commits. Modified
    /// entries count too (a row released back to pending), not just inserts.
    /// </summary>
    internal static ClaimableLanes? CollectClaimableLanes(DbContext context)
    {
        var fast = false;
        var @long = false;
        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<WorkflowStepExecution>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var step = entry.Entity;
            if (step.Status != StepExecutionStatus.Pending || step.NextAttemptAt > now)
            {
                continue;
            }

            if (step.IsLongRunning)
            {
                @long = true;
            }
            else
            {
                fast = true;
            }

            if (fast && @long)
            {
                break;
            }
        }

        return fast || @long ? new ClaimableLanes(fast, @long) : null;
    }

    private async Task NotifyAsync(DbContext context, ClaimableLanes lanes, CancellationToken cancellationToken)
    {
        try
        {
            if (lanes.Fast)
            {
                await context.Database.ExecuteSqlAsync(
                    $"SELECT pg_notify({this.channel}, {FastLanePayload})", cancellationToken);
            }

            if (lanes.Long)
            {
                await context.Database.ExecuteSqlAsync(
                    $"SELECT pg_notify({this.channel}, {LongLanePayload})", cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never fail the (already committed, or still-ambient) unit of work over a wake-up
            // hint. Realistically reachable only when the connection is already broken — and
            // then the ambient commit surfaces the real error itself.
            TryLogWarning(context, ex);
        }
    }

    private static void TryLogWarning(DbContext context, Exception ex)
    {
        try
        {
            context.GetService<ILoggerFactory>()
                .CreateLogger<WorkflowWorkNotifyInterceptor>()
                .LogWarning(ex, "pg_notify work push failed; workers will pick the work up on the fallback poll");
        }
        catch
        {
            // Logging is best-effort, like the notify itself.
        }
    }

    internal sealed record ClaimableLanes(bool Fast, bool Long);
}
