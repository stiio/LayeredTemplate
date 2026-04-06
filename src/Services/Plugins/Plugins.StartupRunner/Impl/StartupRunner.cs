using LayeredTemplate.Plugins.StartupRunner.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Plugins.StartupRunner.Impl;

internal class StartupRunner : IStartupRunner
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<StartupRunner> logger;
    private readonly ICollection<Type> startupTaskTypes;

    public StartupRunner(IEnumerable<IStartupTask> startupTasks, IServiceScopeFactory scopeFactory, ILogger<StartupRunner> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        this.startupTaskTypes = startupTasks.OrderBy(x => x.Order).Select(x => x.GetType()).ToList();
    }

    public async Task StartupAsync(CancellationToken cancellationToken = default)
    {
        using var loggerScope = this.logger.BeginScope(new Dictionary<string, object>()
        {
            ["CorrelationId"] = Guid.NewGuid().ToString(),
        });

        foreach (var startupTaskType in this.startupTaskTypes)
        {
            await using var scope = this.scopeFactory.CreateAsyncScope();
            var startupTask = (IStartupTask)scope.ServiceProvider.GetRequiredService(startupTaskType);
            this.logger.LogInformation("Running startup task {StartupTaskName}", startupTaskType.Name);
            await startupTask.ExecuteAsync(cancellationToken);
        }
    }
}