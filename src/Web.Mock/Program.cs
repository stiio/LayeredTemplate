using LayeredTemplate.Shared.Constants;
using LayeredTemplate.Web.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

ConfigureServices(builder.Services, builder.Configuration);

var webApplication = builder.Build();

ConfigureMiddleware(webApplication, webApplication.Environment);

ConfigureEndpoints(webApplication);

webApplication.Run();

void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddAuthorization(opts =>
    {
        opts.AddPolicy(Policies.Client, Policies.ClientPolicy);
        opts.AddPolicy(Policies.Admin, Policies.AdminPolicy);
    });

    services.ConfigureControllers();
    services.ConfigureSwagger();

    services.AddEndpointsApiExplorer();

    services.AddHealthChecks();
}

void ConfigureMiddleware(IApplicationBuilder app, IWebHostEnvironment env)
{
    if (env.IsDevelopment() || env.IsStaging())
    {
        app.UseDeveloperExceptionPage();
        app.UseConfiguredSwagger();
    }

    app.UseHttpsRedirection();

    app.UseRouting();

    app.UseAuthorization();
}

void ConfigureEndpoints(IEndpointRouteBuilder app)
{
    app.MapControllers().AllowAnonymous();
    app.MapHealthChecks("/health");
}