using System.Globalization;

namespace LayeredTemplate.Plugins.AssemblyExtensions.Attributes;

[AttributeUsage(AttributeTargets.Assembly)]
public class AssemblyBuildDateAttribute : Attribute
{
    public AssemblyBuildDateAttribute(string value)
    {
        this.BuildDate = DateTime.ParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal);
    }

    public DateTime BuildDate { get; }
}