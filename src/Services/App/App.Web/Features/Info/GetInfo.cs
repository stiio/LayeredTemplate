using System.Reflection;
using LayeredTemplate.Plugins.AssemblyExtensions.Extensions;

namespace LayeredTemplate.App.Features.Info;

public static class GetInfo
{
    public sealed record Response
    {
        /// <summary>UTC timestamp when the assembly was built.</summary>
        public DateTime? BuildDate { get; init; }

        /// <summary>Informational assembly version.</summary>
        public string? Version { get; init; }
    }

    public static void Configure(RouteGroupBuilder group) =>
        group.MapGet("/", Handle)
            .WithName(nameof(GetInfo))
            .WithSummary("Get build / version info");

    public static Response Handle()
    {
        var entry = Assembly.GetEntryAssembly()!;
        return new Response
        {
            BuildDate = entry.GetBuildDate(),
            Version = entry.GetVersion(),
        };
    }
}
