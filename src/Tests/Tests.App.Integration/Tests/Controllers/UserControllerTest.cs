using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LayeredTemplate.App.Application.Features.Users.Models;
using LayeredTemplate.App.Domain.Entities;
using LayeredTemplate.Tests.App.Integration.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using Xunit.Priority;

namespace LayeredTemplate.Tests.App.Integration.Tests.Controllers;

[TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
[Collection(nameof(WebApp))]
public class UserControllerTest
{
    private readonly WebApp webApp;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ITestOutputHelper testOutputHelper;

    public UserControllerTest(WebApp webApp, ITestOutputHelper testOutputHelper)
    {
        this.webApp = webApp;
        this.jsonOptions = webApp.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        this.testOutputHelper = testOutputHelper;
    }

    [Theory]
    [ClassData(typeof(TestUsers))]
    [InlineData(null)]
    public async Task Test_GetCurrentUser(User? user)
    {
        using var client = user is null ? this.webApp.CreateClient() : this.webApp.CreateClient(user);

        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/users/current_user");

        var response = await client.SendAsync(request);

        if (user is null)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        else
        {
            Assert.True(response.IsSuccessStatusCode, "Not success status code");

            var currentUser = await response.Content.ReadFromJsonAsync<CurrentUser>(this.jsonOptions);

            Assert.Equal(user.Id, currentUser?.Id);
        }
    }
}