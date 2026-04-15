using HealthChecks.UI.Client;
using LayeredTemplate.Auth.Web.Components;
using LayeredTemplate.Auth.Web.Components.Account;
using LayeredTemplate.Auth.Web.Extensions;
using LayeredTemplate.Auth.Web.Infrastructure.Email;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Contexts;
using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using LayeredTemplate.Auth.Web.Infrastructure.ReCaptcha;
using LayeredTemplate.Auth.Web.Infrastructure.Sms;
using LayeredTemplate.Auth.Web.Infrastructure.StartupTasks;
using LayeredTemplate.Plugins.Options;
using LayeredTemplate.Plugins.Options.Constants;
using LayeredTemplate.Plugins.StartupRunner;
using Microsoft.AspNetCore.DataProtection;
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
    services.AddPluginOptions(configuration);

    services.AddStartupTask<RunMigrationsTask>();
    services.AddStartupTask<SeedOidcClientsTask>();

    services.AddRazorComponents();
    services.AddControllersWithViews();

    services.RegisterDbContext(configuration[ConnectionStrings.AuthDbConnection]!);
    services.AddIdentityServices();
    services.AddOpenIddictApp(configuration, env);

    services.AddDataProtection()
        .SetApplicationName("LayeredTemplate.Auth")
        .PersistKeysToDbContext<AuthDbContext>();

    services.AddHttpClient<ReCaptchaService>();

    services.AddScoped<IdentityRedirectManager>();
    services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
    services.AddSingleton<ISmsSender, NoOpSmsSender>();

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
        .AddIdentityCookies();

    services.AddHealthChecks();

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
    app.UseCors(x => x.SetIsOriginAllowed(origin => true));
    app.UseForwardedHeaders();
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
    app.MapStaticAssets();
    app.MapControllers();
    app.MapRazorComponents<App>();
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
