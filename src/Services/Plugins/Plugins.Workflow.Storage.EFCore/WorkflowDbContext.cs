using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using Microsoft.EntityFrameworkCore;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore;

/// <summary>
/// Plugin-owned <see cref="DbContext"/> for the workflow engine. Internal by design — the
/// public surface is <see cref="IWorkflowStore"/> and <see cref="IWorkflowStorageMigrator"/>,
/// which is plenty for consumers. Keeping this hidden lets us evolve schema/columns without
/// breaking ABI for downstream code that might otherwise reach into the context directly.
/// <para>
/// All workflow tables live in their own Postgres schema (<see cref="SchemaName"/>) to keep
/// the engine's storage contained. Migration history is also schema-scoped so the plugin can
/// evolve without touching the consumer app's <c>__EFMigrationsHistory</c>.
/// </para>
/// </summary>
internal sealed class WorkflowDbContext : DbContext
{
    /// <summary>Postgres schema where every workflow table lives.</summary>
    public const string SchemaName = "workflow";

    private readonly IWorkflowDataProtector? protector;

    public WorkflowDbContext(
        DbContextOptions<WorkflowDbContext> options,
        IWorkflowDataProtector? protector = null)
        : base(options)
    {
        // Optional ctor param — DI resolves null when no implementation is registered.
        this.protector = protector;

        // Lazy loading is a footgun for a worker-style consumer: any accidental access to a
        // navigation (e.g. `step.Run.WorkflowSnapshot` from a custom action) would fire a fresh
        // query mid-batch. Disabled explicitly so future entity changes don't silently re-enable
        // it via a property setter. Eager / Explicit loads only.
        this.ChangeTracker.LazyLoadingEnabled = false;
    }

    public DbSet<Entities.WorkflowDefinition> WorkflowDefinitions => this.Set<Entities.WorkflowDefinition>();

    public DbSet<Entities.WorkflowRun> WorkflowRuns => this.Set<Entities.WorkflowRun>();

    public DbSet<Entities.WorkflowStepExecution> WorkflowStepExecutions => this.Set<Entities.WorkflowStepExecution>();

    public DbSet<Entities.WorkflowBookmark> WorkflowBookmarks => this.Set<Entities.WorkflowBookmark>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Pin every entity in this context to the dedicated schema. Per-table HasTable("…") in
        // the configuration files supplies the table name; the schema comes from here.
        modelBuilder.HasDefaultSchema(SchemaName);

        // All EF configurations live next to the entity types in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkflowDbContext).Assembly);

        // Schema-unification + optional encryption: every protected column lives as `bytea`
        // regardless of whether a protector is registered. Without protector, bytes are
        // UTF-8-encoded plaintext; with protector, bytes are [0x80 magic][ciphertext]. Same
        // schema, two payload formats, mixed-mode safe.
        // Two flavours of converter: string ↔ bytea for plain-text fields (AbortReason,
        // LastError) and JsonElement ↔ bytea for structured fields. JSON variant skips a
        // per-evaluation deserialize and prevents consumer code from stuffing malformed
        // strings into JSON-typed columns.
        var stringConverter = new WorkflowProtectedStringConverter(this.protector);
        var jsonConverter = new WorkflowProtectedJsonConverter(this.protector);

        modelBuilder.Entity<Entities.WorkflowRun>(b =>
        {
            b.Property(x => x.StaticContext).HasColumnType("bytea").HasConversion(jsonConverter!);
            b.Property(x => x.StepsOutputs).HasColumnType("bytea").HasConversion(jsonConverter!);
            b.Property(x => x.ReturnValue).HasColumnType("bytea").HasConversion(jsonConverter);
            b.Property(x => x.AbortReason).HasColumnType("bytea").HasConversion(stringConverter);
        });

        modelBuilder.Entity<Entities.WorkflowStepExecution>(b =>
        {
            b.Property(x => x.ResolvedConfig).HasColumnType("bytea").HasConversion(jsonConverter!);
            b.Property(x => x.Outputs).HasColumnType("bytea").HasConversion(jsonConverter);
            b.Property(x => x.LastError).HasColumnType("bytea").HasConversion(stringConverter);
        });
    }
}
