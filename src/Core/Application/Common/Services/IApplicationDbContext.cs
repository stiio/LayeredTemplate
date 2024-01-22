using LayeredTemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredTemplate.Application.Common.Services;

public interface IApplicationDbContext : IApplicationDbConnection, IDisposable, IAsyncDisposable
{
    DbSet<User> Users { get; }

    DbSet<ApiKey> ApiKeys { get; }

    DbSet<T> Set<T>()
        where T : class;

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}