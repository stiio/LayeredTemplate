using System.Text.Json;
using LayeredTemplate.Plugins.Workflow.Abstractions.Actions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Services;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Actions;

/// <summary>
/// Renders a <b>dynamically supplied</b> Liquid template against the full run context
/// (<c>vars</c> / <c>steps</c> / <c>trigger</c>) and stamps the output as
/// <c>steps.&lt;key&gt;.result</c>. The point is templates whose TEXT is not authored in the
/// graph: pulled from run variables, produced by a previous step, fetched by an integration —
/// graph-authored templates don't need this action, any <c>Expr</c> config field already
/// renders them during config resolution (see Transform).
/// <para>
/// The render goes through the SAME engine as config expressions — compiled-template cache,
/// registered <c>ILiquidFilter</c>s / <c>ILiquidExtension</c>s, MaxSteps and
/// <c>MaxLiquidOutputChars</c> all apply, and the evaluation carries the run's tenant identity.
/// Security model is therefore unchanged: whoever supplies the template text gets exactly the
/// power a graph author has — the sandbox (filters as the trust boundary, no CLR access) is
/// the containment, same as everywhere else.
/// </para>
/// <para>
/// Config nuance: <see cref="RunLiquidConfig.Template"/> resolves to the Liquid SOURCE. Written
/// as a <c>static</c> expression it carries the source verbatim; written as a <c>liquid</c>
/// expression it is itself rendered first (at step build) and the OUTPUT becomes the source
/// this action renders — a deliberate double render. Mind what that first render interpolates:
/// anything containing <c>{{ }}</c> gets executed by the second pass.
/// </para>
/// </summary>
public class RunLiquidActionType : ActionType<RunLiquidConfig>
{
    public const string KindName = "RunLiquid";

    private const string PortDone = "done";

    public static readonly IReadOnlyList<ActionPortDescriptor> Ports = new[]
    {
        new ActionPortDescriptor(PortDone, "Done", ActionPortKind.Normal),
    };

    private readonly IEnumerable<IExpressionEngine> engines;
    private readonly IWorkflowStore store;

    public RunLiquidActionType(IEnumerable<IExpressionEngine> engines, IWorkflowStore store)
    {
        this.engines = engines;
        this.store = store;
    }

    public override string Kind => KindName;

    public override string DisplayName => "Run Liquid template";

    public override IReadOnlyList<ActionPortDescriptor> OutputPorts => Ports;

    public override async Task<ActionExecutionResult> ExecuteAsync(
        ActionContext<RunLiquidConfig> context, CancellationToken cancellationToken)
    {
        var template = context.Config.Template?.Resolved;
        if (string.IsNullOrEmpty(template))
        {
            return this.Error(
                "RunLiquid 'template' must resolve to non-empty Liquid source.",
                transient: false);
        }

        var liquid = this.engines.FirstOrDefault(e => e.Name == ExpressionEngines.Liquid);
        if (liquid is null)
        {
            // Defensive — the core engine always registers liquid; only a heavily customised
            // host could remove it, and that host shouldn't offer this action.
            return this.Error("No 'liquid' expression engine is registered.", transient: false);
        }

        var run = await this.store.GetRunAsync(context.RunId, cancellationToken);
        if (run is null)
        {
            return this.Error($"Run {context.RunId} not found.", transient: false);
        }

        // Same model + evaluation identity every config expression sees — the dynamic template
        // reads {{ vars.* }} / {{ steps.* }} / {{ trigger.* }} exactly like a graph-authored one.
        var model = ExpressionModelBuilder.Build(run.StaticContext, run.StepsOutputs);
        var evaluation = ExpressionModelBuilder.EvaluationContextForRun(run);

        string rendered;
        try
        {
            var element = await liquid.EvaluateAsync(template, model, typeof(string), evaluation, cancellationToken);
            rendered = element.GetString() ?? string.Empty;
        }
        catch (ExpressionResolutionException ex)
        {
            // Deterministic for a given template + context (syntax error, filter rejecting its
            // input, output-cap overflow) — retrying would burn attempts on a guaranteed repeat.
            return this.Error($"Liquid render failed: {ex.Message}", transient: false);
        }

        if (!context.Config.IsJson)
        {
            return this.Port(PortDone, new { result = rendered });
        }

        try
        {
            using var doc = JsonDocument.Parse(rendered);
            return this.Port(PortDone, new { result = doc.RootElement.Clone() });
        }
        catch (JsonException ex)
        {
            var preview = rendered[..Math.Min(80, rendered.Length)];
            return this.Error(
                $"isJson=true but the rendered output is not valid JSON ({preview}…): {ex.Message}",
                transient: false);
        }
    }
}

public class RunLiquidConfig
{
    /// <summary>
    /// Resolves to the Liquid SOURCE this action renders. Use a <c>static</c> expression for
    /// verbatim source, or a liquid/js expression that fetches the source from run data
    /// (<c>{{ vars.emailTemplate }}</c>) — see the class remarks on the double-render nuance.
    /// Must resolve non-empty, otherwise the step fails non-transiently.
    /// </summary>
    public Expr<string>? Template { get; set; }

    /// <summary>
    /// When true, the rendered output is parsed as JSON and <c>result</c> carries the
    /// structured value (readable as <c>steps.&lt;key&gt;.result.field</c>); a parse failure
    /// fails the step non-transiently. Default false: <c>result</c> is the raw rendered string.
    /// Deliberately explicit rather than parse-if-possible — auto-detection would silently turn
    /// a template that happens to render "1999" into a number.
    /// </summary>
    public bool IsJson { get; set; }
}
