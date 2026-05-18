using System.Reflection;
using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Setup;

/// <summary>
/// Reflection-based feature discovery. Walks the assembly twice over the lifecycle of the host:
/// <list type="bullet">
/// <item><see cref="AddFeatureServices"/> — before <c>builder.Build()</c>, finds all
///   <see cref="IFeatureServices"/> implementers and invokes their static
///   <see cref="IFeatureServices.ConfigureServices"/> so features register their own services
///   into the DI container.</item>
/// <item><see cref="MapAllEndpoints"/> — after <c>builder.Build()</c>, finds all
///   <see cref="IEndpoint"/> implementers and invokes their static <see cref="IEndpoint.Map"/> so
///   features register routes onto the live application.</item>
/// </list>
/// Both passes honour <see cref="DevOnlyAttribute"/> — Development-only features are skipped when
/// the host environment is not Development.
/// </summary>
/// <remarks>
/// Cost: ~ms once at startup (reflection over typically &lt;100 types). Zero per-request cost.
/// Trade-off: incompatible with native AOT (<c>PublishAot</c>) due to trimming. If AOT becomes a
/// requirement, replace this with a source generator that emits the registration list at compile
/// time. Not relevant for the current template.
/// </remarks>
public static class FeatureDiscovery
{
    /// <summary>
    /// Registers feature-internal services in the DI container. Invokes
    /// <see cref="IFeatureServices.ConfigureServices"/> on every type in the assembly that
    /// implements <see cref="IFeatureServices"/>. Call before <c>builder.Build()</c>.
    /// </summary>
    public static IServiceCollection AddFeatureServices(this IServiceCollection services, IHostEnvironment env)
    {
        var isDev = env.IsDevelopment();

        var types = typeof(FeatureDiscovery).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        typeof(IFeatureServices).IsAssignableFrom(t));

        foreach (var type in types)
        {
            if (!isDev && type.GetCustomAttribute<DevOnlyAttribute>() is not null)
            {
                continue;
            }

            var method = type.GetMethod(nameof(IFeatureServices.ConfigureServices), BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    $"Type {type.FullName} implements {nameof(IFeatureServices)} but has no public static ConfigureServices(IServiceCollection).");

            method.Invoke(null, [services]);
        }

        return services;
    }

    /// <summary>
    /// Maps every feature's endpoints onto the live application. Invokes <see cref="IEndpoint.Map"/>
    /// on every type in the assembly that implements <see cref="IEndpoint"/>. Call after
    /// <c>builder.Build()</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        var env = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var isDev = env.IsDevelopment();

        var endpointTypes = typeof(FeatureDiscovery).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        typeof(IEndpoint).IsAssignableFrom(t));

        foreach (var type in endpointTypes)
        {
            if (!isDev && type.GetCustomAttribute<DevOnlyAttribute>() is not null)
            {
                continue;
            }

            var mapMethod = type.GetMethod(nameof(IEndpoint.Map), BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    $"Type {type.FullName} implements {nameof(IEndpoint)} but has no public static Map(IEndpointRouteBuilder).");

            mapMethod.Invoke(null, [app]);
        }

        return app;
    }
}
