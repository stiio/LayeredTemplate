using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;
using JintEngine = Jint.Engine;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Extensions;

/// <summary>
/// Per-evaluation hook into the JS engine. Implementations are resolved as scoped services and
/// invoked once per <c>Expr&lt;T&gt;</c> evaluation — they get the freshly-built Jint
/// <see cref="JintEngine"/> and the workflow <see cref="ExpressionEvaluationContext"/>.
/// </summary>
/// <remarks>
/// Typical use cases: expose globals (<c>tenantId</c>, <c>currentWorkflowUrl</c>), register
/// host functions (<c>loadFile(id)</c>) that close over tenant scope, etc.
/// <para>
/// <b>SECURITY — trust boundary.</b> The Jint engine you receive is the <i>same</i> engine on
/// which the workflow author's untrusted JS expressions will run. Anything you push via
/// <see cref="JintEngine.SetValue"/> becomes addressable from author code:
/// </para>
/// <list type="bullet">
///   <item>Primitives (strings, numbers, bools) — safe; data flows one-way (author reads).</item>
///   <item>Pre-shaped DTOs / read-only POCOs — safe if they don't expose stateful methods.</item>
///   <item>Stateful host objects (<c>DbContext</c>, <c>HttpClient</c>, file streams,
///   <c>IServiceProvider</c>, environment / process accessors) — <b>NOT safe</b>. Authors can
///   call any method on them, bypass tenant scoping, exfiltrate data, SSRF internal services,
///   read secrets. Classical confused-deputy.</item>
/// </list>
/// <para>
/// The Jint sandbox (statement / memory / time limits) does <b>not</b> protect against method
/// calls on objects you exposed yourself — it caps the JS interpreter, not the .NET methods
/// it gets to invoke. Sandboxing assumes you didn't hand it a key.
/// </para>
/// <para>
/// Safe pattern: expose only primitives or frozen data DTOs from this hook. For any host I/O
/// (DB lookup, HTTP call, presigned-URL generation), implement an <see cref="IJsFunction"/>
/// instead and validate every argument in the delegate body — see its xmldoc.
/// </para>
/// </remarks>
public interface IJsExtension
{
    void Configure(JsExpressionContext context);
}

public class JsExpressionContext
{
    /// <summary>Fresh Jint engine — register globals/functions via <c>SetValue</c>.</summary>
    public required JintEngine JintEngine { get; init; }

    /// <summary>Workflow-level evaluation context (tenant id, run id, etc).</summary>
    public required ExpressionEvaluationContext Evaluation { get; init; }
}
