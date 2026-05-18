using System.Security.Claims;
using LayeredTemplate.App.Shared.Auth;
using LayeredTemplate.Plugins.Http.Extensions;
using Serilog;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;

namespace LayeredTemplate.App.Setup;

public static class ConfigureSerilog
{
    public static IApplicationBuilder UseAppRequestLogging(this IApplicationBuilder app) =>
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
                    context.Set("User", new
                    {
                        Id = httpContext.User.FindFirstValue(AppClaims.UserId),
                        Email = httpContext.User.FindFirstValue(AppClaims.Email),
                    }, destructureObjects: true);
                }
            };
        });
}
