using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.Conventions;
using LayeredTemplate.App.Application.Common.Models;
using LayeredTemplate.App.Web.Conventions;
using LayeredTemplate.App.Web.ExceptionHandlers;
using LayeredTemplate.App.Web.Filters;
using LayeredTemplate.App.Web.Json.Converters;
using LayeredTemplate.Plugins.JsonMultipart;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace LayeredTemplate.App.Web.Extensions;

public static class ConfigureControllerExtensions
{
    public static void ConfigureControllers(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPluginJsonMultipart();
        services.AddProblemDetails(opts =>
        {
            opts.CustomizeProblemDetails = ctx =>
            {
                if (ctx.ProblemDetails.Title == "One or more validation errors occurred." && ctx.ProblemDetails is not AppProblemDetails)
                {
                    ctx.ProblemDetails.Extensions["errorType"] = AppErrorType.ValidationError;
                }

                ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.Features.Get<IHttpActivityFeature>()!.Activity.Id;
                ctx.ProblemDetails.Extensions["timestamp"] = DateTime.UtcNow;
                ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;

                ctx.ProblemDetails.Type = ctx.ProblemDetails.Status switch
                {
                    400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    401 => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                    403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                    404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                    408 => "https://tools.ietf.org/html/rfc9110#section-15.5.9",
                    409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                    429 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    501 =>"https://tools.ietf.org/html/rfc9110#section-15.6.2",
                    _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                };
            };
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.Configure<ApiBehaviorOptions>(opts =>
        {
            opts.SuppressInferBindingSourcesForParameters = true;
        });

        services.AddControllers(opts =>
            {
                opts.Conventions.Add(new RoutePrefixConvention());
                opts.Filters.Add<DevelopmentOnlyFilter>();
            })
            .AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                opts.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
                opts.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
                opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

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