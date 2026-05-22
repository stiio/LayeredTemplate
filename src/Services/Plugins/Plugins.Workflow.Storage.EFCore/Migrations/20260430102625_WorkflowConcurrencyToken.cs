using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <summary>
    /// Historical marker — added an xmin-based optimistic-concurrency token, then later reverted
    /// by <c>WorkflowDropConcurrencyToken</c>. Kept as a no-op so the migration history chain
    /// stays continuous on already-migrated databases. New deployments apply this back-to-back
    /// with the revert and never see a real concurrency-token column either.
    /// </summary>
    public partial class WorkflowConcurrencyToken : Migration
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
