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
        private readonly IConfiguration configuration;

        public SwaggerStartup(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureControllers(this.configuration);
            services.ConfigureSwagger();
            services.AddEndpointsApiExplorer();
        }

        public void Configure(IApplicationBuilder app)
        {
        }
    }
}