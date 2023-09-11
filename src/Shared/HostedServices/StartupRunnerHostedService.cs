using LayeredTemplate.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LayeredTemplate.Shared.HostedServices;

internal class StartupRunnerHostedService : IHostedService
{
    private readonly IServiceProvider serviceProvider;

    public StartupRunnerHostedService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = this.serviceProvider.CreateScope();
        var startupRunner = scope.ServiceProvider.GetRequiredService<IStartupRunner>();
        await startupRunner.StartupAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}