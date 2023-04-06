using StackExchange.Profiling;

namespace LayeredTemplate.Web.Extensions;

public static class MiniProfilerExtensions
{
    public static void ConfigureMiniProfiler(this IServiceCollection services)
    {
        services.AddMiniProfiler(options =>
        {
            options.RouteBasePath = "/api/profiler";
            options.AddEntityFramework();
            options.ColorScheme = StackExchange.Profiling.ColorScheme.Dark;

            options.TrackConnectionOpenClose = false;

            options.IgnoredPaths.Add("/health");
        });
    }
}