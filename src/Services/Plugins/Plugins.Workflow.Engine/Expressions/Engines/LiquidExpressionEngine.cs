using System.Text.Json;
using Fluid;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions.Extensions;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Engines;

/// <summary>
/// Liquid renders to a string. For <c>Expr&lt;string&gt;</c> the rendered output is used directly.
/// For other target types the output must be a valid JSON literal that parses to the target.
/// <para>
/// Pulls compiled-template cache from a singleton (no re-compilation per request) but resolves
/// <see cref="ILiquidExtension"/>s per scope so they can take other scoped services
/// (DbContext, etc.) via constructor.
/// </para>
/// </summary>
internal class LiquidExpressionEngine : IExpressionEngine
{
    /// <summary>
    /// Hard cap on Liquid statements per render. Without this Fluid's <c>MaxSteps</c> defaults
    /// to 0 = unlimited, which lets a workflow author block the worker thread with
    /// <c>{% for i in (1..1000000) %}{% endfor %}</c> — Liquid is synchronous and not
    /// cancellable, so the only protection is a step limit. 100k is enough for legitimate
    /// templates (typical email body 50–300 steps; DOCX-style massive merges low thousands)
    /// while immediately tripping loop bombs.
    /// </summary>
    private const int MaxStepsLimit = 100_000;

    /// <summary>
    /// Recursion depth cap. Fluid's default is 100 already; set explicitly for visibility and
    /// to lock the value against future Fluid version changes. Recursion comes from nested
    /// <c>{% include %}</c> / <c>{% render %}</c>; we don't register an <c>IFileProvider</c>
    /// so includes are inert, but the limit doubles as a safety net for anyone who later
    /// wires one up.
    /// </summary>
    private const int MaxRecursionLimit = 100;

    private readonly ILiquidTemplateCache cache;
    private readonly IEnumerable<ILiquidFilter> filters;
    private readonly IEnumerable<ILiquidExtension> extensions;
    private readonly WorkflowEngineSettings settings;

    public LiquidExpressionEngine(
        ILiquidTemplateCache cache,
        IEnumerable<ILiquidFilter> filters,
        IEnumerable<ILiquidExtension> extensions,
        IOptions<WorkflowEngineSettings> settings)
    {
        this.cache = cache;
        this.filters = filters;
        this.extensions = extensions;
        this.settings = settings.Value;
    }

    public string Name => ExpressionEngines.Liquid;

    public async ValueTask<JsonElement> EvaluateAsync(
        string rawValue,
        IDictionary<string, object?> model,
        Type targetType,
        ExpressionEvaluationContext context,
        CancellationToken cancellationToken)
    {
        string rendered;
        try
        {
            rendered = await this.RenderAsync(rawValue, model, context, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ExpressionResolutionException(this.Name, "<liquid>", targetType.Name, ex.Message, ex);
        }

        if (targetType == typeof(string))
        {
            return JsonSerializer.SerializeToElement(rendered, WorkflowJsonOptions.Default);
        }

        var trimmed = rendered.Trim();
        if (trimmed.Length == 0)
        {
            return JsonSerializer.SerializeToElement<object?>(null, WorkflowJsonOptions.Default);
        }
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new ExpressionResolutionException(
                this.Name,
                "<liquid>",
                targetType.Name,
                $"Rendered output is not valid JSON for target type ({trimmed[..Math.Min(80, trimmed.Length)]}…): {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Renders <paramref name="template"/> against <paramref name="model"/>: pulls compiled
    /// template from cache, sets up filters / extensions for this evaluation, async-renders into
    /// a <see cref="LimitingStringWriter"/> so the output is bounded.
    /// </summary>
    private async ValueTask<string> RenderAsync(
        string template,
        object model,
        ExpressionEvaluationContext evaluation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        var compiled = this.cache.GetOrCompile(template);

        // Fresh per-render TemplateOptions so extensions can mutate filter/member/converter
        // registrations without leaking across runs.
        var options = new TemplateOptions
        {
            MaxSteps = MaxStepsLimit,
            MaxRecursion = MaxRecursionLimit,
            TimeZone = TimeZoneInfo.Utc,
        };
        var normalizedModel = NormalizeModel(model);
        var ctx = new TemplateContext(normalizedModel, options);

        // Fluid 2.16 doesn't propagate CancellationToken into rendering — cancellation between
        // awaited filter calls is up to filter authors. We rely on MaxSteps for runaway templates;
        // worker shutdown waits for the in-flight render to finish (bounded by MaxSteps).
        _ = cancellationToken;

        // Filters first — extensions may want to override or chain on top.
        foreach (var filter in this.filters)
        {
            var f = filter;
            options.Filters.AddFilter(f.Name, (input, args, templateCtx) =>
                f.InvokeAsync(input, args, templateCtx, evaluation));
        }

        var liquidContext = new LiquidExpressionContext
        {
            Options = options,
            TemplateContext = ctx,
            Evaluation = evaluation,
        };

        foreach (var extension in this.extensions)
        {
            extension.Configure(liquidContext);
        }

        // Async render path so filters with real I/O (HTTP, S3, DB) don't sync-over-async
        // block the worker thread. Output goes through LimitingStringWriter to bound memory
        // pressure — the writer throws on overflow, which surfaces as a Fluid exception that
        // the caller's catch turns into ExpressionResolutionException.
        await using var writer = new LimitingStringWriter(this.settings.MaxLiquidOutputChars);
        await compiled.RenderAsync(writer, NullEncoder.Default, ctx);
        return writer.ToString();
    }

    /// <summary>
    /// Fluid treats <see cref="IDictionary{TKey,TValue}"/> as a scope it can index into. Convert
    /// any nested JsonElement to plain .NET values so liquid expressions like
    /// <c>{{ vars.answers.address.city }}</c> traverse naturally.
    /// </summary>
    private static object? NormalizeModel(object? raw)
    {
        return raw switch
        {
            null => null,
            JsonElement je => FromJson(je),
            IDictionary<string, object?> dict => dict.ToDictionary(
                kv => kv.Key,
                kv => NormalizeModel(kv.Value)),
            _ => raw,
        };
    }

    private static object? FromJson(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Array => el.EnumerateArray().Select(FromJson).ToList(),
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => FromJson(p.Value)),
            _ => null,
        };
    }
}
