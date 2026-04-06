using LayeredTemplate.Plugins.JsonMultipart.Integrations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.JsonMultipart.Options;

internal class ConfigureMvcOptions : IConfigureOptions<MvcOptions>
{
    private readonly IOptions<JsonOptions> jsonOptions;

    public ConfigureMvcOptions(IOptions<JsonOptions> jsonOptions)
    {
        this.jsonOptions = jsonOptions;
    }

    public void Configure(MvcOptions options)
    {
        options.ModelBinderProviders.Insert(0, new FormDataJsonBinderProvider(this.jsonOptions));
    }
}