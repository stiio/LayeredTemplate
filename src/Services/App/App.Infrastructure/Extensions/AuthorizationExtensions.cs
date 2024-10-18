using System.Reflection;
using LayeredTemplate.App.Application.Common.Services;
using LayeredTemplate.App.Domain.Common;
using LayeredTemplate.App.Infrastructure.Authorization.Handlers;
using LayeredTemplate.App.Infrastructure.Authorization.PolicyProviders;
using LayeredTemplate.App.Infrastructure.Services.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.App.Infrastructure.Extensions;

internal static class AuthorizationExtensions
{
    public static void ConfigureAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IClaimsTransformation, AppClaimTransformation>();
        services.AddScoped<IRequirementAuthorizationService, RequirementAuthorizationService>();
        services.AddTransient<IAuthorizationPolicyProvider, CustomAuthorizationPolicyProvider>();

        services.AddAuthorization(opts =>
        {
            opts.InvokeHandlersAfterFailure = false;
        });

        var authorizationHandlers = Assembly.GetExecutingAssembly().GetTypes().Where(type =>
            type.IsAssignableTo(typeof(IAuthorizationHandler)) && type is { IsGenericType: false, IsAbstract: false }).ToArray();
        foreach (var authorizationHandler in authorizationHandlers)
        {
            services.AddScoped(typeof(IAuthorizationHandler), authorizationHandler);
        }

        var hasPermissionRequirementType = typeof(HasPermissionRequirementHandler<>);
        var resources = typeof(IBaseEntity).Assembly.GetTypes()
            .Where(x => x.IsAssignableTo(typeof(IBaseEntity)) && x is { IsAbstract: false, IsInterface: false });
        foreach (var resource in resources)
        {
            var impl = hasPermissionRequirementType.MakeGenericType(resource);
            services.AddScoped(typeof(IAuthorizationHandler), impl);
        }
    }
}