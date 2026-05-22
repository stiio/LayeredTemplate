using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowParentRunFkSetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_workflow_runs_workflow_runs_parent_run_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.AddForeignKey(
                name: "fk_workflow_runs_workflow_runs_parent_run_id",
                schema: "workflow",
                table: "workflow_runs",
                column: "parent_run_id",
                principalSchema: "workflow",
                principalTable: "workflow_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_workflow_runs_workflow_runs_parent_run_id",
                schema: "workflow",
                table: "workflow_runs");

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
    }
}
