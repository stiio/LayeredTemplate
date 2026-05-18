using System.Reflection;
using System.Text.Json;
using LayeredTemplate.Plugins.JsonMultipart.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.JsonMultipart;

/// <summary>
/// Static helpers backing <c>IJsonMultipartRequest&lt;TSelf&gt;</c>. Reads a multipart/form-data
/// request and materialises a strongly-typed DTO where:
/// <list type="bullet">
/// <item>Properties marked <see cref="FromJsonAttribute"/> are JSON-deserialised from the form
///   field of the same name (or, if the field is empty, from an uploaded file of the same name —
///   useful for browser file pickers that ship a JSON document as a file).</item>
/// <item>Properties of type <see cref="IFormFile"/> / <see cref="IFormFileCollection"/> are pulled
///   from <c>form.Files</c>.</item>
/// </list>
/// Validation is NOT performed here — callers add an endpoint filter (e.g. <c>WithValidation&lt;T&gt;()</c>)
/// to enforce required-ness and shape constraints.
/// </summary>
public static class JsonMultipartFormBinder
{
    /// <summary>
    /// Property-info cache. Reflection runs once per type, lookups are then dictionary-fast.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyPlan[]> Plans = new();

    public static async ValueTask<T?> BindAsync<T>(HttpContext context)
        where T : class, new()
    {
        if (!context.Request.HasFormContentType)
        {
            return null;
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);

        var jsonOpts = context.RequestServices.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions;

        var instance = new T();

        foreach (var plan in GetPlans(typeof(T)))
        {
            object? value = plan.Kind switch
            {
                PropertyKind.Json => await ReadJsonAsync(form, plan, jsonOpts, context.RequestAborted),
                PropertyKind.FormFile => form.Files.GetFile(plan.FormFieldName),
                PropertyKind.FormFileCollection => form.Files,
                _ => null,
            };

            if (value is not null)
            {
                plan.Setter(instance, value);
            }
        }

        return instance;
    }

    /// <summary>
    /// Contributes <see cref="IAcceptsMetadata"/> so the OpenAPI machinery treats the endpoint as
    /// <c>multipart/form-data</c>. Fine-grained per-property schema rewrites (e.g. JSON encoding
    /// hint on the JSON-typed fields) are applied by
    /// <see cref="Integrations.MultiPartJsonOperationTransformer"/>.
    /// </summary>
    public static void PopulateMetadata<T>(ParameterInfo parameter, EndpointBuilder builder)
    {
        builder.Metadata.Add(new AcceptsMetadata(["multipart/form-data"], typeof(T)));
    }

    // --- Internals -----------------------------------------------------------

    private static async ValueTask<object?> ReadJsonAsync(
        IFormCollection form,
        PropertyPlan plan,
        JsonSerializerOptions? jsonOpts,
        CancellationToken cancellationToken)
    {
        // Field present as a raw form value (string) — typical case from SPA / curl.
        var rawValue = form[plan.FormFieldName].ToString();

        // Fall back to reading an uploaded file with the same field name as JSON text — supports
        // workflows where the JSON payload comes from a file picker rather than a string input.
        if (string.IsNullOrEmpty(rawValue))
        {
            var file = form.Files.GetFile(plan.FormFieldName);
            if (file is null)
            {
                return null;
            }

            using var reader = new StreamReader(file.OpenReadStream());
            rawValue = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrEmpty(rawValue))
        {
            return null;
        }

        return JsonSerializer.Deserialize(rawValue, plan.PropertyType, jsonOpts);
    }

    private static PropertyPlan[] GetPlans(Type type) =>
        Plans.GetOrAdd(type, t =>
        {
            var list = new List<PropertyPlan>();
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite)
                {
                    continue;
                }

                var setter = BuildSetter(prop);

                if (prop.GetCustomAttribute<FromJsonAttribute>() is not null)
                {
                    list.Add(new PropertyPlan(PropertyKind.Json, prop.Name, prop.PropertyType, setter));
                }
                else if (typeof(IFormFile).IsAssignableFrom(prop.PropertyType))
                {
                    list.Add(new PropertyPlan(PropertyKind.FormFile, prop.Name, prop.PropertyType, setter));
                }
                else if (typeof(IFormFileCollection).IsAssignableFrom(prop.PropertyType))
                {
                    list.Add(new PropertyPlan(PropertyKind.FormFileCollection, prop.Name, prop.PropertyType, setter));
                }

                // Properties without one of these markers are ignored — pass non-file primitives
                // inside the JSON body. Keeps the binder narrow and predictable.
            }

            return list.ToArray();
        });

    private static Action<object, object?> BuildSetter(PropertyInfo prop)
    {
        // Reflection.SetValue is fast enough for request-binding latencies and avoids
        // emitting IL or DynamicMethod (which interact badly with trimming/AOT).
        return (instance, value) => prop.SetValue(instance, value);
    }

    private enum PropertyKind
    {
        Json,
        FormFile,
        FormFileCollection,
    }

    private sealed record PropertyPlan(
        PropertyKind Kind,
        string FormFieldName,
        Type PropertyType,
        Action<object, object?> Setter);
}
