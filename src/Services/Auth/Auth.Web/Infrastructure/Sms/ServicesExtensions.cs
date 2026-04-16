using LayeredTemplate.Auth.Web.Infrastructure.Sms.Services;

namespace LayeredTemplate.Auth.Web.Infrastructure.Sms;

public static class ServicesExtensions
{
    public static void AddAppSmsServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        services.AddScoped<ISmsSender, MockSmsSender>();
    }
}