using LayeredTemplate.App.Shared.Errors;
using Microsoft.AspNetCore.Http.Features;

namespace LayeredTemplate.App.Setup;

public static class ConfigureProblemDetails
{
    /// <summary>
    /// Registers <see cref="GlobalExceptionHandler"/> + RFC 7807 ProblemDetails customisation
    /// (traceId, timestamp, type-uri per status code). All thrown exceptions become structured
    /// JSON responses; the handler picks the right status / error type.
    /// </summary>
    public static IServiceCollection AddAppProblemDetails(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddProblemDetails(opts =>
        {
            opts.CustomizeProblemDetails = ctx =>
            {
                // Auto-validation of DataAnnotations on inbound requests produces a "validation"
                // ProblemDetails *without* our AppErrorType — tag it so clients can branch uniformly.
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
                    501 => "https://tools.ietf.org/html/rfc9110#section-15.6.2",
                    _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                };
            };
        });

        return services;
    }
}
