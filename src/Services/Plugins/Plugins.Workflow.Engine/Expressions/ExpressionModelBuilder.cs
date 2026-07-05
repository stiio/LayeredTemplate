using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Models;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions;

/// <summary>
/// Assembles the model dictionary handed to expression engines (Liquid, JS, Static) for each
/// <c>Expr&lt;T&gt;</c> evaluation. Combines the trigger-supplied static context with the
/// run-accumulated <c>steps</c> outputs so authors can read both
/// <c>{{ vars.answers.email }}</c> (static, under the consumer-supplied <c>vars</c> namespace)
/// and <c>{{ steps.previous.foo }}</c> (dynamic) uniformly.
/// </summary>
/// <remarks>
/// Engine-agnostic: same model object goes to Liquid, JS, and Static. Renamed from
/// <c>WorkflowHelpers.BuildLiquidModel</c> — the old name implied Liquid-specific behavior.
/// </remarks>
internal static class ExpressionModelBuilder
{
    /// <summary>
    /// Returns a model = <c>staticContext</c>'s two namespace keys (<c>trigger</c> +
    /// <c>vars</c>) lifted to the top level + <c>steps</c> dictionary keyed by completed-node
    /// <c>Key</c>. Both <see cref="JsonElement"/> inputs come from the run record:
    /// <c>StaticContext</c> is set once at run start by the trigger
    /// (shape: <c>{ trigger: {...}, vars: {...} }</c>); <c>StepsOutputs</c> is appended to as
    /// steps complete.
    /// </summary>
    public static Dictionary<string, object?> Build(
        JsonElement staticContext,
        JsonElement stepsOutputs)
    {
        var model = JsonElementToClr(staticContext) as Dictionary<string, object?>
            ?? new Dictionary<string, object?>();
        model["steps"] = JsonElementToClr(stepsOutputs);
        return model;
    }

    /// <summary>
    /// The per-evaluation tenant/run context, built uniformly from the run record. Shared by
    /// the build-time resolve (StepExecutionBuilder) and the execute-time transient resolve
    /// (worker / resumer) so both phases evaluate under an identical identity.
    /// </summary>
    public static ExpressionEvaluationContext EvaluationContextForRun(WorkflowRunRecord run) => new()
    {
        TenantId = run.TenantId,
        RunId = run.Id,
        DefinitionId = run.DefinitionId,
        ActorUserId = run.ActorUserId,
        TriggerSourceKind = run.TriggerSourceKind,
        TriggerSourceId = run.TriggerSourceId,
        IsDryRun = run.IsDryRun,
    };

    /// <summary>
    /// Converts a <see cref="JsonElement"/> tree into nested CLR primitives / List / Dictionary so
    /// Fluid (which treats <see cref="IDictionary{TKey,TValue}"/> as a scope) and Jint (which marshals
    /// dictionaries as plain JS objects) both walk it without engine-specific hooks.
    /// </summary>
    public static object? JsonElementToClr(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToClr(p.Value)),
            JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToClr).ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? (object?)i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
