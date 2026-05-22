using LayeredTemplate.Plugins.Workflow.Storage.EFCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Configurations;

internal class WorkflowRunConfiguration : IEntityTypeConfiguration<WorkflowRun>
{
    public void Configure(EntityTypeBuilder<WorkflowRun> builder)
    {
        builder.ToTable("workflow_runs");
        builder.HasKey(x => x.Id).HasName("pk_workflow_runs");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.DefinitionId).HasColumnName("definition_id");
        builder.Property(x => x.TriggerKind).HasColumnName("trigger_kind").HasMaxLength(64).IsRequired();
        builder.Property(x => x.TriggerSourceKind).HasColumnName("trigger_source_kind").HasMaxLength(64);
        builder.Property(x => x.TriggerSourceId).HasColumnName("trigger_source_id");
        builder.Property(x => x.IsDryRun).HasColumnName("is_dry_run");
        // Plaintext column — explicitly NOT routed through WorkflowProtectedStringConverter so
        // list / detail views can surface the label without per-row decryption. Caller contract
        // forbids PHI here; for PHI use protected step outputs.
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(256);
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.WorkflowSnapshot).HasColumnName("workflow_snapshot").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.FinishedAt).HasColumnName("finished_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.NestingLevel).HasColumnName("nesting_level");
        builder.Property(x => x.ParentRunId).HasColumnName("parent_run_id");
        builder.Property(x => x.ParentStepId).HasColumnName("parent_step_id");

        // Protected columns: column type + value converter set in WorkflowDbContext.OnModelCreating
        // (the converter needs the optional IWorkflowDataProtector resolved from DI). Configuration
        // here only locks names + nullability/required.
        builder.Property(x => x.ProtectionVersion).HasColumnName("protection_version").HasMaxLength(64);
        builder.Property(x => x.StaticContext).HasColumnName("static_context").IsRequired(); // old jsonb
        builder.Property(x => x.StepsOutputs).HasColumnName("steps_outputs").IsRequired(); // old jsonb
        builder.Property(x => x.AbortReason).HasColumnName("abort_reason"); // old varchar(200)
        builder.Property(x => x.ReturnValue).HasColumnName("return_value"); // old jsonb

        // Ownerless FK (no navigation) so EF doesn't create a shadow index on definition_id —
        // no application query filters by definition_id alone, and FK enforcement on definition
        // delete (rare admin op) can fall back to a sequential scan. If that ever becomes a
        // bottleneck we add a composite index that also serves a real query path.
        builder.HasOne<Entities.WorkflowDefinition>()
            .WithMany()
            .HasForeignKey(x => x.DefinitionId)
            .HasConstraintName("fk_workflow_runs_workflow_definitions_definition_id")
            .OnDelete(DeleteBehavior.Restrict);

        // Self-FK on parent run for sub-workflow chains. SET NULL — when a parent run is purged
        // by retention (or any future explicit delete), descendant children are orphaned but
        // stay alive. Critical for fire-and-forget sub-workflows: a parent that finishes early
        // and gets retention-purged would otherwise drag a still-suspended child (e.g. parked
        // on Approve / Delay) along with it under CASCADE — even though that child is doing
        // legitimate work and has no logical lifetime tie to the parent.
        // Why not RESTRICT: ExecuteDeleteAsync generates `DELETE WHERE id IN (SELECT … ORDER BY
        // finished_at LIMIT N)`. The ORDER BY only governs which rows enter the LIMIT subset —
        // it does NOT dictate the per-row delete order. Postgres' planner is free to delete the
        // parent before its children in the same statement, which under RESTRICT throws an FK
        // violation and aborts the whole purge batch. SET NULL side-steps the ordering hazard
        // (the FK is atomically nulled when the parent row goes away) without conflating FK
        // integrity with logical lifetime coupling.
        // ParentRunId is purely a back-pointer for trace / observability + the
        // MaxSubRunsPerRun count check; that count is only consulted while the parent is still
        // alive (a child can only be dispatched from a Running parent step), so nulling it on
        // parent purge is fine. Auto-resume of a wait-mode parent goes through ParentStepId
        // (already SET NULL), not ParentRunId.
        builder.HasOne<WorkflowRun>()
            .WithMany()
            .HasForeignKey(x => x.ParentRunId)
            .HasConstraintName("fk_workflow_runs_workflow_runs_parent_run_id")
            .OnDelete(DeleteBehavior.SetNull);

        // Parent-step FK with ON DELETE SET NULL: when a step row is purged (e.g. via the
        // retention sweeper), child runs whose parent_step_id pointed at it lose the back-pointer
        // gracefully instead of holding a dangling UUID. The auto-resume cascade relies on the
        // step still being Waiting; if it's gone the warning path in ResumeParentStepAsync logs
        // and skips, which matches the SET NULL semantics here.
        builder.HasOne<Entities.WorkflowStepExecution>()
            .WithMany()
            .HasForeignKey(x => x.ParentStepId)
            .HasConstraintName("fk_workflow_runs_workflow_step_executions_parent_step_id")
            .OnDelete(DeleteBehavior.SetNull);

        // Hot path for trace-by-source lookups (e.g. GetSubmissionWorkflowRunHandler).
        // Tenant-first so the (now mandatory) tenant filter benefits from index ordering.
        // Note truncated name — Postgres caps identifiers at 63 chars; trailing "_id" is lost.
        builder.HasIndex(x => new { x.TenantId, x.TriggerSourceKind, x.TriggerSourceId })
            .HasDatabaseName("ix_workflow_runs_tenant_id_trigger_source_kind_trigger_source_");
        // List/search queries: WHERE tenant_id = ? AND optional filters ORDER BY created_at DESC.
        // Composite (tenant_id, created_at DESC) drives both the tenant filter and the sort
        // without a sort step. Subsumes the standalone tenant_id index — Postgres uses the prefix.
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_workflow_runs_tenant_id_created_at");
        // Retention purge path: WHERE status IN (..) AND finished_at < threshold ORDER BY finished_at.
        builder.HasIndex(x => new { x.Status, x.FinishedAt })
            .HasDatabaseName("ix_workflow_runs_status_finished_at");
        // FanOut on completion looks up the parent step to resume.
        builder.HasIndex(x => x.ParentStepId)
            .HasDatabaseName("ix_workflow_runs_parent_step_id");
        builder.HasIndex(x => x.ParentRunId)
            .HasDatabaseName("ix_workflow_runs_parent_run_id");
        // Dropped vs prior schema:
        //   ix_workflow_runs_tenant_id    — superseded by (tenant_id, created_at)
        //   ix_workflow_runs_status       — no caller queries by status alone; status_finished_at covers the only path
        //   ix_workflow_runs_is_dry_run   — never queried; pure write tax
        //   ix_workflow_runs_definition_id — never queried; FK is enforced without an index in Postgres
    }
}
