using System.Security.Claims;
using LayeredTemplate.Shared.Constants;
using Serilog;

namespace LayeredTemplate.Web.Extensions;

public static class SerilogExtensions
{
    public static void UseRequestLogging(this IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(opts =>
        {
            opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms; User: {@User}";
            opts.IncludeQueryInRequestPath = true;

            opts.EnrichDiagnosticContext = (context, httpContext) =>
            {
                if (httpContext.User.Identity?.IsAuthenticated ?? false)
                {
                    context.Set(
                        "User",
                        new
                        {
                            UserId = httpContext.User.FindFirstValue(AppClaims.UserId),
                            Role = httpContext.User.FindFirstValue(AppClaims.Role),
                        },
                        true);
                }
                else
                {
                    context.Set("User", null);
                }
            };
        });
    }
}