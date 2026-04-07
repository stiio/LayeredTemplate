using System.Reflection;
using HealthChecks.UI.Client;
using LayeredTemplate.App.Application;
using LayeredTemplate.App.Infrastructure;
using LayeredTemplate.App.Web.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;
using Serilog.Formatting.Json;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .Enrich
    .WithExceptionDetails(new DestructuringOptionsBuilder()
        .WithDestructurers(new[] { new DbUpdateExceptionDestructurer() }))
    .Enrich.FromLogContext()
    .CreateLogger();

Log.Information("Starting up");

try
{
    // register minimal required services for generate openapi document on build.
    if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureSerilog(builder.Host);
        builder.Services.ConfigureControllers(builder.Configuration);
        builder.Services.ConfigureOpenApi();
        builder.Services.AddEndpointsApiExplorer();
        var webApplication = builder.Build();
        webApplication.UseConfiguredOpenApi();
        webApplication.UseRequestLogging();
        webApplication.Run();
    }
    else
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureSerilog(builder.Host);

        ConfigureConfiguration(builder.Configuration, builder.Environment);

        ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

        var webApplication = builder.Build();

        ConfigureMiddleware(webApplication, webApplication.Environment);

        ConfigureEndpoints(webApplication);

        webApplication.Run();
    }
}
catch (Exception e)
{
    Log.Fatal(e, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

void ConfigureConfiguration(ConfigurationManager configuration, IWebHostEnvironment env)
{
    configuration.AddJsonFile("appsettings.json", false, true) // load base settings
        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true) // load environment settings
        .AddJsonFile($"appsettings.local.json", true, true) // load environment settings
        .AddEnvironmentVariables()
        .AddEnvironmentVariablesFromJsonVariables();
}

void ConfigureServices(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
{
    services.AddInfrastructureServices(configuration, env);
    services.AddApplicationServices(configuration);

    services.ConfigureControllers(configuration);
    services.ConfigureOpenApi();
    services.AddEndpointsApiExplorer();

    services.AddHttpClient();
    services.AddHttpContextAccessor();
    services.AddHealthChecks();
}

void ConfigureMiddleware(WebApplication app, IWebHostEnvironment env)
{
    if (env.IsDevelopment() || env.IsStaging())
    {
        app.UseDeveloperExceptionPage();
        app.UseConfiguredOpenApi();
    }

    app.UseRequestLogging();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();
}

void ConfigureEndpoints(IEndpointRouteBuilder app)
{
    app.MapControllers();
    app.MapHealthChecks("/health", new HealthCheckOptions()
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    });
}

void ConfigureSerilog(IHostBuilder host)
{
    host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich
            .WithExceptionDetails(new DestructuringOptionsBuilder()
                .WithDestructurers(new[] { new DbUpdateExceptionDestructurer() }))
            .Enrich.FromLogContext();
    });
}

public partial class Program
{
}