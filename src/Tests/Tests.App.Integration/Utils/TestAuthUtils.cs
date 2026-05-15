using System.Net.Http.Headers;
using System.Text.Json;
using LayeredTemplate.App.Features.Users;
using LayeredTemplate.App.Shared.Auth;
using LayeredTemplate.App.Shared.Auth.MockAuth;

namespace LayeredTemplate.Tests.App.Integration.Utils;

public static class TestAuthUtils
{
    public static void AddToken(HttpClient client, User user)
    {
        var mockUser = new MockUserSettings
        {
            Id = user.Id.ToString(),
            Email = user.Email,
            Role = "Administrator",
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            AppAuthenticationSchemes.Bearer,
            JsonSerializer.Serialize(mockUser));
    }
}
