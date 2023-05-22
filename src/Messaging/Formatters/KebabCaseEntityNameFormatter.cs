using Humanizer;
using MassTransit;

namespace LayeredTemplate.Messaging.Formatters;

public class KebabCaseEntityNameFormatter : IEntityNameFormatter
{
    private readonly bool includeNamespace;
    private readonly string? prefix;

    public KebabCaseEntityNameFormatter(bool includeNamespace)
    {
        this.includeNamespace = includeNamespace;
    }

    public KebabCaseEntityNameFormatter(string prefix, bool includeNamespace)
    {
        this.includeNamespace = includeNamespace;
        this.prefix = prefix;
    }

    public string FormatEntityName<T>()
    {
        var name = (this.includeNamespace
            ? typeof(T).FullName
            : typeof(T).Name).Kebaberize();

        return string.IsNullOrEmpty(this.prefix) ? name : $"{this.prefix}-{name}";
    }
}