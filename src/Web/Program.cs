using LayeredTemplate.Application;
using LayeredTemplate.Infrastructure;
using LayeredTemplate.Infrastructure.Data.Extensions;
using LayeredTemplate.Shared;
using LayeredTemplate.Web.Api.Extensions;
using LayeredTemplate.Web.Extensions;
using LayeredTemplate.Web.Mocks.Authentication;
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
    var builder = WebApplication.CreateBuilder(args);

    ConfigureSerilog(builder.Host);

    ConfigureConfiguration(builder.Configuration, builder.Environment);

    ConfigureServices(builder.Services, builder.Configuration);

    var webApplication = builder.Build();

    ConfigureMiddleware(webApplication, webApplication.Environment);

    ConfigureEndpoints(webApplication);

    webApplication.Run();
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
        .AddEnvironmentVariables();
}

void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<MockUserSettings>(configuration.GetSection(nameof(MockUserSettings)));
    services.RegisterSharedOptions(configuration);

    services.AddInfrastructureServices(configuration);
    services.AddApplicationServices(configuration);

    services.ConfigureAuthentication(configuration);
    services.ConfigureAuthorization();

    services.ConfigureControllers();
    services.ConfigureSwagger();

    services.AddEndpointsApiExplorer();
    services.AddHttpClient();
    services.AddHttpContextAccessor();
    services.AddHealthChecks();
}

void ConfigureMiddleware(IApplicationBuilder app, IWebHostEnvironment env)
{
    // TODO: Uncomment after db configured
    // app.EnsureDbExists();
    if (env.IsDevelopment() || env.IsStaging())
    {
        app.UseDeveloperExceptionPage();
        app.UseConfiguredSwagger();
    }

    app.UseHttpsRedirection();

    app.UseRequestLogging();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();
}

void ConfigureEndpoints(IEndpointRouteBuilder app)
{
    app.MapControllers()
        .AllowAnonymous(); // TODO: Delete this after set auth settings
    app.MapHealthChecks("/health");
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