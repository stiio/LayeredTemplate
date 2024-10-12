using LayeredTemplate.Shared.Options;
using LayeredTemplate.Web.Json.TypeResolvers;
using LayeredTemplate.Web.OpenApiFilters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Web.ConfigureOptions;

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
        options.UseOneOfForPolymorphism();
        options.SelectDiscriminatorNameUsing(_ => "$type");
        options.SchemaFilter<TypeDiscriminatorSchemaFilter>();
    }
}