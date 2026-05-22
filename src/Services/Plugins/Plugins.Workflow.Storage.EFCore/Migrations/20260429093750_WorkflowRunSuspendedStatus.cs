using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowRunSuspendedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill: any Running run with at least one Waiting step is now semantically
            // Suspended. Without this, existing runs (an Approve waiting for human input,
            // a long Delay, a wait-mode RunWorkflow) stay tagged 'running' and would slip
            // through the new run-status state machine — and worse, get caught by stale-
            // running purge if enabled.
            //
            // Schema-only impact otherwise: 'status' is varchar(16), already accepts 'suspended'
            // (Running/Completed/Failed/Suspended all fit), no column change needed.
            migrationBuilder.Sql(
                @"UPDATE workflow.workflow_runs r
                  SET status = 'suspended'
                  WHERE r.status = 'running'
                    AND EXISTS (
                        SELECT 1 FROM workflow.workflow_step_executions s
                        WHERE s.run_id = r.id AND s.status = 'waiting'
                    );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse — collapse Suspended back to Running. The previous state machine had
            // no Suspended tier, so semantically all Suspended runs were Running before.
            migrationBuilder.Sql(
                @"UPDATE workflow.workflow_runs
                  SET status = 'running'
                  WHERE status = 'suspended';");
        }
    }
}
