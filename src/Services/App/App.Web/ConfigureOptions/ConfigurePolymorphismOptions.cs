using LayeredTemplate.App.Web.Json.TypeResolvers;
using LayeredTemplate.App.Web.OpenApiFilters;
using LayeredTemplate.Shared.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.App.Web.ConfigureOptions;

public class ConfigurePolymorphismOptions :
    IConfigureOptions<JsonOptions>,
    IConfigureOptions<SwaggerGenOptions>
{
    private readonly IOptions<JsonPolymorphismSettings> jsonPolymorphismSettings;

    public ConfigurePolymorphismOptions(IOptions<JsonPolymorphismSettings> jsonPolymorphismSettings)
    {
        this.jsonPolymorphismSettings = jsonPolymorphismSettings;
    }

    public void Configure(JsonOptions options)
    {
        options.JsonSerializerOptions.TypeInfoResolver = new PolymorphicTypeResolver(this.jsonPolymorphismSettings);
    }

    public void Configure(SwaggerGenOptions options)
    {
        options.UseAllOfForInheritance();
        options.SelectDiscriminatorNameUsing(_ => "$type");
        options.SchemaFilter<TypeDiscriminatorSchemaFilter>();

        // options.UseOneOfForPolymorphism();
        // options.SchemaFilter<OneOfSchemaFilter>();
        // options.OperationFilter<OneOfSchemaFilter>();
    }
}