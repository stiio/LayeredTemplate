using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.Conventions;
using LayeredTemplate.Web.Conventions;
using LayeredTemplate.Web.Converters;
using LayeredTemplate.Web.Filters;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.JsonMultipartFormDataSupport.Extensions;
using Swashbuckle.AspNetCore.JsonMultipartFormDataSupport.Integrations;

namespace LayeredTemplate.Web.Extensions;

public static class ConfigureControllerExtensions
{
    public static void ConfigureControllers(this IServiceCollection services)
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
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
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