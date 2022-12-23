using System.Text.Json.Serialization;
using LayeredTemplate.Web.Conventions;
using LayeredTemplate.Web.Converters;
using LayeredTemplate.Web.Filters;
using Swashbuckle.AspNetCore.JsonMultipartFormDataSupport.Extensions;
using Swashbuckle.AspNetCore.JsonMultipartFormDataSupport.Integrations;

namespace LayeredTemplate.Web.Extensions;

public static class ConfigureControllerExtensions
{
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