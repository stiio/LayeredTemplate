using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.Conventions;
using LayeredTemplate.App.Web.Conventions;
using LayeredTemplate.App.Web.Filters;
using LayeredTemplate.App.Web.Json.Converters;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.JsonMultipartFormDataSupport.Extensions;
using Swashbuckle.AspNetCore.JsonMultipartFormDataSupport.Integrations;

namespace LayeredTemplate.App.Web.Extensions;

public static class ConfigureControllerExtensions
{
    public static void ConfigureControllers(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiBehaviorOptions>(opts =>
        {
            opts.SuppressInferBindingSourcesForParameters = true;
        });

        services.AddControllers(opts =>
            {
                opts.Conventions.Add(new RoutePrefixConvention());

                opts.Filters.Add<ApplicationExceptionFilter>();
                opts.Filters.Add<DevelopmentOnlyFilter>();
            })
            .AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                opts.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
                opts.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
                opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            })
            .UseCustomValidationErrorResponses();

        services.AddJsonMultipartFormDataSupport(JsonSerializerChoice.SystemText);

        services.AddApiVersioning(opts =>
        {
            opts.DefaultApiVersion = new ApiVersion(1);
            opts.ApiVersionReader = new UrlSegmentApiVersionReader();
            opts.ReportApiVersions = true;
        }).AddMvc(opts =>
        {
            opts.Conventions.Add(new VersionByNamespaceConvention());
        }).AddApiExplorer(opts =>
        {
            opts.GroupNameFormat = "'v'VVV";
            opts.SubstituteApiVersionInUrl = true;
            opts.DefaultApiVersion = new ApiVersion(1);
            opts.SubstitutionFormat = "VVV";
        });
    }
}