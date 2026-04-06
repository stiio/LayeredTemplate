using System.ComponentModel.DataAnnotations;
using System.Reflection;
using LayeredTemplate.Plugins.JsonMultipart.Abstractions;
using LayeredTemplate.Plugins.JsonMultipart.Extensions;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Plugins.JsonMultipart.Integrations;

internal class MultiPartJsonOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var descriptors = context.ApiDescription.ActionDescriptor.Parameters.ToList();
        foreach (var descriptor in descriptors)
        {
            if (!this.HasJsonProperties(descriptor))
            {
                continue;
            }

            var mediaType = operation.RequestBody!.Content!.First().Value;

            mediaType.Schema!.Required!.Clear();

            // Group all exploded properties.
            var groupedProperties = mediaType.Schema.Properties!
                .GroupBy(pair => pair.Key.Split('.')[0]);

            var schemaProperties = new Dictionary<string, IOpenApiSchema>();

            foreach (var property in groupedProperties)
            {
                var isRequired = descriptor.ParameterType.GetProperties().Any(propertyInfo =>
                    propertyInfo.Name.Equals(property.Key, StringComparison.OrdinalIgnoreCase)
                    && propertyInfo.GetCustomAttribute<RequiredAttribute>() != null);

                if (isRequired)
                {
                    mediaType.Schema.Required.Add(property.Key.ToCamelCase());
                }

                var jsonPropertyInfo = this.GetJsonPropertyInfo(descriptor, property.Key);
                if (property.Key.Equals(jsonPropertyInfo?.Name, StringComparison.OrdinalIgnoreCase))
                {
                    this.AddEncoding(mediaType, jsonPropertyInfo);

                    var openApiSchema = this.GetSchema(context, jsonPropertyInfo);
                    schemaProperties.Add(property.Key.ToCamelCase(), openApiSchema);
                }
                else
                {
                    schemaProperties.Add(property.Key.ToCamelCase(), property.First().Value);
                }
            }

            var mediaTypeSchema = mediaType.Schema as OpenApiSchema;

            // Override schema properties
            mediaTypeSchema?.Properties = schemaProperties;
        }
    }

    /// <summary>
    /// Generate schema for propertyInfo
    /// </summary>
    /// <returns></returns>
    private OpenApiSchemaReference GetSchema(OperationFilterContext context, PropertyInfo propertyInfo)
    {
        var present = context.SchemaRepository.TryLookupByType(propertyInfo.PropertyType, out var openApiSchemaReference);
        if (present)
        {
            return openApiSchemaReference;
        }

        var openApiSchema = (context.SchemaGenerator.GenerateSchema(propertyInfo.PropertyType, context.SchemaRepository) as OpenApiSchema)!;
        this.AddDescription(openApiSchema, openApiSchema.Title!);

        return new OpenApiSchemaReference(openApiSchema.Id!);
    }

    private void AddDescription(OpenApiSchema openApiSchema, string schemaDisplayName)
    {
        openApiSchema.Description += $"\n See {schemaDisplayName} model.";
    }

    private void AddEncoding(OpenApiMediaType mediaType, PropertyInfo propertyInfo)
    {
        mediaType.Encoding = mediaType.Encoding!
            .Where(pair => !pair.Key.ToLower().Contains(propertyInfo.Name.ToLower()))
            .ToDictionary(pair => pair.Key.ToCamelCase(), pair => pair.Value);

        mediaType.Encoding.Add(propertyInfo.Name.ToCamelCase(), new OpenApiEncoding()
        {
            // Style = ParameterStyle.DeepObject,
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