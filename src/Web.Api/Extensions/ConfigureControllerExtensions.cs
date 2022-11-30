using System.Text.Json.Serialization;
using LayeredTemplate.Web.Api.Conventions;
using LayeredTemplate.Web.Api.Converters;
using LayeredTemplate.Web.Api.Filters;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.JsonMultipartFormDataSupport.Extensions;
using Swashbuckle.AspNetCore.JsonMultipartFormDataSupport.Integrations;

namespace LayeredTemplate.Web.Api.Extensions;

/// <summary>
/// Controller Extensions
/// </summary>
public static class ConfigureControllerExtensions
{
    /// <summary>
    /// Configure Controllers
    /// </summary>
    /// <param name="services"></param>
    public static void ConfigureControllers(this IServiceCollection services)
    {
        services.AddControllers(opts =>
            {
                opts.Conventions.Add(new ApiExplorerGroupPerVersionConvention());
                opts.Filters.Add<ApplicationExceptionFilter>();
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
    }
}