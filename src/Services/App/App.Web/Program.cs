using System.Reflection;
using System.Text.Json.Serialization;
using FluentValidation;
using HealthChecks.UI.Client;
using LayeredTemplate.App.Setup;
using LayeredTemplate.App.Setup.Json;
using LayeredTemplate.App.Setup.OpenApi;
using LayeredTemplate.App.Shared.Infrastructure.Email;
using LayeredTemplate.App.Shared.Infrastructure.Locks;
using LayeredTemplate.App.Shared.Options;
using LayeredTemplate.Plugins.JsonMultipart;
using LayeredTemplate.Plugins.StartupRunner;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
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
        minimalBuilder.Services.AddAppOpenApi();
        // Plugin's OpenAPI transformers also need to be registered here — without this, multipart
        // endpoints generate without the `application/json` encoding hint on JSON-typed parts.
        minimalBuilder.Services.AddPluginJsonMultipart();
        // Feature services must also be registered so Minimal API can infer DI-bound parameters
        // when building the route table for the description provider. Without this, endpoints
        // accepting feature services fail with "Failure to infer one or more parameters".
        minimalBuilder.Services.AddFeatureServices(minimalBuilder.Configuration, minimalBuilder.Environment);
        // Type-only stubs for "heavy" services (DbContext, etc.) endpoints resolve from DI.
        // Lets Minimal API infer parameter binding without instantiating a real Postgres
        // connection, running startup tasks, etc. — see Setup/ConfigureDocGenStubs.cs.
        minimalBuilder.Services.AddDocGenStubs();
        minimalBuilder.Services.AddEndpointsApiExplorer();
        ConfigureJson(minimalBuilder.Services);
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
    services.AddPluginStartupRunner();
    services.AddPluginJsonMultipart();

    services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));
    services.Configure<SmtpSettings>(configuration.GetSection(nameof(SmtpSettings)));

    services.AddAppDb(configuration);
    services.AddAppAuth(configuration);
    services.AddAppProblemDetails();

    services.AddSingleton<ILockProvider, PostgresLockProvider>();
    services.AddHttpContextAccessor();
    services.AddScoped<LayeredTemplate.App.Shared.Auth.ICurrentUser, LayeredTemplate.App.Shared.Auth.CurrentUser>();

    if (configuration.GetValue<bool>("MOCK_EMAIL_SENDER"))
    {
        services.AddScoped<IEmailSender, EmailSenderMock>();
    }
    else
    {
        services.AddScoped<IEmailSender, EmailSender>();
    }

    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true, lifetime: ServiceLifetime.Singleton);

    services.AddEndpointsApiExplorer();
    services.AddAppOpenApi();

    ConfigureJson(services);

    services.AddHealthChecks();

    services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromMinutes(1);
    });

    // Walk the assembly for IFeatureServices implementers and register feature-internal services.
    // Called LAST so feature registrations may override anything registered above (typical for
    // testing-style decorators / wrappers).
    services.AddFeatureServices(configuration, env);
}

void ConfigureMiddleware(WebApplication app, IWebHostEnvironment env)
{
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
namespace LayeredTemplate.App
{
    public partial class Program;
}
