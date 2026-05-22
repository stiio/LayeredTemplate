using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hipaa.Backend.Plugins.Workflow.Storage.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowGraphSingleStartNode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rewrite jsonb-stored graphs from { "startNodeIds": ["x"] } to { "startNodeId": "x" }.
            // Single-port-per-step engine = one entry point per graph; multi-start was removed
            // because it silently spawned parallel pipelines without explicit fan-out.
            // For graphs with > 1 start id we keep the first one (deterministic, by JSON array
            // order). Authors hitting that case will see a workflow that still runs from the same
            // first node it always did; the dropped pipelines silently disappear.

            // workflow_definitions.graph
            migrationBuilder.Sql(
                @"UPDATE workflow.workflow_definitions
                  SET graph = jsonb_set(
                      graph - 'startNodeIds',
                      '{startNodeId}',
                      COALESCE(graph->'startNodeIds'->0, 'null'::jsonb)
                  )
                  WHERE graph ? 'startNodeIds';");

            // workflow_runs.workflow_snapshot — same rewrite for already-running snapshots.
            // Snapshot is a frozen copy of the graph at run start, so format must match the
            // engine's expectation when it deserialises for fan-out / sweeper paths.
            migrationBuilder.Sql(
                @"UPDATE workflow.workflow_runs
                  SET workflow_snapshot = jsonb_set(
                      workflow_snapshot - 'startNodeIds',
                      '{startNodeId}',
                      COALESCE(workflow_snapshot->'startNodeIds'->0, 'null'::jsonb)
                  )
                  WHERE workflow_snapshot ? 'startNodeIds';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: { "startNodeId": "x" } → { "startNodeIds": ["x"] }. Null start node id
            // becomes an empty array (legacy "no start" representation).
            migrationBuilder.Sql(
                @"UPDATE workflow.workflow_definitions
                  SET graph = jsonb_set(
                      graph - 'startNodeId',
                      '{startNodeIds}',
                      CASE
                          WHEN graph->'startNodeId' IS NULL OR graph->'startNodeId' = 'null'::jsonb
                              THEN '[]'::jsonb
                          ELSE jsonb_build_array(graph->>'startNodeId')
                      END
                  )
                  WHERE graph ? 'startNodeId';");

            migrationBuilder.Sql(
                @"UPDATE workflow.workflow_runs
                  SET workflow_snapshot = jsonb_set(
                      workflow_snapshot - 'startNodeId',
                      '{startNodeIds}',
                      CASE
                          WHEN workflow_snapshot->'startNodeId' IS NULL OR workflow_snapshot->'startNodeId' = 'null'::jsonb
                              THEN '[]'::jsonb
                          ELSE jsonb_build_array(workflow_snapshot->>'startNodeId')
                      END
                  )
                  WHERE workflow_snapshot ? 'startNodeId';");
        }
    }
}
