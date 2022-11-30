using System.Reflection;
using LayeredTemplate.Web.Api.Controllers;
using LayeredTemplate.Web.Api.OpenApiFilters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Web.Api.Extensions;

/// <summary>
/// SwaggerExtensions
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>
    /// Configure Swagger
    /// </summary>
    /// <param name="services"></param>
    public static void ConfigureSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            var version = typeof(AppControllerBase).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Api - V1", Version = version });
            options.SwaggerDoc("v2", new OpenApiInfo { Title = "Api - V2", Version = version });
            options.SwaggerDoc("merged_api_versions", new OpenApiInfo() { Title = "Merged Api Versions", Version = version });

            options.SwaggerDoc("development", new OpenApiInfo() { Title = "API Development", Version = "v1" });

            options.DocInclusionPredicate((documentName, apiDescription) =>
            {
                return documentName switch
                {
                    "merged_api_versions" => apiDescription.GroupName is "v1" or "v2",
                    _ => documentName == apiDescription.GroupName,
                };
            });

            options.UserCustomDateConverters();

            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Web.Api.xml"));
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Application.Contracts.xml"));

            options.OperationFilter<DefaultApplicationResponsesFilter>();

            options.CustomOperationIds(apiDesc =>
                apiDesc.TryGetMethodInfo(out var methodInfo)
                    ? $"{methodInfo.Name}{apiDesc.GroupName!.ToUpper()}"
                    : null);

            options.ConfigureSecurity();
        });
    }

    /// <summary>
    /// UseConfiguredSwagger
    /// </summary>
    /// <param name="app"></param>
    public static void UseConfiguredSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger(options => options.RouteTemplate = "/api-docs/{documentName}/swagger.json");
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/api-docs/v1/swagger.json", "Api v1");
            c.SwaggerEndpoint("/api-docs/v2/swagger.json", "Api v2");
            c.SwaggerEndpoint("/api-docs/merged_api_versions/swagger.json", "Merged Api");

            c.SwaggerEndpoint("/api-docs/development/swagger.json", "Development Api");

            c.RoutePrefix = "api-docs";

            c.EnableFilter();
            c.EnableDeepLinking();
            c.DisplayOperationId();
        });
    }

    private static void ConfigureSecurity(this SwaggerGenOptions options)
    {
        options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme()
        {
            Name = "Bearer",
            BearerFormat = "JWT",
            Scheme = "bearer",
            Description = "Specify the authorization token.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
        });

        options.OperationFilter<AuthOperationFilter>();
    }

    private static void UserCustomDateConverters(this SwaggerGenOptions options)
    {
        options.MapType<DateOnly>(() => new OpenApiSchema
        {
            Type = "string",
            Format = "date-only",
            Example = OpenApiAnyFactory.CreateFromJson($"\"{DateOnly.FromDateTime(DateTime.UtcNow):O}\""),
        });

        options.MapType<DateTime>(() => new OpenApiSchema
        {
            Type = "string",
            Format = "date-with-time",
            Example = OpenApiAnyFactory.CreateFromJson($"\"{DateTime.UtcNow:O}\""),
        });
    }
}