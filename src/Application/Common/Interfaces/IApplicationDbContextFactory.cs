namespace LayeredTemplate.Application.Common.Interfaces;

public interface IApplicationDbContextFactory
{
    Task<IApplicationDbContext> CreateAsync(CancellationToken cancellationToken = default);
}