using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowTenantIndexAndParentStepFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workflow_runs_trigger_source_kind_trigger_source_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_tenant_id_trigger_source_kind_trigger_source_",
                schema: "workflow",
                table: "workflow_runs",
                columns: new[] { "tenant_id", "trigger_source_kind", "trigger_source_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_workflow_runs_workflow_step_executions_parent_step_id",
                schema: "workflow",
                table: "workflow_runs",
                column: "parent_step_id",
                principalSchema: "workflow",
                principalTable: "workflow_step_executions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_workflow_runs_workflow_step_executions_parent_step_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropIndex(
                name: "ix_workflow_runs_tenant_id_trigger_source_kind_trigger_source_",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_trigger_source_kind_trigger_source_id",
                schema: "workflow",
                table: "workflow_runs",
                columns: new[] { "trigger_source_kind", "trigger_source_id" });
        }
    }
}
