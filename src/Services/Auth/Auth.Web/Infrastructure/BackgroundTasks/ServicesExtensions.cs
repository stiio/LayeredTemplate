namespace LayeredTemplate.Auth.Web.Infrastructure.BackgroundTasks;

public static class ServicesExtensions
{
    public static void AddAppBackgroundTasks(this IServiceCollection services)
    {
        services.AddHostedService<OpenIddictCleanupService>();
    }
}
