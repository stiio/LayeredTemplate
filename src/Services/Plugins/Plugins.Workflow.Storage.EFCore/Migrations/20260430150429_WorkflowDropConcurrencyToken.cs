using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <summary>
    /// Marker migration for the removal of the optimistic-concurrency token (the <c>uint Xmin</c>
    /// property mapped to Postgres' <c>xmin</c> system column). Mirrors the same hand-edit as
    /// <c>WorkflowConcurrencyToken</c> — EF's scaffolder emits <c>DropColumn("xmin")</c> here too,
    /// because it doesn't know <c>xmin</c> is system-managed. <c>ALTER TABLE … DROP COLUMN xmin</c>
    /// would fail in Postgres for the same reason ADD COLUMN did. The DDL is removed; this
    /// migration only carries the corrected model snapshot so subsequent migrations diff against
    /// a no-Xmin baseline.
    /// </summary>
    public partial class WorkflowDropConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — xmin is a Postgres system column, no DDL needed.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty.
        }
    }
}
