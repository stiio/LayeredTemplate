using LayeredTemplate.Plugins.StartupRunner.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.StartupRunner.HostedServices;

internal class StartupRunnerHostedService : IHostedService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<StartupRunnerHostedService> logger;

    public StartupRunnerHostedService(IServiceProvider serviceProvider, ILogger<StartupRunnerHostedService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = this.serviceProvider.CreateScope();
        var startupRunner = scope.ServiceProvider.GetRequiredService<IStartupRunner>();

        try
        {
            await startupRunner.StartupAsync(cancellationToken);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Startup task exception");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}