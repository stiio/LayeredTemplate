namespace LayeredTemplate.Plugins.StartupRunner.Services;

public interface IStartupTask
{
    int Order { get; }

    Task ExecuteAsync(CancellationToken cancellationToken = default);
}