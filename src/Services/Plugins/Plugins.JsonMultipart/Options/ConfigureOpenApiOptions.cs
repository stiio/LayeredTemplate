using LayeredTemplate.Plugins.JsonMultipart.Integrations;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.JsonMultipart.Options;

internal class ConfigureOpenApiOptions : IConfigureNamedOptions<OpenApiOptions>
{
    public void Configure(string? name, OpenApiOptions options)
    {
        options.AddOperationTransformer<MultiPartJsonOperationTransformer>();
        options.AddSchemaTransformer<FormFileSchemaTransformer>();
    }

    public void Configure(OpenApiOptions options)
    {
        this.Configure(Microsoft.Extensions.Options.Options.DefaultName, options);
    }
}
