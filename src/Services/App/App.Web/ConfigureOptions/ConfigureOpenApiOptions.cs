using System.Reflection;
using System.Text;
using Asp.Versioning.ApiExplorer;
using LayeredTemplate.App.Web.Controllers;
using LayeredTemplate.App.Web.OpenApiTransformers;
using LayeredTemplate.Plugins.AssemblyExtensions.Extensions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace LayeredTemplate.App.Web.ConfigureOptions;

public class ConfigureOpenApiOptions : IConfigureNamedOptions<OpenApiOptions>
{
    private readonly IApiVersionDescriptionProvider apiVersionDescriptionProvider;

    public ConfigureOpenApiOptions(IApiVersionDescriptionProvider apiVersionDescriptionProvider)
    {
        this.apiVersionDescriptionProvider = apiVersionDescriptionProvider;
    }

    public void Configure(string? name, OpenApiOptions options)
    {
        var packageVersion = typeof(AppControllerBase).Assembly.GetVersion();

        var mergedDocumentNames = this.apiVersionDescriptionProvider.ApiVersionDescriptions
            .Where(descriptor => string.IsNullOrEmpty(descriptor.ApiVersion.Status))
            .Select(descriptor => descriptor.GroupName)
            .ToArray();

        if (name == "merged_api")
        {
            options.ShouldInclude = (description) => mergedDocumentNames.Contains(description.GroupName);
        }

        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            if (name == "merged_api")
            {
                document.Info = new OpenApiInfo() { Title = "Merged Api", Version = packageVersion };
            }
            else
            {
                var description = this.apiVersionDescriptionProvider.ApiVersionDescriptions
                    .FirstOrDefault(d => d.GroupName == name);

                if (description is not null)
                {
                    document.Info = CreateInfoForApiVersion(description);
                }
            }

            return Task.CompletedTask;
        });

        options.AddDocumentTransformer<SecurityDefinitionTransformer>();
        options.AddDocumentTransformer<ErrorResultDocumentTransformer>();

        options.AddOperationTransformer<DefaultApplicationResponsesTransformer>();
        options.AddOperationTransformer<AuthOperationTransformer>();
        options.AddOperationTransformer<DefaultOperationSummaryTransformer>();
        options.AddOperationTransformer<CustomOperationIdTransformer>();
        options.AddOperationTransformer<CamelCaseParametersTransformer>();

        options.AddSchemaTransformer<SortingToEnumTransformer>();
        options.AddSchemaTransformer<DefaultSchemaDescriptionTransformer>();
        options.AddSchemaTransformer<DateTimeSchemaTransformer>();
    }

    public void Configure(OpenApiOptions options)
    {
        this.Configure(Options.DefaultName, options);
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        var text = new StringBuilder();
        var info = new OpenApiInfo()
        {
            Title = $"Api - v{description.ApiVersion}",
            Version = description.ApiVersion.ToString(),
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
