using LayeredTemplate.Shared.Extensions;
using LayeredTemplate.Shared.Options;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.App.Web.OpenApiFilters;

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

        var targetSchema = schema.AllOf.Count == 2
            ? schema.AllOf[1]
            : schema;

        if (targetSchema.Discriminator?.PropertyName is not null && schema.AllOf.Count > 1)
        {
            targetSchema.Required.Remove("$type");
            targetSchema.Properties.Remove(discriminator);
            targetSchema.Discriminator = null;
        }

        if (targetSchema.Discriminator?.PropertyName is not null)
        {
            var enumSchema = this.GetOrCreateEnum(context);

            var discriminatorSchema = targetSchema.Properties[discriminator];
            discriminatorSchema.Reference = new OpenApiReference()
            {
                Id = enumSchema.SchemaId,
                Type = ReferenceType.Schema,
            };
        }

        if (targetSchema.Discriminator?.PropertyName is not null)
        {
            var assemblies = this.jsonPolymorphismSettings.Assemblies;
            var types = assemblies.SelectMany(x => x.GetTypes())
                .Where(x => x.IsAssignableTo(context.Type) && !x.IsAbstract && x != context.Type)
                .ToArray();

            targetSchema.Discriminator.Mapping = types.ToDictionary(
                x => x.Name,
                x => $"#/components/schemas/{x.Name}");
        }
    }

    private (string SchemaId, OpenApiSchema Schema) GetOrCreateEnum(SchemaFilterContext context)
    {
        var assemblies = this.jsonPolymorphismSettings.Assemblies;

        var baseType = context.Type.GetRootBaseType();
        var schemaId = $"{baseType.Name}Discriminator";

        var schema = context.SchemaRepository.Schemas.GetValueOrDefault(schemaId);

        if (schema is not null)
        {
            return (schemaId, schema);
        }

        var types = assemblies
            .SelectMany(x => x.GetTypes())
            .Where(x => x.IsAssignableTo(baseType) && !x.IsAbstract)
            .ToArray();

        schema = context.SchemaRepository.AddDefinition($"{baseType.Name}Discriminator", new OpenApiSchema()
        {
            Type = "string",
            Enum = types
                .Select(x => new OpenApiString(x.Name))
                .ToList<IOpenApiAny>(),
        });

        return (schemaId, schema);
    }
}