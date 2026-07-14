using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;
using LayeredTemplate.Plugins.Workflow.Engine;
using LayeredTemplate.Plugins.Workflow.Engine.Actions;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions.Engines;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions.Extensions;
using LayeredTemplate.Tests.Workflow.TestDoubles;
using Microsoft.Extensions.Options;
using Xunit;

namespace LayeredTemplate.Tests.Workflow;

/// <summary>
/// RunLiquid — rendering a dynamically-supplied template against the run context:
///   - the template sees the same {{ vars.* }} / {{ steps.* }} model config expressions see
///   - isJson=false (default) → `result` is the raw rendered string, even when it looks like JSON
///   - isJson=true → `result` is the parsed structure; unparseable output fails non-transiently
///   - empty template source / broken Liquid syntax fail non-transiently (deterministic errors,
///     retrying would repeat the same failure).
/// Uses the REAL LiquidExpressionEngine (cache + limits included), no fakes on the render path.
/// </summary>
public class RunLiquidActionTypeTests
{
    [Fact]
    public async Task Renders_vars_from_run_context()
    {
        var (action, run) = Make(staticContextJson: """{"vars":{"name":"Bob"}}""");

        var result = await action.ExecuteAsync(
            Context(run, "Hello {{ vars.name }}"), CancellationToken.None);

        Assert.Equal("done", result.OutputPort);
        Assert.Equal("Hello Bob", Outputs(result).GetProperty("result").GetString());
    }

    [Fact]
    public async Task Renders_prior_step_outputs()
    {
        var (action, run) = Make(stepsOutputsJson: """{"prev":{"total":42}}""");

        var result = await action.ExecuteAsync(
            Context(run, "total={{ steps.prev.total }}"), CancellationToken.None);

        Assert.Equal("total=42", Outputs(result).GetProperty("result").GetString());
    }

    [Fact]
    public async Task Without_isJson_result_stays_a_string_even_when_it_looks_like_json()
    {
        var (action, run) = Make();

        var result = await action.ExecuteAsync(
            Context(run, """{"a":1}"""), CancellationToken.None);

        var resultProp = Outputs(result).GetProperty("result");
        Assert.Equal(JsonValueKind.String, resultProp.ValueKind);
        Assert.Equal("""{"a":1}""", resultProp.GetString());
    }

    [Fact]
    public async Task With_isJson_result_is_structured()
    {
        var (action, run) = Make(staticContextJson: """{"vars":{"n":7}}""");

        var result = await action.ExecuteAsync(
            Context(run, """{"n": {{ vars.n }}, "list": [1,2]}""", isJson: true), CancellationToken.None);

        var resultProp = Outputs(result).GetProperty("result");
        Assert.Equal(7, resultProp.GetProperty("n").GetInt32());
        Assert.Equal(2, resultProp.GetProperty("list").GetArrayLength());
    }

    [Fact]
    public async Task With_isJson_unparseable_output_fails_non_transient()
    {
        var (action, run) = Make();

        var result = await action.ExecuteAsync(
            Context(run, "definitely not json", isJson: true), CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
        Assert.Null(result.OutputPort);
    }

    [Fact]
    public async Task Empty_template_fails_non_transient()
    {
        var (action, run) = Make();

        var result = await action.ExecuteAsync(
            Context(run, template: string.Empty), CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
    }

    [Fact]
    public async Task Broken_liquid_syntax_fails_non_transient()
    {
        var (action, run) = Make();

        var result = await action.ExecuteAsync(
            Context(run, "{% if %}unclosed"), CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.False(result.IsTransient);
    }

    // ----- harness -----

    private static (RunLiquidActionType Action, WorkflowRunRecord Run) Make(
        string staticContextJson = """{"vars":{}}""",
        string stepsOutputsJson = "{}")
    {
        var run = new WorkflowRunRecord
        {
            TenantId = Guid.NewGuid(),
            DefinitionId = Guid.NewGuid(),
            TriggerKind = "Test",
            WorkflowSnapshot = "{}",
            StaticContext = JsonSerializer.Deserialize<JsonElement>(staticContextJson),
            StepsOutputs = JsonSerializer.Deserialize<JsonElement>(stepsOutputsJson),
            Status = WorkflowRunStatus.Running,
            StartedAt = DateTime.UtcNow,
        };

        var liquid = new LiquidExpressionEngine(
            new LiquidTemplateCache(),
            Enumerable.Empty<ILiquidFilter>(),
            Enumerable.Empty<ILiquidExtension>(),
            Options.Create(new WorkflowEngineSettings()));

        var action = new RunLiquidActionType(
            new IExpressionEngine[] { liquid },
            new FakeStore(run));

        return (action, run);
    }

    private static ActionContext<RunLiquidConfig> Context(
        WorkflowRunRecord run, string template, bool isJson = false) => new()
    {
        Config = new RunLiquidConfig
        {
            Template = new Expr<string> { Engine = "static", Resolved = template },
            IsJson = isJson,
        },
        RunId = run.Id,
        StepExecutionId = Guid.NewGuid(),
        TenantId = run.TenantId,
        DefinitionId = run.DefinitionId,
        NodeKey = "liquid_1",
        StepsOutputs = run.StepsOutputs,
    };

    private static JsonElement Outputs(ActionExecutionResult result) =>
        JsonSerializer.SerializeToElement(result.Outputs, WorkflowJsonOptions.Default);
}
