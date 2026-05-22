using Fluid;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Extensions;

/// <summary>
/// Per-evaluation hook into the Liquid renderer. Implementations are resolved as scoped
/// services and invoked once per <c>Expr&lt;T&gt;</c> render — they get full access to the
/// underlying Fluid <see cref="TemplateOptions"/> and <see cref="TemplateContext"/>,
/// plus the workflow-level <see cref="ExpressionEvaluationContext"/> for tenant scoping.
/// </summary>
/// <remarks>
/// Typical use cases:
///  <list type="bullet">
///   <item><description>Register custom filters that close over <c>context.Evaluation.TenantId</c>.</description></item>
///   <item><description>Expose globals — e.g. <c>{{ tenantId }}</c>, <c>{{ workflowUrl }}</c>.</description></item>
///   <item><description>Register <c>MemberAccessStrategy</c> entries so Liquid can read POCO properties.</description></item>
///   <item><description>Add value converters for custom types.</description></item>
///  </list>
/// For a simple stateless filter that doesn't need an extension class, use the
/// <c>AddLiquidFilter(name, delegate)</c> shortcut instead.
/// <para>
/// <b>SECURITY — trust boundary.</b> The <see cref="TemplateContext"/> and
/// <see cref="TemplateOptions"/> you receive are the same ones the workflow author's untrusted
/// Liquid template will render against. Liquid is much safer than JS by default — it's a
/// templating language, not a programming language; member access is opt-in via
/// <c>MemberAccessStrategy</c>; there's no <c>eval</c> / function declaration / file include
/// without explicit registration. But a careless extension can poke holes:
/// </para>
/// <list type="bullet">
///   <item>Globals: <c>TemplateContext.SetValue("db", dbContext)</c> alone returns nil for
///     member access (default <c>NullMemberAccessStrategy</c>) — but pair it with
///     <c>options.MemberAccessStrategy.Register&lt;DbContext&gt;()</c> and authors gain
///     <c>{{ db.Users.first.email }}</c>. Two ingredients, both required, both inadvisable.</item>
///   <item>Custom filters that do host I/O: validate every argument from the template.
///     URLs / paths / ids go through allowlist + tenant scoping (see <see cref="ILiquidFilter"/>
///     remarks).</item>
///   <item>Don't register <c>MemberAccessStrategy</c> for stateful types (<c>DbContext</c>,
///     <c>HttpClient</c>, <c>IServiceProvider</c>); use frozen DTOs or expose the data via
///     a tenant-scoped filter instead.</item>
/// </list>
/// <para>
/// Render limits (<see cref="TemplateOptions.MaxSteps"/>, <see cref="TemplateOptions.MaxRecursion"/>)
/// are pre-set by <c>LiquidRenderer</c>. Don't override them upward in extensions — the engine's
/// caps protect the worker thread from runaway templates and shouldn't be relaxed per-extension.
/// </para>
/// </remarks>
public interface ILiquidExtension
{
    void Configure(LiquidExpressionContext context);
}

public class LiquidExpressionContext
{
    /// <summary>Fluid template options — register filters, members, converters here.</summary>
    public required TemplateOptions Options { get; init; }

    /// <summary>Per-render context — set globals via <c>SetValue</c>.</summary>
    public required TemplateContext TemplateContext { get; init; }

    /// <summary>Workflow-level evaluation context (tenant id, run id, etc).</summary>
    public required ExpressionEvaluationContext Evaluation { get; init; }
}
