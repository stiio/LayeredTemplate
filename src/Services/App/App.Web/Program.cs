using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthChecks.UI.Client;
using LayeredTemplate.App.Setup;
using LayeredTemplate.App.Setup.Json;
using LayeredTemplate.App.Setup.OpenApi;
using LayeredTemplate.App.Shared;
using LayeredTemplate.Plugins.JsonMultipart;
using LayeredTemplate.Plugins.StartupRunner;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;
using Serilog.Formatting.Json;

// ----- Bootstrap Serilog before host construction so startup errors are captured. -----
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .Enrich
    .WithExceptionDetails(new DestructuringOptionsBuilder()
        .WithDestructurers([new DbUpdateExceptionDestructurer()]))
    .Enrich.FromLogContext()
    .CreateLogger();

Log.Information("Starting up");

try
{
    // Special-case for `dotnet GetDocument.Insider` (Microsoft.Extensions.ApiDescription.Server) —
    // it spins up the host to generate OpenAPI documents at build time, doesn't need DB/Auth/etc.
    if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
    {
        var minimalBuilder = WebApplication.CreateBuilder(args);
        ConfigureSerilog(minimalBuilder.Host);
        ConfigureJson(minimalBuilder.Services);
        minimalBuilder.Services.AddAppOpenApi();
        minimalBuilder.Services.AddPluginJsonMultipart();
        minimalBuilder.Services.AddEndpointsApiExplorer();
        // Type-only stubs for "heavy" services (DbContext, etc.) endpoints resolve from DI.
        // Lets Minimal API infer parameter binding without instantiating a real Postgres
        // connection, running startup tasks, etc. — see Setup/ConfigureDocGenStubs.cs.
        minimalBuilder.Services.AddDocGenStubs();
        var minimalApp = minimalBuilder.Build();
        minimalApp.UseAppOpenApi();
        minimalApp.UseAppRequestLogging();
        // Still need endpoints to be in the route table for docs to discover them, but they
        // won't actually serve — `GetDocument.Insider` only walks the description provider.
        minimalApp.MapAllEndpoints();
        minimalApp.Run();
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

return;

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
    ConfigureJson(services);

    services.AddPluginJsonMultipart();
    services.AddEndpointsApiExplorer();
    services.AddAppOpenApi();

    services.AddAppDb(configuration);
    services.AddAppDataProtection(configuration);
    services.AddAppAuth(configuration);
    services.AddAppProblemDetails();

    services.AddHttpContextAccessor();

    services.AddHealthChecks();

    services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromMinutes(1);
    });

    services.AddAppCors(configuration);
    services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.All;
        options.ForwardLimit = 2;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
    });


    services.AddPluginStartupRunner();
    services.AddSharedServices(configuration, env);
    // Walk the assembly for IFeatureServices implementers and register feature-internal services.
    // Called LAST so feature registrations may override anything registered above (typical for
    // testing-style decorators / wrappers).
    services.AddFeatureServices(configuration, env);
}

void ConfigureMiddleware(WebApplication app, IWebHostEnvironment env)
{
    app.UseForwardedHeaders();
    app.UseCors();

    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        app.UseAppOpenApi();
    }

    app.UseAppRequestLogging();
    app.UseExceptionHandler();

    app.UseAuthentication();
    app.UseAuthorization();
}

void ConfigureEndpoints(IEndpointRouteBuilder app)
{
    app.MapAllEndpoints();
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    });
}

void ConfigureJson(IServiceCollection services)
{
    services.Configure<JsonOptions>(opts =>
    {
        opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opts.SerializerOptions.Converters.Add(new DateTimeJsonConverter());
        opts.SerializerOptions.Converters.Add(new DateOnlyJsonConverter());
        opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        opts.SerializerOptions.WriteIndented = false;
        opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
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

#pragma warning disable SA1402
public partial class Program;
