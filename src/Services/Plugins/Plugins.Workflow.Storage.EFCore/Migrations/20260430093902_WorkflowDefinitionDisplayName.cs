using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowDefinitionDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workflow_definitions_tenant_id",
                schema: "workflow",
                table: "workflow_definitions");

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                schema: "workflow",
                table: "workflow_definitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_tenant_id_created_at",
                schema: "workflow",
                table: "workflow_definitions",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workflow_definitions_tenant_id_created_at",
                schema: "workflow",
                table: "workflow_definitions");

            migrationBuilder.DropColumn(
                name: "display_name",
                schema: "workflow",
                table: "workflow_definitions");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_tenant_id",
                schema: "workflow",
                table: "workflow_definitions",
                column: "tenant_id");
        }
    }
}
