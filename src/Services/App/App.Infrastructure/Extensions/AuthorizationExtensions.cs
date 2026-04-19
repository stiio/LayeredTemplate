using System.Reflection;
using LayeredTemplate.App.Infrastructure.Authorization.PolicyProviders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.App.Infrastructure.Extensions;

internal static class AuthorizationExtensions
{
    public static void ConfigureAuthorization(this IServiceCollection services)
    {
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
    }
}