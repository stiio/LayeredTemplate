namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Extensions;

/// <summary>
/// Registered by <c>AddWorkflowCore</c>. Exposes the workflow evaluation context as JS globals
/// so authors can reference <c>tenantId</c>, <c>runId</c>, etc. directly. App-side extensions
/// can override or extend.
/// </summary>
internal sealed class DefaultContextJsExtension : IJsExtension
{
    public void Configure(JsExpressionContext context)
    {
        var ev = context.Evaluation;
        context.JintEngine.SetValue("run",
            new Dictionary<string, object?>()
            {
                ["tenantId"] = ev.TenantId.ToString(),
                ["runId"] = ev.RunId.ToString(),
                ["definitionId"] = ev.DefinitionId.ToString(),
                ["actorUserId"] = ev.ActorUserId?.ToString(),
                ["triggerSourceKind"] = ev.TriggerSourceKind,
                ["triggerSourceId"] = ev.TriggerSourceId?.ToString(),
                ["isDryRun"] = ev.IsDryRun,
            });
    }
}
