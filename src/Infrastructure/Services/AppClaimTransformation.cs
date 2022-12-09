﻿using System.Security.Claims;
using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Infrastructure.Services;

public class AppClaimTransformation : IClaimsTransformation
{
    private readonly ILogger<AppClaimTransformation> logger;

    public AppClaimTransformation(ILogger<AppClaimTransformation> logger)
    {
        this.logger = logger;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var role = principal.FindFirstValue(TokenKeys.Role);
        if (!string.IsNullOrEmpty(role) && principal.Claims.All(claim => claim.Type != ClaimTypes.Role))
        {
            var roleClaim = new Claim[]
            {
                new Claim(ClaimTypes.Role, role),
            };

            principal.AddIdentity(new ClaimsIdentity(roleClaim));
        }

        return Task.FromResult(principal);
    }
}