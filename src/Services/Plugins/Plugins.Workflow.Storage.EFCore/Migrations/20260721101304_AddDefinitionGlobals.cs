using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class AddDefinitionGlobals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "globals",
                schema: "workflow",
                table: "workflow_definitions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "globals",
                schema: "workflow",
                table: "workflow_definitions");
        }
    }
}
