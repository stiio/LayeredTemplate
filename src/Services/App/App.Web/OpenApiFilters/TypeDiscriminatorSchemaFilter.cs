using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using LayeredTemplate.Shared.Extensions;
using LayeredTemplate.Shared.Options;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.App.Web.OpenApiFilters;

public class TypeDiscriminatorSchemaFilter : ISchemaFilter
{
    private readonly JsonPolymorphismSettings jsonPolymorphismSettings;

    public TypeDiscriminatorSchemaFilter(IOptions<JsonPolymorphismSettings> jsonPolymorphismSettings)
    {
        this.jsonPolymorphismSettings = jsonPolymorphismSettings.Value;
    }

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        const string discriminator = "$type";

        var targetSchema = schema.AllOf?.Count == 2
            ? schema.AllOf[1] as OpenApiSchema
            : schema as OpenApiSchema;

        if (targetSchema!.Discriminator?.PropertyName is not null && schema.AllOf?.Count > 1)
        {
            targetSchema.Required ??= new HashSet<string>();
            targetSchema.Required.Remove("$type");

            targetSchema.Properties ??= new Dictionary<string, IOpenApiSchema>();
            targetSchema.Properties.Remove(discriminator);
            targetSchema.Discriminator = null;
        }

        if (targetSchema.Discriminator?.PropertyName is not null)
        {
            var enumSchema = this.GetOrCreateEnum(context);

            var discriminatorSchema = targetSchema.Properties![discriminator] as OpenApiSchema;
            discriminatorSchema!.DynamicRef = enumSchema.SchemaId;
        }

        if (targetSchema.Discriminator?.PropertyName is not null)
        {
            var assemblies = this.jsonPolymorphismSettings.Assemblies;
            var types = assemblies.SelectMany(x => x.GetTypes())
                .Where(x => x.IsAssignableTo(context.Type) && !x.IsAbstract && x != context.Type)
                .ToArray();

            targetSchema.Discriminator.Mapping = types.ToDictionary(
                x => x.Name,
                x => new OpenApiSchemaReference(x.Name));
        }
    }

    private (string SchemaId, OpenApiSchema Schema) GetOrCreateEnum(SchemaFilterContext context)
    {
        var assemblies = this.jsonPolymorphismSettings.Assemblies;

        var baseType = context.Type.GetRootBaseType();
        var schemaId = $"{baseType.Name}Discriminator";

        if (context.SchemaRepository.Schemas.GetValueOrDefault(schemaId) is OpenApiSchema schema)
        {
            return (schemaId, schema);
        }

        var types = assemblies
            .SelectMany(x => x.GetTypes())
            .Where(x => x.IsAssignableTo(baseType) && !x.IsAbstract)
            .ToArray();

        var reference = context.SchemaRepository.AddDefinition($"{baseType.Name}Discriminator", new OpenApiSchema()
        {
            Type = JsonSchemaType.String,
            Enum = types
                .Select(x => JsonNode.Parse(x.Name))
                .ToList()!,
        });

        schema = (context.SchemaRepository.Schemas.GetValueOrDefault(schemaId) as OpenApiSchema)!;
        return (schemaId, schema);
    }
}