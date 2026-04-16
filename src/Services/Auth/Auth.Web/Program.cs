using HealthChecks.UI.Client;
using LayeredTemplate.Auth.Web.Components;
using LayeredTemplate.Auth.Web.Components.Account;
using LayeredTemplate.Auth.Web.Infrastructure.Cors;
using LayeredTemplate.Auth.Web.Infrastructure.Data;
using LayeredTemplate.Auth.Web.Infrastructure.DataProtection;
using LayeredTemplate.Auth.Web.Infrastructure.Email;
using LayeredTemplate.Auth.Web.Infrastructure.Identity;
using LayeredTemplate.Auth.Web.Infrastructure.Logging;
using LayeredTemplate.Auth.Web.Infrastructure.OpenIddict;
using LayeredTemplate.Auth.Web.Infrastructure.Options;
using LayeredTemplate.Auth.Web.Infrastructure.Options.Constants;
using LayeredTemplate.Auth.Web.Infrastructure.ReCaptcha;
using LayeredTemplate.Auth.Web.Infrastructure.Sms;
using LayeredTemplate.Auth.Web.Infrastructure.StartupTasks;
using LayeredTemplate.Plugins.StartupRunner;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
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

    ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

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
        .AddJsonFile($"appsettings.local.json", true, true) // load environment settings
        .AddEnvironmentVariables()
        .AddEnvironmentVariablesFromJsonVariables();
}

void ConfigureServices(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
{
    services.AddPluginStartupRunner();
    services.AddAppSettings(configuration);

    services.AddStartupTask<RunMigrationsTask>();
    services.AddStartupTask<SeedOidcClientsTask>();

    services.AddRazorComponents();
    services.AddControllersWithViews();

    services.RegisterDbContext(configuration[ConnectionStrings.AuthDbConnection]!);
    services.AddIdentityServices();
    services.AddAppOpenIddict(configuration, env);

    services.AddAppDataProtection(configuration);

    services.AddHttpClient<ReCaptchaService>();

    services.AddScoped<IdentityRedirectManager>();
    services.AddAppEmailServices(configuration, env);
    services.AddAppSmsServices();

    services.AddCascadingAuthenticationState();

    services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
        .AddGitHub(opts =>
        {
            opts.ClientId = configuration["GitHub:ClientId"]!;
            opts.ClientSecret = configuration["GitHub:ClientSecret"]!;
            opts.Scope.Add("user:email");
        })
        .AddYandex(opts =>
        {
            opts.ClientId = configuration["Yandex:ClientId"]!;
            opts.ClientSecret = configuration["Yandex:ClientSecret"]!;
            opts.CallbackPath = "/signin-yandex";
        })
        .AddIdentityCookies();

    services.AddHealthChecks();

    services.AddAppCors(configuration);

    services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.All;
        options.ForwardLimit = 2;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

void ConfigureMiddleware(WebApplication app, IWebHostEnvironment env)
{
    app.UseForwardedHeaders();
    app.UseCors();

    app.UseRequestLogging();

    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();

    app.UseStatusCodePagesWithReExecute("/not_found");
    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseAntiforgery();
}

void ConfigureEndpoints(IEndpointRouteBuilder app)
{
    app.MapHealthChecks("/health", new HealthCheckOptions()
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    });

    app.MapStaticAssets();
    app.MapControllers();
    app.MapRazorComponents<App>();
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

namespace LayeredTemplate.Auth.Web
{
    public partial class Program
    {
    }
}
