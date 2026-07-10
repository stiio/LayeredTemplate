using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Engine;
using LayeredTemplate.Plugins.Workflow.Engine.Actions;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Microsoft.Extensions.Options;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Save-time structural validation of node configs (<c>node_config_invalid</c>): the validator
/// deserializes each node's <c>config</c> against the action's <c>ConfigType</c> with the same
/// options the dispatch-time resolver uses, so shape/type mismatches are rejected when the
/// author saves the graph instead of dead-lettering the first run. Structure only — expressions
/// are never evaluated here, so structurally-valid-but-semantically-broken templates still pass
/// (they fail at resolution, by design).
/// </summary>
public class WorkflowValidatorConfigTests
{
    [Fact]
    public void Bare_literal_in_expr_field_is_rejected_at_save_time()
    {
        // The classic mistake after the Expr migration: `"seconds": 30` instead of the
        // { engine, value } wrapper. Dispatch would throw on deserialize — validator says so now.
        var errors = Validate("""
            {
              "startNodeId": "n1",
              "nodes": [ { "id": "n1", "key": "n1", "kind": "Delay", "config": { "seconds": 30 } } ],
              "edges": []
            }
            """);

        Assert.Contains(errors, e => e.Code == "node_config_invalid" && e.Target == "n1");
    }

    [Fact]
    public void Proper_expr_wrapper_passes()
    {
        var errors = Validate("""
            {
              "startNodeId": "n1",
              "nodes": [ { "id": "n1", "key": "n1", "kind": "Delay",
                           "config": { "seconds": { "engine": "static", "value": "30" } } } ],
              "edges": []
            }
            """);

        Assert.DoesNotContain(errors, e => e.Code == "node_config_invalid");
    }

    [Fact]
    public void Absent_config_is_not_a_shape_error()
    {
        // Required-but-missing fields are a runtime concern (Delay errors non-transiently on a
        // null resolution); the validator only rejects configs that can't even deserialize.
        var errors = Validate("""
            {
              "startNodeId": "n1",
              "nodes": [ { "id": "n1", "key": "n1", "kind": "Delay" } ],
              "edges": []
            }
            """);

        Assert.DoesNotContain(errors, e => e.Code == "node_config_invalid");
    }

    [Fact]
    public void Expressions_are_not_evaluated_only_structure_is_checked()
    {
        // A liquid template with garbage syntax is still a structurally valid Expr — the
        // validator has no engines/model and must not run author code. It fails later, at
        // resolution, where the failure is attributed to the step.
        var errors = Validate("""
            {
              "startNodeId": "n1",
              "nodes": [ { "id": "n1", "key": "n1", "kind": "Delay",
                           "config": { "seconds": { "engine": "liquid", "value": "{{ definitely broken" } } } ],
              "edges": []
            }
            """);

        Assert.DoesNotContain(errors, e => e.Code == "node_config_invalid");
    }

    [Fact]
    public void Unknown_kind_reports_unknown_kind_without_config_check()
    {
        var errors = Validate("""
            {
              "startNodeId": "n1",
              "nodes": [ { "id": "n1", "key": "n1", "kind": "NoSuchAction", "config": { "seconds": 30 } } ],
              "edges": []
            }
            """);

        Assert.Contains(errors, e => e.Code == "node_unknown_kind");
        Assert.DoesNotContain(errors, e => e.Code == "node_config_invalid");
    }

    private static IReadOnlyList<LayeredTemplate.Plugins.Workflow.Abstractions.Services.WorkflowValidationError> Validate(string workflowJson)
    {
        var validator = new WorkflowValidator(
            new FakeRegistry(new DelayActionType()),
            Options.Create(new WorkflowEngineSettings()));
        return validator.Validate(JsonSerializer.Deserialize<JsonElement>(workflowJson));
    }
}
