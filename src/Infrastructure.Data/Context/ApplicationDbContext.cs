using System.Reflection;
using Humanizer;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Domain.Exceptions;
using LayeredTemplate.Infrastructure.Data.Interceptors;
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

    public DbSet<TodoList> TodoLists { get; set; } = null!;

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    public IDbContextTransaction? CurrentTransaction => this.Database.CurrentTransaction;

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
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23503" } postgresException)
        {
            throw new ForeignKeyViolationException(postgresException.ConstraintName);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" } postgresException)
        {
            throw new DuplicateUniqueColumnException(postgresException.TableName?.Humanize(LetterCasing.Title), postgresException.ColumnName?.Humanize());
        }
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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new BaseEntitySaveChangesInterceptor());

        base.OnConfiguring(optionsBuilder);
    }
}