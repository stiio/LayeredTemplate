using System.Net.Http.Headers;
using System.Net.Mime;
using LayeredTemplate.App.Domain.Entities;
using LayeredTemplate.App.Infrastructure.Data;
using LayeredTemplate.App.Infrastructure.Data.Context;
using LayeredTemplate.Tests.App.Integration.TestAuthHandler;
using LayeredTemplate.Tests.App.Integration.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace LayeredTemplate.Tests.App.Integration;

public class WebApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgreSqlTestContainer = new PostgreSqlBuilder()
        .WithDatabase("appDbName-test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithExposedPort(5555)
        .WithPortBinding(5555, 5555)
        .WithAutoRemove(true)
        .WithImage("postgres:13.2")
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

    protected override IWebHostBuilder? CreateWebHostBuilder()
    {
        return base.CreateWebHostBuilder()?.UseEnvironment("Test");
    }

    protected override void ConfigureClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // test db
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();

            services.RegisterDbContext(this.postgreSqlTestContainer.GetConnectionString());

            // --
            services.AddAuthentication()
                .AddScheme<TestAuthAuthenticationOptions, TestAuthHandler.TestAuthHandler>(TestAuthAuthenticationOptions.DefaultScheme, _ => { });

            services.AddTransient<IAuthenticationSchemeProvider, TestSchemeProvider>();

            DataSeeder.SeedData(services);
        });
    }
}