using System.Net.Http.Headers;
using System.Text.Json;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Web.Api.IntegrationTests.TestAuth;
using LayeredTemplate.Web.Mocks.Authentication;

namespace LayeredTemplate.Web.Api.IntegrationTests.Utils;

public static class TestAuthUtils
{
    public static void AddToken(HttpClient client, User user)
    {
        var mockUser = new MockUserSettings()
        {
            Id = user.Id.ToString(),
            Email = user.Email,
            Role = user.Role.ToString(),
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthAuthenticationOptions.DefaultScheme, JsonSerializer.Serialize(mockUser));
    }
}