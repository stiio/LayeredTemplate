using LayeredTemplate.Web.Extensions;

namespace LayeredTemplate.Web;

public class SwaggerHostFactory
{
    public static IHost CreateHost()
    {
        return Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureWebHostDefaults(b => b.UseStartup<SwaggerStartup>())
            .Build();
    }

    private class SwaggerStartup
    {
        public SwaggerStartup(IConfiguration configuration)
        {
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureControllers();
            services.ConfigureSwagger();
            services.AddEndpointsApiExplorer();
        }

        public void Configure(IApplicationBuilder app)
        {
        }
    }
}