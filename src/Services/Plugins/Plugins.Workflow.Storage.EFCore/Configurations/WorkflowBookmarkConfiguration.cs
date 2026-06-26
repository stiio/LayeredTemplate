using LayeredTemplate.Plugins.Workflow.Storage.EFCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Configurations;

/// <summary>
/// Maps the generic signal-wait bookmark. Naming is hand-rolled (no convention package), matching
/// the rest of the workflow schema. FK cascades from the owning run so a purged run drags its
/// bookmarks; the reconciliation sweep handles every other retirement path.
/// </summary>
internal class WorkflowBookmarkConfiguration : IEntityTypeConfiguration<WorkflowBookmark>
{
    public void Configure(EntityTypeBuilder<WorkflowBookmark> builder)
    {
        builder.ToTable("workflow_bookmark");
        builder.HasKey(x => x.Id).HasName("pk_workflow_bookmark");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.RunId).HasColumnName("run_id");
        builder.Property(x => x.StepId).HasColumnName("step_id");
        builder.Property(x => x.CorrelationKey).HasColumnName("correlation_key").HasMaxLength(256).IsRequired();
        builder.Property(x => x.ResumePort).HasColumnName("resume_port").HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        // Intra-engine FK → run, ON DELETE CASCADE. When a run is purged (retention) the bookmark
        // goes with it — eliminates the "run vanished, bookmark dangling" orphan class entirely.
        builder.HasOne(x => x.Run)
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .HasConstraintName("fk_workflow_bookmark_workflow_runs_run_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Signal lookup hot path: WHERE tenant_id = ? AND correlation_key = ?. Tenant-first so the
        // mandatory tenant filter benefits from index ordering.
        builder.HasIndex(x => new { x.TenantId, x.CorrelationKey })
            .HasDatabaseName("ix_workflow_bookmark_tenant_id_correlation_key");
        // FK index — Postgres doesn't auto-index FK columns; the cascade-delete scan on run purge
        // and any run-scoped cleanup hit this.
        builder.HasIndex(x => x.RunId)
            .HasDatabaseName("ix_workflow_bookmark_run_id");
        // Reconciliation sweep joins bookmark.step_id → step.id; the eager delete-on-resume also
        // targets a single bookmark whose step just flipped.
        builder.HasIndex(x => x.StepId)
            .HasDatabaseName("ix_workflow_bookmark_step_id");
    }
}
