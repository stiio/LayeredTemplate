namespace LayeredTemplate.Shared.Interfaces;

public interface IStartupTask
{
    int Order { get; }

    Task ExecuteAsync(CancellationToken cancellationToken = default);
}