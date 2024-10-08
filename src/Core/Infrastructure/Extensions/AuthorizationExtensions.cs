﻿using System.Reflection;
using LayeredTemplate.Application.Common.Services;
using LayeredTemplate.Infrastructure.Services.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace LayeredTemplate.Infrastructure.Extensions;

internal static class AuthorizationExtensions
{
    public static void ConfigureAuthorization(this IServiceCollection services)
    {
        // TODO: uncomment
        // services.AddScoped<IClaimsTransformation, AppClaimTransformation>();
        services.AddScoped<IResourceAuthorizationService, ResourceAuthorizationService>();

        services.AddAuthorization(ConfigurePolicies);

        var authorizationHandlers = Assembly.GetExecutingAssembly().GetTypes().Where(type => type.IsAssignableTo(typeof(IAuthorizationHandler))).ToArray();
        foreach (var authorizationHandler in authorizationHandlers)
        {
            services.AddScoped(typeof(IAuthorizationHandler), authorizationHandler);
        }
    }

    private static void ConfigurePolicies(AuthorizationOptions options)
    {
    }
}