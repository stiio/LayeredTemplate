using System.Reflection;

namespace LayeredTemplate.Shared.Extensions;

public static class VersionExtensions
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
}