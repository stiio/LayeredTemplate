using System.Reflection;

namespace LayeredTemplate.Shared.Options;

public class JsonPolymorphismSettings
{
    public string[] AssemblyNames { get; set; } = Array.Empty<string>();

    public Assembly[] Assemblies { get; set; } = Array.Empty<Assembly>();

    public Dictionary<string, Dictionary<string, string>> Mapping { get; set; } = new();
}