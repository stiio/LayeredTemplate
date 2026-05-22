using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowIndexCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_workflow_runs_workflow_runs_parent_run_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropIndex(
                name: "ix_workflow_step_executions_run_id",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.DropIndex(
                name: "ix_workflow_step_executions_status_lane_next_attempt_at",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.DropIndex(
                name: "ix_workflow_runs_is_dry_run",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropIndex(
                name: "ix_workflow_runs_status",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropIndex(
                name: "ix_workflow_runs_tenant_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.RenameIndex(
                name: "ix_workflow_runs_definition_id",
                schema: "workflow",
                table: "workflow_runs",
                newName: "IX_workflow_runs_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_pending_lane_next_attempt",
                schema: "workflow",
                table: "workflow_step_executions",
                columns: new[] { "is_long_running", "next_attempt_at" },
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_run_id_created_at",
                schema: "workflow",
                table: "workflow_step_executions",
                columns: new[] { "run_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_waiting_lane_next_attempt",
                schema: "workflow",
                table: "workflow_step_executions",
                columns: new[] { "is_long_running", "next_attempt_at" },
                filter: "status = 'waiting'");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_tenant_id_created_at",
                schema: "workflow",
                table: "workflow_runs",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.AddForeignKey(
                name: "fk_workflow_runs_workflow_runs_parent_run_id",
                schema: "workflow",
                table: "workflow_runs",
                column: "parent_run_id",
                principalSchema: "workflow",
                principalTable: "workflow_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_workflow_runs_workflow_runs_parent_run_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropIndex(
                name: "ix_workflow_step_executions_pending_lane_next_attempt",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.DropIndex(
                name: "ix_workflow_step_executions_run_id_created_at",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.DropIndex(
                name: "ix_workflow_step_executions_waiting_lane_next_attempt",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.DropIndex(
                name: "ix_workflow_runs_tenant_id_created_at",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.RenameIndex(
                name: "IX_workflow_runs_definition_id",
                schema: "workflow",
                table: "workflow_runs",
                newName: "ix_workflow_runs_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_run_id",
                schema: "workflow",
                table: "workflow_step_executions",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_status_lane_next_attempt_at",
                schema: "workflow",
                table: "workflow_step_executions",
                columns: new[] { "status", "is_long_running", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_is_dry_run",
                schema: "workflow",
                table: "workflow_runs",
                column: "is_dry_run");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_status",
                schema: "workflow",
                table: "workflow_runs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_tenant_id",
                schema: "workflow",
                table: "workflow_runs",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "fk_workflow_runs_workflow_runs_parent_run_id",
                schema: "workflow",
                table: "workflow_runs",
                column: "parent_run_id",
                principalSchema: "workflow",
                principalTable: "workflow_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
