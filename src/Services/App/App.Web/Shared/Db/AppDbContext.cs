using System.Data;
using System.Reflection;
using Dapper;
using LayeredTemplate.App.Shared.Errors;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace LayeredTemplate.App.Shared.Db;

/// <summary>
/// Application's single <see cref="DbContext"/>. Declared as <c>partial</c> so each feature can
/// declare its own <see cref="DbSet{TEntity}"/> in a partial file inside the feature folder
/// (see <c>Features/Users/_DbContext.cs</c> etc.). This keeps a feature's DB surface visible
/// inside its own slice rather than buried in a shared "god" class.
/// </summary>
/// <remarks>
/// SaveChangesAsync catches Postgres-specific FK/unique violation SQLSTATE codes and translates
/// them into domain exceptions (<see cref="ForeignKeyViolationException"/>, <see cref="AlreadyExistsException"/>)
/// so callers can pattern-match on intent instead of digging through inner exception chains.
///
/// Dapper helpers are exposed for raw SQL queries that go through EF's connection — the in-flight
/// transaction (if any) is wired through so EF + Dapper share a transactional context.
/// </remarks>
public partial class AppDbContext : DbContext, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

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
            return await base.SaveChangesAsync(cancellationToken);
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

    // --- Dapper passthroughs (shared connection + ambient transaction) ---

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default) =>
        this.DbConnection.QueryAsync<T>(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            cancellationToken: cancellationToken));

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default) =>
        this.DbConnection.QueryFirstOrDefaultAsync<T>(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            cancellationToken: cancellationToken));

    public Task<T> QueryFirstAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default) =>
        this.DbConnection.QueryFirstAsync<T>(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            cancellationToken: cancellationToken));

    public Task<T> QuerySingleAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default) =>
        this.DbConnection.QuerySingleAsync<T>(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            cancellationToken: cancellationToken));

    public Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default) =>
        this.DbConnection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            cancellationToken: cancellationToken));

    public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default) =>
        this.DbConnection.ExecuteScalarAsync<T>(new CommandDefinition(
            sql,
            parameters: param,
            transaction: this.Database.CurrentTransaction?.GetDbTransaction(),
            cancellationToken: cancellationToken));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Picks up every IEntityTypeConfiguration<T> in the assembly — feature slices put theirs
        // in `Features/<Feature>/_DbConfig.cs`. No central registration needed.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<string>().HaveMaxLength(256);
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(256);

        base.ConfigureConventions(configurationBuilder);
    }
}
