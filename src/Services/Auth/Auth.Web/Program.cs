using HealthChecks.UI.Client;
using LayeredTemplate.Auth.Web.Components;
using LayeredTemplate.Auth.Web.Components.Account;
using LayeredTemplate.Auth.Web.Data;
using LayeredTemplate.Auth.Web.Services;
using LayeredTemplate.Auth.Web.Extensions;
using LayeredTemplate.Auth.Web.StartupTasks;
using LayeredTemplate.Plugins.StartupRunner;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    services.AddStartupTask<RunMigrationsTask>();
    services.AddStartupTask<SeedOidcClientsTask>();

    services.AddRazorComponents();
    services.AddControllersWithViews();
    services.AddAntDesign();

    services.Configure<ReCaptchaSettings>(configuration.GetSection("ReCaptcha"));
    services.AddHttpClient<ReCaptchaService>();

    services.AddCascadingAuthenticationState();
    services.AddScoped<IdentityRedirectManager>();

    services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
        .AddIdentityCookies();

    services.AddHealthChecks();

    var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                           throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
    services.AddDatabaseDeveloperPageExceptionFilter();

    services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

    services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

    services.AddOpenIddict()
        .AddCore(options =>
        {
            options.UseEntityFrameworkCore()
                .UseDbContext<ApplicationDbContext>();
        })
        .AddServer(options =>
        {
            options.SetAuthorizationEndpointUris("/connect/authorize")
                .SetTokenEndpointUris("/connect/token")
                .SetUserInfoEndpointUris("/connect/userinfo")
                .SetEndSessionEndpointUris("/connect/logout");

            options.AllowAuthorizationCodeFlow()
                .RequireProofKeyForCodeExchange();

            options.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();

            options.RegisterScopes("openid", "profile", "email");

            options.UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough()
                .EnableUserInfoEndpointPassthrough()
                .EnableEndSessionEndpointPassthrough();
        });
}

void ConfigureMiddleware(WebApplication app, IWebHostEnvironment env)
{
    app.UseRequestLogging();

    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();

    app.UseStatusCodePagesWithReExecute("/not-found");
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
