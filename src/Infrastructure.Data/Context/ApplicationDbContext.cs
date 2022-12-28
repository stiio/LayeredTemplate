using System.Reflection;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Infrastructure.Data.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure.Data.Context;

internal class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly BaseEntitySaveChangesInterceptor? baseEntitySaveChangesInterceptor;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    [ActivatorUtilitiesConstructor]
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        BaseEntitySaveChangesInterceptor baseEntitySaveChangesInterceptor)
        : base(options)
    {
        this.baseEntitySaveChangesInterceptor = baseEntitySaveChangesInterceptor;
    }

    public DbSet<User> Users { get; set; } = null!;

    public DbSet<TodoList> TodoLists { get; set; } = null!;

    public IDbContextTransaction? CurrentTransaction => this.Database.CurrentTransaction;

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return this.Database.BeginTransactionAsync(cancellationToken);
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
        if (this.baseEntitySaveChangesInterceptor is not null)
        {
            optionsBuilder.AddInterceptors(this.baseEntitySaveChangesInterceptor);
        }

        base.OnConfiguring(optionsBuilder);
    }
}