using LayeredTemplate.App.Web.ConfigureOptions;
using Scalar.AspNetCore;

namespace LayeredTemplate.App.Web.Extensions;

public static class OpenApiExtensions
{
    public static void ConfigureOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1");
        services.AddOpenApi("v1-dev");
        services.AddOpenApi("merged_api");

        services.ConfigureOptions<ConfigureOpenApiOptions>();
    }

    public static void UseConfiguredOpenApi(this WebApplication app)
    {
        app.MapOpenApi("api/openapi/{documentName}.json");

        app.MapScalarApiReference("api/docs", options =>
        {
            options.Title = "Api Documentation";
            options.OpenApiRoutePattern = "api/openapi/{documentName}.json";
            options.Agent = new ScalarAgentOptions() { Disabled = true };
            options.Mcp = new ScalarMcpOptions() { Disabled = true };
            options.ShowOperationId = true;
            options.HiddenClients = false;
            options.DotNetFlag = true;
            options.Theme = ScalarTheme.Purple;
            options.HideClientButton = true;
        });
    }
}
