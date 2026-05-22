using Fluid.Values;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Extensions;

/// <summary>
/// Registered by <c>AddWorkflowCore</c>. Exposes the workflow evaluation context as Liquid
/// globals so authors can write <c>{{ tenantId }}</c>, <c>{{ runId }}</c>, etc. without any
/// app-specific setup. Names match <see cref="DefaultContextJsExtension"/> (camelCase) so the
/// same identifier works in both Liquid and JS expressions. App-side extensions can layer
/// additional globals on top.
/// </summary>
internal sealed class DefaultContextLiquidExtension : ILiquidExtension
{
    public void Configure(LiquidExpressionContext context)
    {
        var ev = context.Evaluation;
        var opts = context.Options;

        context.TemplateContext.SetValue(
            "run",
            FluidValue.Create(new Dictionary<string, object?>()
            {
                ["tenantId"] = ev.TenantId,
                ["runId"] = ev.RunId,
                ["definitionId"] = ev.DefinitionId,
                ["actorUserId"] = ev.ActorUserId,
                ["triggerSourceKind"] = ev.TriggerSourceKind,
                ["triggerSourceId"] = ev.TriggerSourceId,
                ["isDryRun"] = ev.IsDryRun,
            }, opts));
    }
}
