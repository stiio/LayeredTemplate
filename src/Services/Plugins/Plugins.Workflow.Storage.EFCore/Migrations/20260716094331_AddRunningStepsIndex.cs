using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class AddRunningStepsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_running_updated_at",
                schema: "workflow",
                table: "workflow_step_executions",
                column: "updated_at",
                filter: "status = 'running'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workflow_step_executions_running_updated_at",
                schema: "workflow",
                table: "workflow_step_executions");
        }
    }
}
