using System.Text.Json;
using System.Text.RegularExpressions;
using Jint;
using Jint.Native;
using Jint.Runtime;
using LayeredTemplate.Plugins.Workflow.Abstractions;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using LayeredTemplate.Plugins.Workflow.Engine.Expressions.Extensions;
using JintEngine = Jint.Engine;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Engines;

/// <summary>
/// Evaluates a JS expression via Jint with a locked-down sandbox: no CLR host, no fs/network,
/// hard timeout, memory/recursion caps. Authors can write either:
///   — a single expression (<c>answers.total * 1.2</c>) → wrapped as <c>return (EXPR)</c>
///   — a multi-statement body with an explicit <c>return</c> (if/else, let, loops) → used as-is
///   — anything using <c>await</c> for async host functions (e.g. <c>await getPresignedUrl(id)</c>)
/// Heuristic for picking a wrapper: if the source contains a <c>return</c> keyword we treat it
/// as a function body, otherwise as an expression. The IIFE is always <c>async</c>, so any
/// host delegate that returns <c>Task&lt;T&gt;</c> can be <c>await</c>'d directly. Output is
/// normalized through <c>JSON.stringify</c> → parsed as a <see cref="JsonElement"/>.
/// </summary>
internal class JsExpressionEngine : IExpressionEngine
{
    private const int TimeoutMs = 500;
    private const long MemoryLimitBytes = 4 * 1024 * 1024; // 4 MiB

    // Hard cap on JS statements per evaluation. A tight loop without recursion (`while(true){}`)
    // otherwise burns the full TimeoutMs on the worker thread; with BatchSize=10 that's ~5s of
    // thread-starvation per malicious workflow. 50k is enough for legitimate complex
    // expressions (loops over <1000 items with non-trivial body) and immediately trips bombs.
    private const int StatementLimit = 50_000;

    // Word-boundary `return` — detected loosely on purpose (false-positives inside string
    // literals are harmless; they'd just pick the body-style wrapper, which still parses).
    private static readonly Regex ReturnRegex = new(@"\breturn\b", RegexOptions.Compiled);

    private readonly IEnumerable<IJsFunction> functions;
    private readonly IEnumerable<IJsExtension> extensions;

    public JsExpressionEngine(
        IEnumerable<IJsFunction> functions,
        IEnumerable<IJsExtension> extensions)
    {
        this.functions = functions;
        this.extensions = extensions;
    }

    public string Name => ExpressionEngines.Js;

    public async ValueTask<JsonElement> EvaluateAsync(
        string rawValue,
        IDictionary<string, object?> model,
        Type targetType,
        ExpressionEvaluationContext context,
        CancellationToken cancellationToken)
    {
        // Empty / whitespace input → null. Mirrors LiquidExpressionEngine and StaticExpressionEngine
        // behavior so all three engines coerce blank values to a default (false / 0 / null / "")
        // rather than throwing. Authors who pre-fill an Expr with no value (e.g. branch conditions
        // in Switch's "all" mode where conditions are ignored anyway) shouldn't see resolver errors.
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return JsonSerializer.SerializeToElement<object?>(null, WorkflowJsonOptions.Default);
        }

        JsValue result;
        JsValue stringified;
        try
        {
            // CT goes both into engine constraints (Jint observes between statements) and into
            // EvaluateAsync's outer Task — together they cover both compute-bound and IO-bound
            // cancellation paths.
            var engine = new JintEngine(opts => opts
                .LimitRecursion(64)
                .MaxStatements(StatementLimit)
                .TimeoutInterval(TimeSpan.FromMilliseconds(TimeoutMs))
                .LimitMemory(MemoryLimitBytes)
                .CancellationToken(cancellationToken)
                .Strict());

            foreach (var kv in model)
            {
                engine.SetValue(kv.Key, ToJsValue(engine, kv.Value));
            }

            // Functions registered first — they're the explicit "named function" path.
            // Extensions run afterwards and can override / add arbitrary globals.
            foreach (var func in this.functions)
            {
                engine.SetValue(func.Name, func.Create(context));
            }

            var jsContext = new JsExpressionContext
            {
                JintEngine = engine,
                Evaluation = context,
            };
            foreach (var extension in this.extensions)
            {
                extension.Configure(jsContext);
            }

            // Wrap as an async IIFE so `await` is legal inside the user's body — top-level await
            // isn't supported in non-module evaluation. The IIFE returns a Promise; engine.EvaluateAsync
            // awaits it and resolves the inner value, releasing the calling thread during any
            // host I/O (Task<T> returned by an IJsFunction delegate). Per Jint 4.6 docs:
            // "zero threads consumed during IO-bound operations".
            var wrapped = ReturnRegex.IsMatch(rawValue)
                ? $"(async function(){{ {rawValue} }})()"
                : $"(async function(){{ return ({rawValue}); }})()";

            result = await engine.EvaluateAsync(wrapped, source: "<js>", cancellationToken);

            // JSON.stringify is pure-CPU on already-resolved values, no async needed.
            stringified = engine.Evaluate("JSON.stringify").Call(JsValue.Undefined, new[] { result });
        }
        catch (JavaScriptException ex)
        {
            throw new ExpressionResolutionException(this.Name, "<js>", targetType.Name, ex.Message, ex);
        }
        catch (PromiseRejectedException ex)
        {
            throw new ExpressionResolutionException(this.Name, "<js>", targetType.Name, $"Promise rejected: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is TimeoutException or MemoryLimitExceededException or StatementsCountOverflowException)
        {
            throw new ExpressionResolutionException(this.Name, "<js>", targetType.Name, $"Execution limit hit: {ex.GetType().Name}", ex);
        }

        if (stringified.IsUndefined() || stringified.IsNull())
        {
            return JsonSerializer.SerializeToElement<object?>(null, WorkflowJsonOptions.Default);
        }

        var json = stringified.AsString();
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new ExpressionResolutionException(
                this.Name,
                "<js>",
                targetType.Name,
                $"JSON.stringify output not parseable: {ex.Message}",
                ex);
        }
    }

    private static JsValue ToJsValue(JintEngine engine, object? raw)
    {
        return raw switch
        {
            null => JsValue.Null,
            JsonElement je => JsonElementToJsValue(engine, je),
            _ => JsValue.FromObject(engine, raw),
        };
    }

    private static JsValue JsonElementToJsValue(JintEngine engine, JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? string.Empty,
            JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => JsValue.Null,
            JsonValueKind.Array => JsValue.FromObject(engine, el.EnumerateArray().Select(JsonToObject).ToArray()),
            JsonValueKind.Object => JsValue.FromObject(engine, el.EnumerateObject().ToDictionary(p => p.Name, p => JsonToObject(p.Value))),
            _ => JsValue.Null,
        };
    }

    private static object? JsonToObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? (object?)i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => el.EnumerateArray().Select(JsonToObject).ToList(),
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => JsonToObject(p.Value)),
            _ => null,
        };
    }
}
