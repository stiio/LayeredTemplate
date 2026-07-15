using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class AddStepStartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "started_at",
                schema: "workflow",
                table: "workflow_step_executions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "started_at",
                schema: "workflow",
                table: "workflow_step_executions");
        }
    }
}
