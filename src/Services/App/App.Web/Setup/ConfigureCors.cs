using LayeredTemplate.App.Shared.Options;

namespace LayeredTemplate.App.Setup;

public static class ConfigureCors
{
    public static IServiceCollection AddAppCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CorsSettings>(configuration.GetSection(nameof(CorsSettings)));
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

        return services;
    }
}