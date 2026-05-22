using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowLongRunningLane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workflow_step_executions_status_next_attempt_at",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.AddColumn<bool>(
                name: "is_long_running",
                schema: "workflow",
                table: "workflow_step_executions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_status_lane_next_attempt_at",
                schema: "workflow",
                table: "workflow_step_executions",
                columns: new[] { "status", "is_long_running", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workflow_step_executions_status_lane_next_attempt_at",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.DropColumn(
                name: "is_long_running",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_status_next_attempt_at",
                schema: "workflow",
                table: "workflow_step_executions",
                columns: new[] { "status", "next_attempt_at" });
        }
    }
}
