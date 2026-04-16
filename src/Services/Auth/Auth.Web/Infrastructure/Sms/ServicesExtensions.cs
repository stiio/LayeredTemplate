using LayeredTemplate.Auth.Web.Infrastructure.Sms.Services;

namespace LayeredTemplate.Auth.Web.Infrastructure.Sms;

public static class ServicesExtensions
{
    public static void AddAppSmsServices(this IServiceCollection services)
    {
        services.AddScoped<ISmsSender, MockSmsSender>();
    }
}