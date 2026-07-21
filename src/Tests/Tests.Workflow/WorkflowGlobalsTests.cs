using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Graph;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;
using LayeredTemplate.Plugins.Workflow.Engine.Services;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// Definition-level globals: frozen into the run's static_context at start (same snapshot
/// semantics as the graph — the pair is authored together), exposed to expressions as
/// <c>globals.&lt;key&gt;</c> via the model builder's top-level-key lift.
/// </summary>
public class WorkflowGlobalsTests
{
    [Fact]
    public async Task Definition_globals_are_frozen_into_static_context()
    {
        var runner = new WorkflowRunner(new FakeBuilder(), new FakeStore());
        var definition = MakeDefinition(globalsJson: """{ "apiUrl": "https://staging.example", "retries": 3 }""");

        var run = await runner.StartAsync(
            MakeIntent(varsJson: """{ "answers": { "a": 1 } }"""), definition, CancellationToken.None);

        Assert.NotNull(run);
        var globals = run!.StaticContext.GetProperty(WorkflowGlobals.RootKey);
        Assert.Equal("https://staging.example", globals.GetProperty("apiUrl").GetString());
        Assert.Equal(3, globals.GetProperty("retries").GetInt32());
        // Existing namespaces stay intact next to the new slot.
        Assert.Equal(1, run.StaticContext.GetProperty("vars").GetProperty("answers").GetProperty("a").GetInt32());
        Assert.Equal("Manual", run.StaticContext.GetProperty("trigger").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Missing_globals_freeze_as_an_empty_object()
    {
        var runner = new WorkflowRunner(new FakeBuilder(), new FakeStore());

        var run = await runner.StartAsync(MakeIntent(), MakeDefinition(globalsJson: null), CancellationToken.None);

        // Always-an-object contract: strict-mode JS `globals.x` must resolve to undefined, not
        // throw ReferenceError on a missing root.
        var globals = run!.StaticContext.GetProperty(WorkflowGlobals.RootKey);
        Assert.Equal(JsonValueKind.Object, globals.ValueKind);
        Assert.Empty(globals.EnumerateObject());
    }

    [Fact]
    public async Task Non_object_globals_on_the_definition_are_rejected()
    {
        var runner = new WorkflowRunner(new FakeBuilder(), new FakeStore());
        var definition = MakeDefinition(globalsJson: null) with
        {
            Globals = JsonSerializer.SerializeToElement(42, WorkflowJsonOptions.Default),
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => runner.StartAsync(MakeIntent(), definition, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Globals_surface_in_the_expression_model()
    {
        // The model builder lifts every static_context top-level key, so globals reach the
        // engines with no model-builder changes — this pins that lift for the new slot.
        var runner = new WorkflowRunner(new FakeBuilder(), new FakeStore());
        var definition = MakeDefinition(globalsJson: """{ "apiUrl": "https://staging.example" }""");
        var run = await runner.StartAsync(MakeIntent(), definition, CancellationToken.None);

        var model = ExpressionModelBuilder.Build(
            run!.StaticContext, JsonSerializer.SerializeToElement(new { }, WorkflowJsonOptions.Default));

        var globals = Assert.IsType<Dictionary<string, object?>>(model[WorkflowGlobals.RootKey]);
        Assert.Equal("https://staging.example", globals["apiUrl"]);
    }

    [Theory]
    [InlineData("apiUrl", true)]
    [InlineData("_x1", true)]
    [InlineData("A", true)]
    [InlineData("1bad", false)]
    [InlineData("has-dash", false)]
    [InlineData("has space", false)]
    [InlineData("has.dot", false)]
    [InlineData("", false)]
    [InlineData("кириллица", false)]
    public void Key_validation_requires_identifier_shape(string key, bool valid)
        => Assert.Equal(valid, WorkflowGlobals.IsValidKey(key));

    [Fact]
    public void EnsureValid_rejects_non_objects_and_bad_keys()
    {
        Assert.Throws<ArgumentException>(() => WorkflowGlobals.EnsureValid(
            JsonSerializer.SerializeToElement("not an object", WorkflowJsonOptions.Default)));
        Assert.Throws<ArgumentException>(() => WorkflowGlobals.EnsureValid(
            JsonSerializer.Deserialize<JsonElement>("""{ "bad-key": 1 }""")));

        WorkflowGlobals.EnsureValid(JsonSerializer.Deserialize<JsonElement>("""{ "ok_key": { "nested": [1] } }"""));
    }

    private static WorkflowStartIntent MakeIntent(string? varsJson = null) => new()
    {
        TenantId = Guid.NewGuid(),
        TriggerKind = "Manual",
        Variables = varsJson is null ? null : JsonSerializer.Deserialize<JsonElement>(varsJson),
    };

    private static WorkflowDefinition MakeDefinition(string? globalsJson) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        OwnerKind = "Standalone",
        OwnerId = null,
        TriggerKind = "Manual",
        Graph = new WorkflowGraph
        {
            StartNodeId = "n1",
            Nodes =
            [
                new WorkflowNode
                {
                    Id = "n1",
                    Key = "start",
                    Kind = "Transform",
                    Config = JsonSerializer.SerializeToElement(new { }, WorkflowJsonOptions.Default),
                },
            ],
        },
        Globals = globalsJson is null ? null : JsonSerializer.Deserialize<JsonElement>(globalsJson),
    };
}
