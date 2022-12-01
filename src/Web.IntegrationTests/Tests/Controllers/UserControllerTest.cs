using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LayeredTemplate.Application.Contracts.Models;
using LayeredTemplate.Web.IntegrationTests.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Priority;

namespace LayeredTemplate.Web.IntegrationTests.Tests.Controllers;

[TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
[Collection(nameof(WebApp))]
public class UserControllerTest
{
    private readonly WebApp webApp;
    private readonly JsonSerializerOptions jsonOptions;

    public UserControllerTest(WebApp webApp)
    {
        this.webApp = webApp;
        this.jsonOptions = webApp.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
    }

    [Fact]
    [Priority(0)]
    public async Task Test_GetCurrentUser_Client()
    {
        using var client = this.webApp.CreateClient(TestUsers.Client);

        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/users/current_user");

        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, "Not success status code");

        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUser>(this.jsonOptions);

        Assert.Equal(TestUsers.Client.Id, currentUser?.Id);
    }

    [Fact]
    [Priority(0)]
    public async Task Test_GetCurrentUser_Admin()
    {
        using var client = this.webApp.CreateClient(TestUsers.Admin);

        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/users/current_user");

        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, "Not success status code");

        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUser>(this.jsonOptions);

        Assert.Equal(TestUsers.Admin.Id, currentUser?.Id);
    }

    [Fact]
    [Priority(0)]
    public async Task Test_GetCurrentUser_WithoutToken()
    {
        using var client = this.webApp.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/users/current_user");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Priority(0)]
    public async Task Test_GetCurrentUser_NotSeedClient()
    {
        using var client = this.webApp.CreateClient(TestUsers.NotSeedClient);

        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/users/current_user");

        var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode, "Not success status code");

        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUser>(this.jsonOptions);

        Assert.Equal(TestUsers.NotSeedClient.Id, currentUser?.Id);
    }
}