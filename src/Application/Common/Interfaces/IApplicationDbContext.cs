using LayeredTemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredTemplate.Application.Common.Interfaces;

public interface IApplicationDbContext : IApplicationDbConnection, IDisposable, IAsyncDisposable
{
    DbSet<User> Users { get; }

    DbSet<TodoList> TodoLists { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}