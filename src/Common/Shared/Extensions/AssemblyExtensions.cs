using System.Reflection;
using LayeredTemplate.Shared.AssemblyAttributes;

namespace LayeredTemplate.Shared.Extensions;

public static class AssemblyExtensions
{
    public static string? GetVersion(this Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (version is null || !version.Contains('+'))
        {
            return version;
        }

        return version[..version.IndexOf('+')];
    }

    public static DateTime? GetBuildDate(this Assembly assembly)
    {
        return assembly.GetCustomAttribute<AssemblyBuildDateAttribute>()?.BuildDate;
    }
}