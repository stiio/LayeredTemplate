namespace LayeredTemplate.Plugins.JsonMultipart;

/// <summary>
/// Marks a property on a request DTO as carrying its value as a JSON-encoded multipart field.
/// The form field with the same name as the property is read as raw text (or the body of an
/// upload of the same name), then deserialized with <c>System.Text.Json</c> using the application's
/// registered <c>Microsoft.AspNetCore.Http.Json.JsonOptions</c>.
/// </summary>
/// <remarks>
/// <para>This is a plain marker attribute — it does NOT inherit from <c>FromFormAttribute</c>.
/// Inheriting from it (the previous MVC-era design) would make Minimal API's request-delegate
/// factory try to bind the property as a primitive form value, which fails at expression-tree
/// compile time for complex types. The actual binding is performed by
/// <c>JsonMultipartFormBinder.BindAsync</c> via <c>IBindableFromHttpContext</c>.</para>
/// <para>Usage: declare your request DTO as <c>IJsonMultipartRequest&lt;TSelf&gt;</c>, mark JSON
/// fields with <see cref="FromJsonAttribute"/>, declare file fields as <c>IFormFile</c>. The
/// interface contributes the static <c>BindAsync</c> and OpenAPI metadata implementations.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class FromJsonAttribute : Attribute;
