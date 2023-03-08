using System.Reflection;
using LayeredTemplate.Web.Api.Controllers;
using LayeredTemplate.Web.OpenApiFilters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Web.Extensions;

public static class SwaggerExtensions
{
    private static readonly string[] Versions = { "v1", "v2", "development", "merged_api" };

    public static void ConfigureSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            var packageVersion = typeof(AppControllerBase).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            foreach (var version in Versions)
            {
                options.SwaggerDoc(version, new OpenApiInfo() { Title = $"Api - {version}", Version = packageVersion });
            }

            options.DocInclusionPredicate((documentName, apiDescription) =>
            {
                return documentName switch
                {
                    "merged_api" => apiDescription.GroupName is "v1" or "v2",
                    _ => documentName == apiDescription.GroupName,
                };
            });

            options.UserCustomDateConverters();
            options.DescribeAllParametersInCamelCase();

            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Web.Api.xml"));
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Application.Contracts.xml"));

            options.OperationFilter<DefaultApplicationResponsesFilter>();
            options.SchemaFilter<SortingToEnumFilter>();
            options.DocumentFilter<AdditionalSchemasFilter>();

            options.CustomOperationIds(apiDesc =>
                apiDesc.TryGetMethodInfo(out var methodInfo)
                    ? $"{methodInfo.Name}{apiDesc.GroupName!.ToUpper()}"
                    : null);

            options.ConfigureSecurity();
        });
    }

    public static void UseConfiguredSwagger(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSwagger(options => options.RouteTemplate = "/api-docs/{documentName}/swagger.json");
        app.UseSwaggerUI(c =>
        {
            foreach (string version in Versions.Where(version => version != "development"))
            {
                c.SwaggerEndpoint($"/api-docs/{version}/swagger.json", $"Api - {version}");
            }

            if (env.IsDevelopment())
            {
                c.SwaggerEndpoint("/api-docs/development/swagger.json", "Development Api");
            }

            c.RoutePrefix = "api-docs";

            c.EnableFilter();
            c.EnableDeepLinking();
            c.DisplayOperationId();
            c.DisplayRequestDuration();
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
            Example = OpenApiAnyFactory.CreateFromJson($"\"{DateOnly.FromDateTime(new DateTime(2022, 11, 15, 12, 0, 0, DateTimeKind.Utc)):O}\""),
        });

        options.MapType<DateTime>(() => new OpenApiSchema
        {
            Type = "string",
            Format = "date-with-time",
            Example = OpenApiAnyFactory.CreateFromJson($"\"{new DateTime(2022, 11, 15, 12, 0, 0, DateTimeKind.Utc):O}\""),
        });
    }
}