using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowProtectedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF's auto-generated AlterColumn omits the USING clause, which Postgres requires
            // for jsonb→bytea / text→bytea casts. We replace the auto-scaffolded statements
            // with explicit SQL that converts existing UTF-8 text content into bytea — same
            // bytes, different column type. The bytes start with valid UTF-8 leading bytes
            // (`{`, `[`, `"`, ASCII letter, etc.), never with the engine's 0x80 magic byte,
            // so the converter on read treats them as plaintext.
            //
            // Six protected columns get migrated; for nullable columns we preserve NULL via
            // CASE WHEN, since `convert_to(NULL, 'UTF8')` would fail.

            // ---- workflow_runs ----
            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_runs
                    ALTER COLUMN static_context TYPE bytea
                    USING convert_to(static_context::text, 'UTF8');");

            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_runs
                    ALTER COLUMN steps_outputs TYPE bytea
                    USING convert_to(steps_outputs::text, 'UTF8');");

            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_runs
                    ALTER COLUMN return_value TYPE bytea
                    USING (CASE
                        WHEN return_value IS NULL THEN NULL
                        ELSE convert_to(return_value::text, 'UTF8')
                    END);");

            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_runs
                    ALTER COLUMN abort_reason TYPE bytea
                    USING (CASE
                        WHEN abort_reason IS NULL THEN NULL
                        ELSE convert_to(abort_reason, 'UTF8')
                    END);");

            // ---- workflow_step_executions ----
            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_step_executions
                    ALTER COLUMN resolved_config TYPE bytea
                    USING convert_to(resolved_config::text, 'UTF8');");

            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_step_executions
                    ALTER COLUMN outputs TYPE bytea
                    USING (CASE
                        WHEN outputs IS NULL THEN NULL
                        ELSE convert_to(outputs::text, 'UTF8')
                    END);");

            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_step_executions
                    ALTER COLUMN last_error TYPE bytea
                    USING (CASE
                        WHEN last_error IS NULL THEN NULL
                        ELSE convert_to(last_error, 'UTF8')
                    END);");

            // ---- protection_version stamps (no data migration needed; nullable from inception) ----
            migrationBuilder.AddColumn<string>(
                name: "protection_version",
                schema: "workflow",
                table: "workflow_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "protection_version",
                schema: "workflow",
                table: "workflow_step_executions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversing is dangerous when any row has been encrypted: ciphertext is not valid
            // UTF-8 (let alone valid JSON), so the convert_from + ::jsonb cast will fail. Down
            // is safe only on a database that never had IWorkflowDataProtector enabled, OR
            // after running a one-off decryption pass that re-writes every protected column as
            // plaintext bytes.
            //
            // Guard: refuse to proceed if any row has been stamped with a protection_version.
            // Operators who really mean it must clear the column manually after migrating the
            // data offline (or pass through a dedicated decryption migration first).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM workflow.workflow_runs WHERE protection_version IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM workflow.workflow_step_executions WHERE protection_version IS NOT NULL) THEN
                        RAISE EXCEPTION
                            'Refusing to revert WorkflowProtectedColumns: encrypted rows present (protection_version IS NOT NULL). '
                            'Run a one-off decryption pass that nulls protection_version on every row before downgrading.';
                    END IF;
                END $$;");

            migrationBuilder.DropColumn(
                name: "protection_version",
                schema: "workflow",
                table: "workflow_step_executions");

            migrationBuilder.DropColumn(
                name: "protection_version",
                schema: "workflow",
                table: "workflow_runs");

            // ---- workflow_step_executions ----
            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_step_executions
                    ALTER COLUMN last_error TYPE text
                    USING (CASE
                        WHEN last_error IS NULL THEN NULL
                        ELSE convert_from(last_error, 'UTF8')
                    END);");

            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_step_executions
                    ALTER COLUMN outputs TYPE jsonb
                    USING (CASE
                        WHEN outputs IS NULL THEN NULL
                        ELSE convert_from(outputs, 'UTF8')::jsonb
                    END);");

            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_step_executions
                    ALTER COLUMN resolved_config TYPE jsonb
                    USING convert_from(resolved_config, 'UTF8')::jsonb;");

            // ---- workflow_runs ----
            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_runs
                    ALTER COLUMN abort_reason TYPE character varying(200)
                    USING (CASE
                        WHEN abort_reason IS NULL THEN NULL
                        ELSE convert_from(abort_reason, 'UTF8')::varchar(200)
                    END);");

            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_runs
                    ALTER COLUMN return_value TYPE jsonb
                    USING (CASE
                        WHEN return_value IS NULL THEN NULL
                        ELSE convert_from(return_value, 'UTF8')::jsonb
                    END);");

            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_runs
                    ALTER COLUMN steps_outputs TYPE jsonb
                    USING convert_from(steps_outputs, 'UTF8')::jsonb;");

            migrationBuilder.Sql(@"
                ALTER TABLE workflow.workflow_runs
                    ALTER COLUMN static_context TYPE jsonb
                    USING convert_from(static_context, 'UTF8')::jsonb;");
        }
    }
}
