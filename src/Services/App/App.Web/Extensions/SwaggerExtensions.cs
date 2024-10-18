using Asp.Versioning.ApiExplorer;
using LayeredTemplate.App.Web.ConfigureOptions;

namespace LayeredTemplate.App.Web.Extensions;

public static class SwaggerExtensions
{
    public static void ConfigureSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen();
        services.ConfigureOptions<ConfigureSwaggerGenOptions>();
    }

    public static void UseConfiguredSwagger(
        this IApplicationBuilder app,
        IWebHostEnvironment env,
        IApiVersionDescriptionProvider apiVersionDescriptionProvider)
    {
        app.UseSwagger(options => options.RouteTemplate = "/api-docs/{documentName}/swagger.json");
        app.UseSwaggerUI(c =>
        {
            foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
            {
                if (description.ApiVersion.Status == "dev" && !env.IsDevelopment())
                {
                    continue;
                }

                c.SwaggerEndpoint($"/api-docs/{description.GroupName}/swagger.json", $"Api - v{description.ApiVersion}");
            }

            c.RoutePrefix = "api-docs";

            c.EnableFilter();
            c.EnableDeepLinking();
            c.DisplayOperationId();
            c.DisplayRequestDuration();
        });
    }
}