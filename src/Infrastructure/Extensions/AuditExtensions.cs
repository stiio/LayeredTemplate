using Audit.NET.PostgreSql;
using Audit.PostgreSql.Providers;
using Audit.WebApi;
using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Shared.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure.Extensions;

public static class AuditExtensions
{
    /// <summary>
    /// Add to middleware pipeline after UseAuthentication for audit call api with api keys.
    /// </summary>
    /// <param name="app"></param>
    public static void UseAudit(this IApplicationBuilder app)
    {
        var serviceProvider = app.ApplicationServices;
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

        Audit.Core.Configuration.DataProvider = new PostgreSqlDataProvider()
        {
            ConnectionString = configuration[ConnectionStrings.DefaultConnection],
            TableName = "audit_events",
            IdColumnName = "id",
            DataColumnName = "data",
            DataType = "jsonb",
            LastUpdatedDateColumnName = "updated_at",
            CustomColumns = new List<CustomColumn>()
            {
                new CustomColumn("event_type", ev => ev.EventType),
                new CustomColumn("created_at", ev => ev.StartDate),
            },
        };

        Audit.Core.Configuration.AddOnCreatedAction((scope) =>
        {
            if (httpContextAccessor.HttpContext?.User.Identity?.AuthenticationType != AppAuthenticationSchemes.ApiKey)
            {
                scope.Discard();
            }

            scope.SetCustomField(AuditCustomFields.IpAddress, httpContextAccessor.HttpContext?.GetRequestIp());
        });

        app.Use(async (context, next) =>
        {
            if (context.User?.Identity?.AuthenticationType == AppAuthenticationSchemes.ApiKey)
            {
                context.Request.EnableBuffering();
                await next();
            }
        });

        app.UseAuditMiddleware(opts =>
            opts.IncludeRequestBody()
                .IncludeResponseBody()
                .WithEventType(context =>
                {
                    var controllerActionDescription = context.GetEndpoint()!.Metadata.GetMetadata<ControllerActionDescriptor>()!;
                    return $"{controllerActionDescription.ControllerName}.{controllerActionDescription.ActionName}.{{verb}} ({context.User.Identity?.AuthenticationType})";
                }));
    }
}