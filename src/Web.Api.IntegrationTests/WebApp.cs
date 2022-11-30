using System.Net.Http.Headers;
using System.Net.Mime;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Domain.Entities;
using LayeredTemplate.Infrastructure.Data.Context;
using LayeredTemplate.Web.Api.IntegrationTests.TestAuth;
using LayeredTemplate.Web.Api.IntegrationTests.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace LayeredTemplate.Web.Api.IntegrationTests;

public class WebApp : WebApplicationFactory<Web.Program>, IAsyncLifetime
{
    private readonly PostgreSqlTestcontainer postgreSqlTestContainer = new TestcontainersBuilder<PostgreSqlTestcontainer>()
        .WithDatabase(new PostgreSqlTestcontainerConfiguration()
        {
            Database = "sampleWorkflowManager",
            Username = "postgres",
            Password = "postgres",
            Port = 5555,
        })
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
        return base.CreateWebHostBuilder()?.UseEnvironment("test");
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
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(opts =>
            {
                opts.UseNpgsql(this.postgreSqlTestContainer.ConnectionString);
            });

            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

            // --
            services.AddAuthentication(TestAuthAuthenticationOptions.DefaultScheme)
                .AddScheme<TestAuthAuthenticationOptions, TestAuthHandler>(TestAuthAuthenticationOptions.DefaultScheme, _ => { });

            DataSeeder.SeedData(services);
        });
    }
}