using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class SubWorkflowChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "nesting_level",
                schema: "workflow",
                table: "workflow_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_run_id",
                schema: "workflow",
                table: "workflow_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_step_id",
                schema: "workflow",
                table: "workflow_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_parent_run_id",
                schema: "workflow",
                table: "workflow_runs",
                column: "parent_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_parent_step_id",
                schema: "workflow",
                table: "workflow_runs",
                column: "parent_step_id");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_workflow_runs_workflow_runs_parent_run_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropIndex(
                name: "ix_workflow_runs_parent_run_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropIndex(
                name: "ix_workflow_runs_parent_step_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropColumn(
                name: "nesting_level",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropColumn(
                name: "parent_run_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropColumn(
                name: "parent_step_id",
                schema: "workflow",
                table: "workflow_runs");
        }
    }
}
