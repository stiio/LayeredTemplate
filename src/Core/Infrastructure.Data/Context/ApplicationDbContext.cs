using System.Data;
using System.Reflection;
using Dapper;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Domain.Exceptions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace LayeredTemplate.Infrastructure.Data.Context;

internal class ApplicationDbContext : DbContext, IDataProtectionKeyContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    internal IDbConnection DbConnection => this.Database.GetDbConnection();

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

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        return this.DbConnection.QueryAsync<T>(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            flags: CommandFlags.NoCache,
            cancellationToken: cancellationToken));
    }

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        return this.DbConnection.QueryFirstOrDefaultAsync<T>(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            flags: CommandFlags.NoCache,
            cancellationToken: cancellationToken));
    }

    public Task<T> QueryFirstAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        return this.DbConnection.QueryFirstAsync<T>(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            flags: CommandFlags.NoCache,
            cancellationToken: cancellationToken));
    }

    public Task<T> QuerySingleAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        return this.DbConnection.QuerySingleAsync<T>(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            flags: CommandFlags.NoCache,
            cancellationToken: cancellationToken));
    }

    public Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        return this.DbConnection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            flags: CommandFlags.NoCache,
            cancellationToken: cancellationToken));
    }

    public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
    {
        return this.DbConnection.ExecuteScalarAsync<T>(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            flags: CommandFlags.NoCache,
            cancellationToken: cancellationToken));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<string>()
            .HaveMaxLength(256);

        configurationBuilder.Properties<Enum>()
            .HaveConversion<string>()
            .HaveMaxLength(256);

        base.ConfigureConventions(configurationBuilder);
    }
}