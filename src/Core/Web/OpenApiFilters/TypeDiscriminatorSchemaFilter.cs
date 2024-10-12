using LayeredTemplate.Shared.Extensions;
using LayeredTemplate.Shared.Options;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Web.OpenApiFilters;

public class TypeDiscriminatorSchemaFilter : ISchemaFilter
{
    private readonly JsonPolymorphismSettings jsonPolymorphismSettings;

    public TypeDiscriminatorSchemaFilter(IOptions<JsonPolymorphismSettings> jsonPolymorphismSettings)
    {
        this.jsonPolymorphismSettings = jsonPolymorphismSettings.Value;
    }

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        const string discriminator = "$type";
        var assemblies = this.jsonPolymorphismSettings.Assemblies;

        var baseType = context.Type.GetRootBaseType();
        if (schema.Discriminator?.PropertyName is not null && schema.AllOf.Count == 0)
        {
            var types = assemblies
                .SelectMany(x => x.GetTypes())
                .Where(x => x.IsAssignableTo(context.Type) && !x.IsAbstract)
                .ToArray();

            schema.Properties[discriminator].Enum = types
                .Select(x => new OpenApiString(x.Name))
                .ToList<IOpenApiAny>();
        }

        if (schema.Discriminator?.PropertyName is not null && schema.AllOf.Count > 0)
        {
            schema.Properties.Remove(discriminator);
        }

        if (schema.Discriminator?.PropertyName is not null)
        {
            var types = assemblies.SelectMany(x => x.GetTypes())
                .Where(x => x.IsAssignableTo(context.Type) && !x.IsAbstract && x != context.Type)
                .ToArray();

            schema.Discriminator.Mapping = types.ToDictionary(
                x => x.Name,
                x => $"#/components/schemas/{x.Name}");
        }

        if (!context.Type.IsAbstract && (schema.Discriminator?.PropertyName is not null || schema.AllOf?.Count > 0))
        {
            var defaultValue = this.jsonPolymorphismSettings.Mapping
                .GetValueOrDefault($"{baseType.FullName}, {context.Type.Assembly.GetName().Name}")
                ?.GetValueOrDefault($"{baseType.FullName}, {context.Type.Assembly.GetName().Name}");

            schema.Default = new OpenApiObject()
            {
                [discriminator] = new OpenApiString(defaultValue ?? context.Type.Name),
            };
        }
    }
}