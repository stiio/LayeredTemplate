namespace LayeredTemplate.Auth.Web.Infrastructure.Locks;

public static class ServicesExtensions
{
    public static void AddPostgresLockProvider(this IServiceCollection services)
    {
        services.AddSingleton<ILockProvider, PostgresLockProvider>();
    }
}