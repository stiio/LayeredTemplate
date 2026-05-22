using Fluid;
using Fluid.Values;
using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Extensions;

/// <summary>
/// A custom Liquid filter as a scoped service. The engine resolves all <see cref="ILiquidFilter"/>
/// implementations per render and registers each as a Fluid filter under <see cref="Name"/>;
/// inside <see cref="InvokeAsync"/> the implementation gets the workflow
/// <see cref="ExpressionEvaluationContext"/> for tenant scoping.
/// </summary>
/// <remarks>
/// Why scoped: filters often need DB / S3 / other scoped services. The filter instance can be
/// constructed against the active scope and reused across all expressions evaluated in that scope.
/// Use this for filters with logic / dependencies; if a filter is purely stateless and doesn't
/// need an interface implementation, an <see cref="ILiquidExtension"/> can register a delegate too.
/// </remarks>
/// <example>
/// <code>
/// public class FilePresignedUrlFilter : ILiquidFilter
/// {
///     private readonly IFileAccessService files;
///     public FilePresignedUrlFilter(IFileAccessService files) =&gt; this.files = files;
///
///     public string Name =&gt; "presigned_url";
///
///     public async ValueTask&lt;FluidValue&gt; InvokeAsync(
///         FluidValue input, FilterArguments args, TemplateContext ctx, ExpressionEvaluationContext eval)
///     {
///         var fileId = Guid.Parse(input.ToStringValue());
///         var url = await this.files.GetPresignedUrlAsync(eval.TenantId, fileId);
///         return new StringValue(url);
///     }
/// }
/// </code>
/// </example>
public interface ILiquidFilter
{
    /// <summary>Name used in templates: <c>{{ value | name: arg1, arg2 }}</c>.</summary>
    string Name { get; }

    /// <summary>
    /// Apply the filter. <paramref name="input"/> is the piped value; <paramref name="arguments"/>
    /// holds positional and named filter args.
    /// </summary>
    /// <remarks>
    /// <b>SECURITY — argument validation.</b> Both <paramref name="input"/> and
    /// <paramref name="arguments"/> originate in the workflow author's Liquid template — fully
    /// untrusted. The body of this method is the trust boundary where author input meets host
    /// infrastructure (DB, HTTP, file storage). Apply the same hygiene rules as for
    /// <see cref="IJsFunction"/> delegate bodies:
    /// <list type="bullet">
    ///   <item>Resource ids (file id, user id, …): re-scope via
    ///     <paramref name="evaluation"/>.<see cref="ExpressionEvaluationContext.TenantId"/>
    ///     before any lookup. Don't trust the author to pass an id belonging to <i>their</i>
    ///     tenant.</item>
    ///   <item>URLs / hostnames: deny by default, allowlist explicit destinations. Naive
    ///     <c>http.GetAsync(input.ToStringValue())</c> is an SSRF vector.</item>
    ///   <item>Free-text strings reaching SQL / shell / template engines: parameterise or
    ///     sanitise. Don't string-format author input into anything an interpreter sees.</item>
    ///   <item>Throw or return <c>NilValue.Instance</c> on invalid input. The thrown exception
    ///     surfaces as <c>ExpressionResolutionException</c>, which the engine treats as
    ///     non-transient and dead-letters the step — exactly the right outcome for a malformed
    ///     filter call.</item>
    /// </list>
    /// </remarks>
    ValueTask<FluidValue> InvokeAsync(
        FluidValue input,
        FilterArguments arguments,
        TemplateContext templateContext,
        ExpressionEvaluationContext evaluation);
}
