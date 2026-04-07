using System.ComponentModel.DataAnnotations;
using System.Reflection;
using LayeredTemplate.Plugins.JsonMultipart.Abstractions;
using LayeredTemplate.Plugins.JsonMultipart.Extensions;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LayeredTemplate.Plugins.JsonMultipart.Integrations;

internal class MultiPartJsonOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var descriptors = context.Description.ActionDescriptor.Parameters.ToList();
        foreach (var descriptor in descriptors)
        {
            if (!this.HasJsonProperties(descriptor))
            {
                continue;
            }

            var contentEntry = operation.RequestBody!.Content!.First();
            var mediaType = contentEntry.Value;

            if (contentEntry.Key != "multipart/form-data")
            {
                operation.RequestBody.Content!.Remove(contentEntry.Key);
                operation.RequestBody.Content["multipart/form-data"] = mediaType;
            }

            var mediaTypeSchema = mediaType.Schema as OpenApiSchema;
            mediaTypeSchema!.Required ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            mediaTypeSchema.Required.Clear();
            mediaTypeSchema.Properties ??= new Dictionary<string, IOpenApiSchema>();

            var groupedProperties = mediaTypeSchema.Properties
                .GroupBy(pair => pair.Key.Split('.')[0]);

            var schemaProperties = new Dictionary<string, IOpenApiSchema>();

            foreach (var property in groupedProperties)
            {
                var isRequired = descriptor.ParameterType.GetProperties().Any(propertyInfo =>
                    propertyInfo.Name.Equals(property.Key, StringComparison.OrdinalIgnoreCase)
                    && propertyInfo.GetCustomAttribute<RequiredAttribute>() != null);

                if (isRequired)
                {
                    mediaTypeSchema.Required.Add(property.Key.ToCamelCase());
                }

                var jsonPropertyInfo = this.GetJsonPropertyInfo(descriptor, property.Key);
                if (property.Key.Equals(jsonPropertyInfo?.Name, StringComparison.OrdinalIgnoreCase))
                {
                    this.AddEncoding(mediaType, jsonPropertyInfo);

                    var openApiSchema = this.GetSchema(jsonPropertyInfo);
                    schemaProperties.Add(property.Key.ToCamelCase(), openApiSchema);
                }
                else
                {
                    schemaProperties.Add(property.Key.ToCamelCase(), property.First().Value);
                }
            }

            mediaTypeSchema.Properties = schemaProperties;
        }

        return Task.CompletedTask;
    }

    private OpenApiSchemaReference GetSchema(PropertyInfo propertyInfo)
    {
        var typeName = propertyInfo.PropertyType.Name;
        return new OpenApiSchemaReference(typeName);
    }

    private void AddEncoding(OpenApiMediaType mediaType, PropertyInfo propertyInfo)
    {
        mediaType.Encoding = mediaType.Encoding?
            .Where(pair => !pair.Key.ToLower().Contains(propertyInfo.Name.ToLower()))
            .ToDictionary(pair => pair.Key.ToCamelCase(), pair => pair.Value) ?? new Dictionary<string, OpenApiEncoding>();

        mediaType.Encoding.Add(propertyInfo.Name.ToCamelCase(), new OpenApiEncoding()
        {
            ContentType = "application/json",
        });
    }

    private bool HasJsonProperties(ParameterDescriptor descriptor)
    {
        return descriptor.ParameterType.GetProperties()
            .Any(property => property.GetCustomAttribute<FromJsonAttribute>() != null);
    }

    private PropertyInfo? GetJsonPropertyInfo(ParameterDescriptor descriptor, string propertyName)
    {
        return descriptor.ParameterType.GetProperties()
            .SingleOrDefault(property =>
                property.GetCustomAttribute<FromJsonAttribute>() != null
                && property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
    }
}
