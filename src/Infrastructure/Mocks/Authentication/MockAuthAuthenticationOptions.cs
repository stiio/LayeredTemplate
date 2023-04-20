﻿using LayeredTemplate.Shared.Constants;
using Microsoft.AspNetCore.Authentication;

namespace LayeredTemplate.Infrastructure.Mocks.Authentication;

internal class MockAuthAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = AppAuthenticationSchemes.User;

    public string Scheme => DefaultScheme;

    public string AuthenticationType => AppAuthenticationTypes.User;
}