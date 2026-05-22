using Microsoft.Extensions.Hosting;

namespace LayeredTemplate.Plugins.Workflow.Engine.Services;

/// <summary>
/// Helper for <see cref="BackgroundService"/>s that should hold off real work until the host
/// has finished bringing up every <see cref="IHostedService"/> (most importantly the EF
/// migration runner). Without this gate, BackgroundService.StartAsync returns Task.CompletedTask
/// immediately and the worker loop races against migration, logging "relation does not exist"
/// for the first few polls of a cold start.
/// </summary>
internal static class HostStartupBarrier
{
    /// <summary>
    /// Awaits <see cref="IHostApplicationLifetime.ApplicationStarted"/>. Returns immediately if
    /// the application is already started. Honours <paramref name="stoppingToken"/> so a
    /// shutdown during host startup unwinds cleanly with <see cref="OperationCanceledException"/>.
    /// </summary>
    public static Task WaitAsync(IHostApplicationLifetime lifetime, CancellationToken stoppingToken)
    {
        if (lifetime.ApplicationStarted.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Registration is auto-cleaned by the runtime once ApplicationStarted fires; on the
        // shutdown-races-startup path WaitAsync below throws OCE and the registration is dropped
        // by GC eventually as the worker tears down.
        lifetime.ApplicationStarted.Register(
            static s => ((TaskCompletionSource)s!).TrySetResult(),
            tcs);
        return tcs.Task.WaitAsync(stoppingToken);
    }
}
