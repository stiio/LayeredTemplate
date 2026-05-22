using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Configurations;

/// <summary>
/// Naming is hand-rolled (no <c>EFCore.NamingConventions</c>): every column / index / PK gets
/// an explicit snake_case name so the schema is locked in here, not derived from a third-party
/// convention. Keeps the contract visible at the type-config level instead of "magic somewhere".
/// </summary>
internal class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<Entities.WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<Entities.WorkflowDefinition> builder)
    {
        // Pin table name explicitly so namespace moves don't trigger destructive migrations.
        builder.ToTable("workflow_definitions");
        builder.HasKey(d => d.Id).HasName("pk_workflow_definitions");

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id");
        builder.Property(d => d.OwnerKind).HasColumnName("owner_kind").HasMaxLength(64).IsRequired();
        builder.Property(d => d.OwnerId).HasColumnName("owner_id");
        builder.Property(d => d.TriggerKind).HasColumnName("trigger_kind").HasMaxLength(64).IsRequired();
        builder.Property(d => d.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        builder.Property(d => d.Graph).HasColumnName("graph").HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");

        // Locator hot path: per-tenant lookup by owner + trigger.
        // Note the truncated index name — Postgres caps identifiers at 63 chars and the original
        // generated name lost its trailing "_kind". Keeping the historical truncation so EF
        // doesn't try to drop+recreate.
        builder.HasIndex(d => new { d.TenantId, d.OwnerKind, d.OwnerId, d.TriggerKind })
            .IsUnique()
            .HasDatabaseName("ix_workflow_definitions_tenant_id_owner_kind_owner_id_trigger_");
        // List/search hot path: WHERE tenant_id = ? AND optional filters ORDER BY created_at DESC.
        // Subsumes the prior standalone tenant_id index (postgres uses the prefix).
        builder.HasIndex(d => new { d.TenantId, d.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_workflow_definitions_tenant_id_created_at");
    }
}
