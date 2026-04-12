using System.Security.Claims;
using LayeredTemplate.Plugins.Authorization.Abstractions.Constants;
using LayeredTemplate.Plugins.Http.Extensions;
using Serilog;

namespace LayeredTemplate.Auth.Web.Extensions;

public static class SerilogExtensions
{
    public static void UseRequestLogging(this IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(opts =>
        {
            opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms.";
            opts.IncludeQueryInRequestPath = true;

            opts.EnrichDiagnosticContext = (context, httpContext) =>
            {
                context.Set("RequestIp", httpContext.GetRequestIp());
                context.Set("Referer", httpContext.Request.Headers.Referer.ToString());
                if (httpContext.User.Identity?.IsAuthenticated ?? false)
                {
                    context.Set(
                        "User",
                        new
                        {
                            Id = httpContext.User.FindFirstValue(AppClaims.UserId),
                            Email = httpContext.User.FindFirstValue(AppClaims.Email),
                        },
                        true);
                }
            };
        });
    }
}