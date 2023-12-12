using System.Reflection;
using System.Text;
using Asp.Versioning.ApiExplorer;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Shared.Extensions;
using LayeredTemplate.Web.Api.Controllers;
using LayeredTemplate.Web.OpenApiFilters;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LayeredTemplate.Web.ConfigureOptions;

public class ConfigureSwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider apiVersionDescriptionProvider;

    public ConfigureSwaggerGenOptions(IApiVersionDescriptionProvider apiVersionDescriptionProvider)
    {
        this.apiVersionDescriptionProvider = apiVersionDescriptionProvider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        var packageVersion = typeof(AppControllerBase).Assembly.GetVersion();

        foreach (var description in this.apiVersionDescriptionProvider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description, packageVersion));
        }

        options.SwaggerDoc("merged_api", new OpenApiInfo() { Title = "Merged Api", Version = packageVersion });

        var mergedDocumentNames = this.apiVersionDescriptionProvider.ApiVersionDescriptions
            .Where(descriptor => string.IsNullOrEmpty(descriptor.ApiVersion.Status))
            .Select(descriptor => descriptor.GroupName)
            .ToArray();

        options.DocInclusionPredicate((documentName, apiDescription) =>
        {
            return documentName switch
            {
                "merged_api" => mergedDocumentNames.Contains(apiDescription.GroupName),
                _ => documentName == apiDescription.GroupName,
            };
        });

        options.CustomSchemaIds(type =>
        {
            var typeName = type.Name;
            if (!type.GetTypeInfo().IsGenericType)
            {
                return typeName;
            }

            var genericArgumentIds = type.GetGenericArguments()
                .Select(t => t.Name)
                .ToArray();

            return new StringBuilder(typeName)
                .Replace($"`{genericArgumentIds.Length}", string.Empty)
                .Append($"{string.Join(string.Empty, genericArgumentIds)}")
                .ToString();
        });

        options.DescribeAllParametersInCamelCase();

        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Application.Contracts.xml"));
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Web.Api.xml"));

        options.OperationFilter<DefaultApplicationResponsesFilter>();
        options.OperationFilter<AuthOperationFilter>();
        options.OperationFilter<DefaultOperationSummaryFilter>();

        options.SchemaFilter<SortingToEnumFilter>();
        options.SchemaFilter<DefaultSchemaDescriptionFilter>();

        options.CustomOperationIds(apiDesc =>
            apiDesc.TryGetMethodInfo(out var methodInfo)
                ? $"{methodInfo.Name}{apiDesc.GroupName!.ToUpper()}"
                : null);

        options.AddSecurityDefinition(AppAuthenticationSchemes.Bearer, new OpenApiSecurityScheme()
        {
            Name = AppAuthenticationSchemes.Bearer,
            BearerFormat = "JWT",
            Scheme = AppAuthenticationSchemes.Bearer,
            Description = "Specify the authorization token.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
        });

        options.AddSecurityDefinition(AppAuthenticationSchemes.ApiKey, new OpenApiSecurityScheme()
        {
            Name = "X-Api-Key",
            Description = "Specify the api key.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
        });

        options.MapType<DateOnly>(() => new OpenApiSchema
        {
            Type = "string",
            Format = null,
            Example = OpenApiAnyFactory.CreateFromJson($"\"{DateOnly.FromDateTime(new DateTime(2022, 11, 15, 12, 0, 0, DateTimeKind.Utc)):O}\""),
        });

        options.MapType<DateTime>(() => new OpenApiSchema
        {
            Type = "string",
            Format = null,
            Example = OpenApiAnyFactory.CreateFromJson($"\"{new DateTime(2022, 11, 15, 12, 0, 0, DateTimeKind.Utc):O}\""),
        });
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description, string? packageVersion)
    {
        var text = new StringBuilder();
        var info = new OpenApiInfo()
        {
            Title = $"Api - v{description.ApiVersion}",
            Version = packageVersion,
        };

        if (description.IsDeprecated)
        {
            text.Append("This API version has been deprecated.");
        }

        if (description.SunsetPolicy is { } policy)
        {
            if (policy.Date is { } when)
            {
                text.Append(" The API will be sunset on ")
                    .Append(when.Date.ToShortDateString())
                    .Append('.');
            }

            if (policy.HasLinks)
            {
                text.AppendLine();

                foreach (var link in policy.Links)
                {
                    if (link.Type != "text/html")
                    {
                        continue;
                    }

                    text.AppendLine();

                    if (link.Title.HasValue)
                    {
                        text.Append(link.Title.Value).Append(": ");
                    }

                    text.Append(link.LinkTarget.OriginalString);
                }
            }
        }

        info.Description = text.ToString();

        return info;
    }
}