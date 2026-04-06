namespace LayeredTemplate.Plugins.StartupRunner.Services;

public interface IStartupRunner
{
    Task StartupAsync(CancellationToken cancellationToken = default);
}