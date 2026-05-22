using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class SinglePortPerStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the new single-port column first; backfill from output_ports[0]; only then
            // drop the legacy columns. Saves the trace UI from showing blanks for completed
            // steps that already had a port recorded.
            migrationBuilder.AddColumn<string>(
                name: "output_port",
                schema: "workflow",
                table: "workflow_step_executions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE workflow.workflow_step_executions
                SET output_port = output_ports->>0
                WHERE output_ports IS NOT NULL
                  AND jsonb_typeof(output_ports) = 'array'
                  AND jsonb_array_length(output_ports) >= 1;
            ");

            migrationBuilder.DropColumn(
                name: "arrivals",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.DropColumn(
                name: "arrived_from_node_ids",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.DropColumn(
                name: "expected_arrivals",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.DropColumn(
                name: "output_ports",
                schema: "workflow",
                table: "workflow_step_executions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "arrivals",
                schema: "workflow",
                table: "workflow_step_executions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "arrived_from_node_ids",
                schema: "workflow",
                table: "workflow_step_executions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "expected_arrivals",
                schema: "workflow",
                table: "workflow_step_executions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "output_ports",
                schema: "workflow",
                table: "workflow_step_executions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            // Best-effort restore: wrap output_port in a 1-element array.
            migrationBuilder.Sql(@"
                UPDATE workflow.workflow_step_executions
                SET output_ports = jsonb_build_array(output_port)
                WHERE output_port IS NOT NULL;
            ");

            migrationBuilder.DropColumn(
                name: "output_port",
                schema: "workflow",
                table: "workflow_step_executions");
        }
    }
}
