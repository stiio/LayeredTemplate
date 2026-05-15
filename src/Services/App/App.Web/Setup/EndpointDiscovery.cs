using System.Reflection;
using LayeredTemplate.App.Shared.Endpoints;

namespace LayeredTemplate.App.Setup;

/// <summary>
/// Reflection-based <see cref="IEndpoint"/> discovery. Runs once at startup; finds all classes
/// implementing <see cref="IEndpoint"/> in the running assembly and invokes their static
/// <c>Map</c>. Honours <see cref="DevOnlyAttribute"/> — Development-only feature route files are
/// skipped when the host environment is not Development.
/// </summary>
/// <remarks>
/// Cost: ~ms once at startup (reflection over typically &lt;100 types). Zero per-request cost.
/// Trade-off: incompatible with native AOT (<c>PublishAot</c>) due to trimming. If AOT becomes a
/// requirement, replace this with a source generator that emits the registration list at compile
/// time. Not relevant for the current template.
/// </remarks>
public static class EndpointDiscovery
{
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        var env = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var isDev = env.IsDevelopment();

        var endpointTypes = typeof(EndpointDiscovery).Assembly.GetTypes()
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
