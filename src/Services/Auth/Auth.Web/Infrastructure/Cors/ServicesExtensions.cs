using LayeredTemplate.Auth.Web.Infrastructure.Options.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Cors;

public static class ServicesExtensions
{
    public static void AddAppCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(opts =>
        {
            var corsSettings = configuration.GetSection(nameof(CorsSettings)).Get<CorsSettings>()!;

            opts.AddDefaultPolicy(policy =>
            {
                if (corsSettings.AllowedOrigins.Length == 0)
                {
                    policy.SetIsOriginAllowed(origin => true);
                }
                else
                {
                    policy.WithOrigins(corsSettings.AllowedOrigins);
                }

                policy.AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }
}