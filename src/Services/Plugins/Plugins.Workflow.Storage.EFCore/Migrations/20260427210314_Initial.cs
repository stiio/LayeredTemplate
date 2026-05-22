using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workflow");

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trigger_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    graph = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_runs",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    trigger_source_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    trigger_source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_dry_run = table.Column<bool>(type: "boolean", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    workflow_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    static_context = table.Column<string>(type: "jsonb", nullable: false),
                    steps_outputs = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    abort_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_runs_workflow_definitions_definition_id",
                        column: x => x.definition_id,
                        principalSchema: "workflow",
                        principalTable: "workflow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_step_executions",
                schema: "workflow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    predecessor_execution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trigger_port = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    resolved_config = table.Column<string>(type: "jsonb", nullable: false),
                    outputs = table.Column<string>(type: "jsonb", nullable: true),
                    output_ports = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expected_arrivals = table.Column<int>(type: "integer", nullable: false),
                    arrivals = table.Column<int>(type: "integer", nullable: false),
                    arrived_from_node_ids = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_step_executions", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_step_executions_workflow_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "workflow",
                        principalTable: "workflow_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_tenant_id",
                schema: "workflow",
                table: "workflow_definitions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_tenant_id_owner_kind_owner_id_trigger_",
                schema: "workflow",
                table: "workflow_definitions",
                columns: new[] { "tenant_id", "owner_kind", "owner_id", "trigger_kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_definition_id",
                schema: "workflow",
                table: "workflow_runs",
                column: "definition_id");

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
                name: "ix_workflow_runs_status_finished_at",
                schema: "workflow",
                table: "workflow_runs",
                columns: new[] { "status", "finished_at" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_tenant_id",
                schema: "workflow",
                table: "workflow_runs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_trigger_source_kind_trigger_source_id",
                schema: "workflow",
                table: "workflow_runs",
                columns: new[] { "trigger_source_kind", "trigger_source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_run_id",
                schema: "workflow",
                table: "workflow_step_executions",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_status_next_attempt_at",
                schema: "workflow",
                table: "workflow_step_executions",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_tenant_id",
                schema: "workflow",
                table: "workflow_step_executions",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_step_executions",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "workflow_runs",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "workflow_definitions",
                schema: "workflow");
        }
    }
}
