using LayeredTemplate.Plugins.Workflow.Storage.EFCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Configurations;

internal class WorkflowStepExecutionConfiguration : IEntityTypeConfiguration<WorkflowStepExecution>
{
    public void Configure(EntityTypeBuilder<WorkflowStepExecution> builder)
    {
        builder.ToTable("workflow_step_executions");
        builder.HasKey(x => x.Id).HasName("pk_workflow_step_executions");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RunId).HasColumnName("run_id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.NodeId).HasColumnName("node_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(x => x.PredecessorExecutionId).HasColumnName("predecessor_execution_id");
        builder.Property(x => x.TriggerPort).HasColumnName("trigger_port").HasMaxLength(32);
        builder.Property(x => x.OutputPort).HasColumnName("output_port").HasMaxLength(32);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        // Drives lane routing in ClaimPending/Expired SQL. Stamped from IActionType.IsLongRunning
        // when the step is built; defaults to false for backward compat with legacy rows.
        builder.Property(x => x.IsLongRunning).HasColumnName("is_long_running").HasDefaultValue(false);

        // Protected columns: column type + value converter set in WorkflowDbContext.OnModelCreating.
        builder.Property(x => x.ResolvedConfig).HasColumnName("resolved_config").IsRequired(); // old jsonb
        builder.Property(x => x.Outputs).HasColumnName("outputs"); // old jsonb
        builder.Property(x => x.LastError).HasColumnName("last_error"); // old text

        builder.HasOne(x => x.Run)
            .WithMany(r => r.StepExecutions)
            .HasForeignKey(x => x.RunId)
            .HasConstraintName("fk_workflow_step_executions_workflow_runs_run_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Hot worker path: WHERE status = 'pending' AND is_long_running = ? AND next_attempt_at <= now()
        // ORDER BY next_attempt_at. PARTIAL index — only the tiny live-queue subset, not the
        // ever-growing terminal rows (Completed/Dead). At 10M+ row scale this collapses index
        // size by ~99%, makes the claim scan O(active queue) instead of O(history).
        // EF Core dedups HasIndex by column set unless you supply an explicit name to the
        // overload — the (expression, name) form forces two distinct indexes with the same
        // columns but different filters.
        builder.HasIndex(
                x => new { x.IsLongRunning, x.NextAttemptAt },
                "ix_workflow_step_executions_pending_lane_next_attempt")
            .HasFilter("status = 'pending'");
        // Same shape for the timeout sweeper: WHERE status='waiting' AND next_attempt_at <= now().
        // Waiting rows are the parked subset (Approve / Delay / RunWorkflow), much smaller than
        // pending+terminal. Partial keeps the sweep cheap regardless of historical row count.
        builder.HasIndex(
                x => new { x.IsLongRunning, x.NextAttemptAt },
                "ix_workflow_step_executions_waiting_lane_next_attempt")
            .HasFilter("status = 'waiting'");
        // GetStepsForRunAsync: WHERE run_id = ? ORDER BY created_at. Composite avoids an
        // in-memory sort once a run accumulates dozens of steps (legitimate for ForEach loops).
        // Subsumes the prior standalone run_id index for FK lookups (postgres uses the prefix).
        builder.HasIndex(x => new { x.RunId, x.CreatedAt })
            .HasDatabaseName("ix_workflow_step_executions_run_id_created_at");
        // Tenant-scoped purge / query path (e.g. "delete all PHI for tenant X").
        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("ix_workflow_step_executions_tenant_id");
    }
}
