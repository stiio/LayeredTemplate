using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LayeredTemplate.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
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
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    workflow_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    static_context = table.Column<byte[]>(type: "bytea", nullable: false),
                    steps_outputs = table.Column<byte[]>(type: "bytea", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    abort_reason = table.Column<byte[]>(type: "bytea", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    return_value = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    nesting_level = table.Column<int>(type: "integer", nullable: false),
                    parent_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_step_id = table.Column<Guid>(type: "uuid", nullable: true)
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
                    table.ForeignKey(
                        name: "fk_workflow_runs_workflow_runs_parent_run_id",
                        column: x => x.parent_run_id,
                        principalSchema: "workflow",
                        principalTable: "workflow_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
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
                    resolved_config = table.Column<byte[]>(type: "bytea", nullable: false),
                    is_long_running = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    outputs = table.Column<byte[]>(type: "bytea", nullable: true),
                    output_port = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<byte[]>(type: "bytea", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                name: "ix_workflow_definitions_tenant_id_created_at",
                schema: "workflow",
                table: "workflow_definitions",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_tenant_id_owner_kind_owner_id_trigger_",
                schema: "workflow",
                table: "workflow_definitions",
                columns: new[] { "tenant_id", "owner_kind", "owner_id", "trigger_kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_runs_definition_id",
                schema: "workflow",
                table: "workflow_runs",
                column: "definition_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_status_finished_at",
                schema: "workflow",
                table: "workflow_runs",
                columns: new[] { "status", "finished_at" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_tenant_id_created_at",
                schema: "workflow",
                table: "workflow_runs",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_runs_tenant_id_trigger_source_kind_trigger_source_",
                schema: "workflow",
                table: "workflow_runs",
                columns: new[] { "tenant_id", "trigger_source_kind", "trigger_source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_pending_lane_next_attempt",
                schema: "workflow",
                table: "workflow_step_executions",
                columns: new[] { "is_long_running", "next_attempt_at" },
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_run_id_created_at",
                schema: "workflow",
                table: "workflow_step_executions",
                columns: new[] { "run_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_tenant_id",
                schema: "workflow",
                table: "workflow_step_executions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_waiting_lane_next_attempt",
                schema: "workflow",
                table: "workflow_step_executions",
                columns: new[] { "is_long_running", "next_attempt_at" },
                filter: "status = 'waiting'");

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
                name: "fk_workflow_runs_workflow_definitions_definition_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropForeignKey(
                name: "fk_workflow_runs_workflow_step_executions_parent_step_id",
                schema: "workflow",
                table: "workflow_runs");

            migrationBuilder.DropTable(
                name: "workflow_definitions",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "workflow_step_executions",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "workflow_runs",
                schema: "workflow");
        }
    }
}
