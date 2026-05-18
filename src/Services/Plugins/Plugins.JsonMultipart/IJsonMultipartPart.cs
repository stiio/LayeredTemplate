using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace LayeredTemplate.Plugins.JsonMultipart;

/// <summary>
/// Implement this on a <b>complex type</b> that should be bound from a single JSON-encoded
/// <c>multipart/form-data</c> part. The form field with the same name as the consuming property
/// (or, if empty, an uploaded file of that name) is read as text and JSON-deserialised into the
/// implementing type via <c>System.Text.Json</c>.
/// </summary>
/// <typeparam name="TSelf">Self-type (CRTP) — must be a parameterless-constructible class.</typeparam>
/// <example>
/// <code>
/// // JSON-bound part — annotate the type, not the property.
/// public sealed class Body : IJsonMultipartPart&lt;Body&gt;
/// {
///     [Required] public string Name { get; set; } = null!;
///     public string? Description { get; set; }
/// }
///
/// // Aggregate Request bound via [AsParameters]; Body uses Body.BindAsync, File and IsDraft
/// // are handled natively by Minimal API.
/// public sealed class Request
/// {
///     [Required] public Body Body { get; set; } = null!;
///     [Required] public IFormFile File { get; set; } = null!;
///     public bool IsDraft { get; set; }
/// }
///
/// public static TodoListDto Handle([AsParameters] Request request) => ...;
/// </code>
/// </example>
/// <remarks>
/// <para>The interface inherits <c>IBindableFromHttpContext&lt;TSelf&gt;</c> and supplies a default
/// static implementation that delegates to <see cref="JsonMultipartFormBinder.BindJsonPartAsync{T}"/>.
/// Minimal API discovers <c>BindAsync</c> on each property's type when binding an
/// <c>[AsParameters]</c> aggregate — no extra opt-in on the consuming type needed.</para>
/// <para>Compared to the old "whole request" binder, this scope-narrowed design means:
/// <list type="bullet">
/// <item><c>IFormFile</c> / <c>IFormFileCollection</c> / primitives / enums / Guid / DateTime etc.
///   are bound by Minimal API natively — we don't reimplement TypeDescriptor / form-file lookup.</item>
/// <item>Our plugin owns one concern only: "deserialize a multipart text part as JSON".</item>
/// </list></para>
/// <para>OpenAPI: properties whose type implements this interface get
/// <c>encoding.&lt;field&gt;.contentType: application/json</c> attached by
/// <see cref="Integrations.MultiPartJsonOperationTransformer"/>.</para>
/// </remarks>
public interface IJsonMultipartPart<TSelf> : IBindableFromHttpContext<TSelf>
    where TSelf : class, IJsonMultipartPart<TSelf>
{
    static ValueTask<TSelf?> IBindableFromHttpContext<TSelf>.BindAsync(HttpContext context, ParameterInfo parameter) =>
        JsonMultipartFormBinder.BindJsonPartAsync<TSelf>(context, parameter.Name ?? typeof(TSelf).Name);
}
