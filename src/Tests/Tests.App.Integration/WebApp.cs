using System.Net.Http.Headers;
using System.Net.Mime;
using LayeredTemplate.App.Features.Users;
using LayeredTemplate.Tests.App.Integration.TestAuthHandler;
using LayeredTemplate.Tests.App.Integration.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace LayeredTemplate.Tests.App.Integration;

public class WebApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgreSqlTestContainer = new PostgreSqlBuilder("postgres:13.2")
        .WithDatabase("appDbName-test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithExposedPort(5555)
        .WithPortBinding(5555, 5555)
        .WithAutoRemove(true)
        .Build();

    public async Task InitializeAsync()
    {
        await this.postgreSqlTestContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await this.postgreSqlTestContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    public HttpClient CreateClient(User user)
    {
        var client = this.CreateClient();
        TestAuthUtils.AddToken(client, user);
        return client;
    }

    protected override IWebHostBuilder? CreateWebHostBuilder() =>
        base.CreateWebHostBuilder()?.UseEnvironment("Test");

    protected override void ConfigureClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Override the production DB connection string with the test container's via a
        // configuration overlay — simpler than swapping DbContextOptions in the service
        // collection, and avoids issues with DbContextPool internals.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppWriteDbConnection"] = this.postgreSqlTestContainer.GetConnectionString(),
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication()
                .AddScheme<TestAuthAuthenticationOptions, TestAuthHandler.TestAuthHandler>(TestAuthAuthenticationOptions.DefaultScheme, _ => { });

            services.AddTransient<IAuthenticationSchemeProvider, TestSchemeProvider>();

            DataSeeder.SeedData(services);
        });
    }
}
