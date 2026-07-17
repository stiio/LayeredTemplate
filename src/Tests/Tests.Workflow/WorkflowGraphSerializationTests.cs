using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Wire-format contract of the graph document. The store re-serializes the typed
/// <see cref="WorkflowGraph"/> on every save (UpsertDefinitionAsync), so an editor-visible
/// field survives persistence ONLY if the POCO declares it — these tests pin both sides of
/// that contract.
/// </summary>
public class WorkflowGraphSerializationTests
{
    [Fact]
    public void Descriptions_survive_the_store_round_trip()
    {
        var json = """
            {
              "startNodeId": "n1",
              "description": "whole-workflow note",
              "nodes": [
                { "id": "n1", "key": "a", "kind": "Transform", "description": "node note", "config": {} }
              ],
              "edges": []
            }
            """;

        var graph = JsonSerializer.Deserialize<WorkflowGraph>(json, WorkflowJsonOptions.Default)!;
        var stored = JsonSerializer.Serialize(graph, WorkflowJsonOptions.Default);
        var reloaded = JsonSerializer.Deserialize<WorkflowGraph>(stored, WorkflowJsonOptions.Default)!;

        Assert.Equal("whole-workflow note", reloaded.Description);
        Assert.Equal("node note", Assert.Single(reloaded.Nodes).Description);
    }

    [Fact]
    public void Null_descriptions_are_omitted_from_the_stored_json()
    {
        var graph = new WorkflowGraph
        {
            StartNodeId = "n1",
            Nodes =
            [
                new WorkflowNode
                {
                    Id = "n1",
                    Key = "a",
                    Kind = "Transform",
                    Config = JsonSerializer.SerializeToElement(new { }, WorkflowJsonOptions.Default),
                },
            ],
        };

        var stored = JsonSerializer.Serialize(graph, WorkflowJsonOptions.Default);

        Assert.DoesNotContain("description", stored);
    }

    [Fact]
    public void Undeclared_editor_fields_are_dropped_by_the_store_round_trip()
    {
        var json = """
            {
              "nodes": [
                { "id": "n1", "key": "a", "kind": "Transform", "config": {}, "futureEditorField": "x" }
              ],
              "edges": []
            }
            """;

        var graph = JsonSerializer.Deserialize<WorkflowGraph>(json, WorkflowJsonOptions.Default)!;
        var stored = JsonSerializer.Serialize(graph, WorkflowJsonOptions.Default);

        Assert.DoesNotContain("futureEditorField", stored);
    }
}
