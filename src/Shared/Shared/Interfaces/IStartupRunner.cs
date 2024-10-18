namespace LayeredTemplate.Shared.Interfaces;

public interface IStartupRunner
{
    Task StartupAsync(CancellationToken cancellationToken = default);
}