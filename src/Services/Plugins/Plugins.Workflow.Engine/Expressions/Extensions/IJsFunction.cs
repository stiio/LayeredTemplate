using LayeredTemplate.Plugins.Workflow.Abstractions.Expressions;

namespace LayeredTemplate.Plugins.Workflow.Engine.Expressions.Extensions;

/// <summary>
/// A custom JS function as a scoped service. The engine resolves all <see cref="IJsFunction"/>
/// implementations per evaluation and registers each on the Jint engine under <see cref="Name"/>
/// using the delegate produced by <see cref="Create"/>. Closing over <c>evaluation</c> lets the
/// function scope itself to the current tenant / actor.
/// </summary>
/// <remarks>
/// Why scoped: functions often need DB / S3 / other scoped services via constructor injection.
/// The returned delegate is fresh per evaluation, so it can close over tenant-aware state
/// even though the implementation itself is reused across evaluations within a scope.
/// <para>
/// <b>Async support — first-class.</b> The engine wraps every user expression in an <c>async</c>
/// IIFE and dispatches via <c>Jint.Engine.EvaluateAsync</c>, which awaits the resulting Promise
/// without blocking the worker thread. A delegate that returns <c>Task&lt;T&gt;</c> /
/// <c>ValueTask&lt;T&gt;</c> shows up as a Promise to the JS author and can be <c>await</c>'d
/// directly. While the host Task is in flight (HTTP, S3, DB), Jint releases the calling thread
/// — zero threads consumed during IO. Authors can write
/// <c>let url = await getPresignedUrl(fileId); return { url };</c> safely; the worker batch
/// continues to process other steps in parallel.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class GetPresignedUrlJsFunction : IJsFunction
/// {
///     private readonly IFileAccessService files;
///     public GetPresignedUrlJsFunction(IFileAccessService files) =&gt; this.files = files;
///
///     public string Name =&gt; "getPresignedUrl";
///
///     // Returning Task&lt;string&gt; — JS author writes `await getPresignedUrl(fileId)`.
///     public Delegate Create(ExpressionEvaluationContext evaluation) =&gt;
///         (Func&lt;string, Task&lt;string&gt;&gt;)(fileId =&gt;
///             this.files.GetPresignedUrlAsync(evaluation.TenantId, Guid.Parse(fileId)));
/// }
/// </code>
/// </example>
public interface IJsFunction
{
    /// <summary>Identifier in JS code, e.g. <c>getPresignedUrl(fileId)</c>.</summary>
    string Name { get; }

    /// <summary>
    /// Returns the delegate to register on the Jint engine. Any <see cref="Func{T, TResult}"/> /
    /// <see cref="Action"/> shape Jint accepts works.
    /// </summary>
    /// <remarks>
    /// <b>SECURITY — argument validation.</b> Every parameter your delegate accepts comes from
    /// untrusted JS authored by whoever has access to the workflow editor. The body of your
    /// delegate is the trust boundary — it's where author input meets host infrastructure
    /// (DB, HTTP, file storage, etc.).
    /// <list type="bullet">
    ///   <item>Resource ids (file id, user id, …): always re-scope via
    ///     <c>evaluation.TenantId</c> before the lookup. Don't trust the author to pass a
    ///     value that belongs to <i>their</i> tenant.</item>
    ///   <item>URLs / hostnames: deny by default, allowlist explicit destinations. Naive
    ///     <c>http.GetAsync(authorUrl)</c> is an SSRF vector — author can target your internal
    ///     admin services, AWS metadata endpoint, etc.</item>
    ///   <item>Free-text strings reaching SQL / shell / template engines: parameterise or
    ///     sanitise. Don't string-format author input into anything an interpreter sees.</item>
    ///   <item>Throw a clear exception on invalid input — it propagates as
    ///     <c>JavaScriptException</c> to the author and surfaces as
    ///     <c>ExpressionResolutionException</c> in the engine, which is what you want.</item>
    /// </list>
    /// </remarks>
    Delegate Create(ExpressionEvaluationContext evaluation);
}
