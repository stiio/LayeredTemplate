using System.Data;
using System.Reflection;
using Dapper;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Domain.Exceptions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using StackExchange.Profiling;
using StackExchange.Profiling.Data;

namespace LayeredTemplate.Infrastructure.Data.Context;

internal class ApplicationDbContext : DbContext, IDataProtectionKeyContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;

    public DbSet<TodoList> TodoLists { get; set; } = null!;

    public DbSet<AuditEvent> AuditEvents { get; set; } = null!;

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    internal IDbConnection DbConnection => new ProfiledDbConnection(this.Database.GetDbConnection(), MiniProfiler.Current);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return this.Database.BeginTransactionAsync(cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await base.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23503" })
        {
            throw new ForeignKeyViolationException(innerException: e);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new AlreadyExistsException(innerException: e);
        }
    }

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        return this.DbConnection.QueryAsync<T>(sql, param, this.Database.CurrentTransaction?.GetDbTransaction());
    }

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
    {
        return this.DbConnection.QueryFirstOrDefaultAsync<T>(sql, param, this.Database.CurrentTransaction?.GetDbTransaction());
    }

    public Task<T> QueryFirstAsync<T>(string sql, object? param = null)
    {
        return this.DbConnection.QueryFirstAsync<T>(sql, param, this.Database.CurrentTransaction?.GetDbTransaction());
    }

    public Task<T> QuerySingleAsync<T>(string sql, object? param = null)
    {
        return this.DbConnection.QuerySingleAsync<T>(sql, param, this.Database.CurrentTransaction?.GetDbTransaction());
    }

    public Task<int> ExecuteAsync(string sql, object? param = null)
    {
        return this.DbConnection.ExecuteAsync(sql, param, this.Database.CurrentTransaction?.GetDbTransaction());
    }

    public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        return this.DbConnection.ExecuteScalarAsync<T>(sql, param, this.Database.CurrentTransaction?.GetDbTransaction());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<string>()
            .HaveMaxLength(255);

        configurationBuilder.Properties<Enum>()
            .HaveConversion<string>()
            .HaveMaxLength(255);

        base.ConfigureConventions(configurationBuilder);
    }
}