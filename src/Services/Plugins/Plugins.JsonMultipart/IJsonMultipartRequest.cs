using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace LayeredTemplate.Plugins.JsonMultipart;

/// <summary>
/// Implement this on a request DTO to make Minimal API bind it from a <c>multipart/form-data</c>
/// payload where some fields are JSON-encoded (marked with <see cref="Abstractions.FromJsonAttribute"/>)
/// and others are uploaded files.
/// </summary>
/// <typeparam name="TSelf">Self-type (CRTP) — must be a parameterless-constructible class.</typeparam>
/// <example>
/// <code>
/// public sealed class Request : IJsonMultipartRequest&lt;Request&gt;
/// {
///     [FromJson] public Body Body { get; init; } = null!;
///     public IFormFile File { get; init; } = null!;
/// }
/// </code>
/// </example>
/// <remarks>
/// <para>The interface inherits two Minimal API extension points and supplies default static
/// implementations that delegate to <see cref="JsonMultipartFormBinder"/>:</para>
/// <list type="bullet">
/// <item><c>IBindableFromHttpContext&lt;TSelf&gt;.BindAsync</c> — Minimal API's custom-parameter
///   binding hook, invoked instead of the default expression-tree binder.</item>
/// <item><c>IEndpointParameterMetadataProvider.PopulateMetadata</c> — declares the endpoint
///   accepts <c>multipart/form-data</c> so the generated OpenAPI describes the body correctly.</item>
/// </list>
/// <para>Per-property schema fine-tuning (encoding hint for JSON parts, etc.) is performed by the
/// <c>MultiPartJsonOperationTransformer</c> registered via
/// <see cref="ConfigureServices.AddPluginJsonMultipart"/>.</para>
/// </remarks>
public interface IJsonMultipartRequest<TSelf>
    : IBindableFromHttpContext<TSelf>, IEndpointParameterMetadataProvider
    where TSelf : class, IJsonMultipartRequest<TSelf>, new()
{
    static ValueTask<TSelf?> IBindableFromHttpContext<TSelf>.BindAsync(HttpContext context, ParameterInfo parameter) =>
        JsonMultipartFormBinder.BindAsync<TSelf>(context);

    static void IEndpointParameterMetadataProvider.PopulateMetadata(ParameterInfo parameter, EndpointBuilder builder) =>
        JsonMultipartFormBinder.PopulateMetadata<TSelf>(parameter, builder);
}
