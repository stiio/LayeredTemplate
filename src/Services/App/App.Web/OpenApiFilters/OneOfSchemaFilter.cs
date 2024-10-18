using LayeredTemplate.Shared.Options;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.App.Web.OpenApiFilters;

public class OneOfSchemaFilter : ISchemaFilter, IOperationFilter
{
    private readonly JsonPolymorphismSettings jsonPolymorphismSettings;

    public OneOfSchemaFilter(IOptions<JsonPolymorphismSettings> jsonPolymorphismSettings)
    {
        this.jsonPolymorphismSettings = jsonPolymorphismSettings.Value;
    }

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var assemblies = this.jsonPolymorphismSettings.Assemblies;
        var types = assemblies.SelectMany(x => x.GetTypes())
            .Where(x => x.IsAssignableTo(context.Type) && !x.IsAbstract)
            .ToArray();

        if (types.Length > 1)
        {
            var schemaId = $"{context.Type.Name}OneOfType";
            var oneOfSchema = new OpenApiSchema()
            {
                OneOf = types.Select(x => new OpenApiSchema()
                {
                    Reference = new OpenApiReference()
                    {
                        Id = $"{x.Name}",
                        Type = ReferenceType.Schema,
                    },
                }).ToList(),
            };

            if (!context.SchemaRepository.Schemas.ContainsKey(schemaId))
            {
                context.SchemaRepository.AddDefinition(schemaId, oneOfSchema);
            }
        }

        foreach (var schemaProperty in schema.Properties)
        {
            if (schemaProperty.Value.OneOf.Count > 0)
            {
                var schemaId = this.FindSchemaId(schemaProperty.Value, context.SchemaRepository);
                if (schemaId is not null)
                {
                    schema.Properties[schemaProperty.Key] = new OpenApiSchema()
                    {
                        Reference = new OpenApiReference()
                        {
                            Id = schemaId,
                            Type = ReferenceType.Schema,
                        },
                    };
                }
            }
        }
    }

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var openApiMediaType in operation.RequestBody.Content
                     .Where(x => x.Value.Schema.OneOf.Count > 0))
        {
            var schemaId = this.FindSchemaId(openApiMediaType.Value.Schema, context.SchemaRepository);
            if (schemaId is not null)
            {
                operation.RequestBody.Content[openApiMediaType.Key].Schema = new OpenApiSchema()
                {
                    Reference = new OpenApiReference()
                    {
                        Id = schemaId,
                        Type = ReferenceType.Schema,
                    },
                };
            }
        }

        foreach (var openApiMediaType in operation.Responses
                     .Where(x => x.Key == "200")
                     .SelectMany(x => x.Value.Content))
        {
            var schemaId = this.FindSchemaId(openApiMediaType.Value.Schema, context.SchemaRepository);
            if (schemaId is not null)
            {
                operation.Responses["200"].Content[openApiMediaType.Key].Schema = new OpenApiSchema()
                {
                    Reference = new OpenApiReference()
                    {
                        Id = schemaId,
                        Type = ReferenceType.Schema,
                    },
                };
            }
        }
    }

    private string? FindSchemaId(OpenApiSchema schema, SchemaRepository schemaRepository)
    {
        var oneOfIds = schema.OneOf.Select(x => x.Reference.Id).ToArray();

        var existsSchema = schemaRepository.Schemas
            .Where(x => x.Value.OneOf.Count > 0)
            .FirstOrDefault(x =>
                x.Value.OneOf.All(o => oneOfIds.Contains(o.Reference?.Id))
                                       && oneOfIds.Length == x.Value.OneOf.Count);

        return string.IsNullOrEmpty(existsSchema.Key) ? null : existsSchema.Key;
    }
}