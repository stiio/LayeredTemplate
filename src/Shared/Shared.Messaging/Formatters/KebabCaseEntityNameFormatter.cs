using Humanizer;
using MassTransit;
using MassTransit.Transports;

namespace LayeredTemplate.Shared.Messaging.Formatters;

public class KebabCaseEntityNameFormatter : IEntityNameFormatter
{
    private readonly string? prefix;

    private readonly IMessageNameFormatter messageNameFormatter;

    public KebabCaseEntityNameFormatter(IMessageNameFormatter messageNameFormatter)
    {
        this.messageNameFormatter = messageNameFormatter;
    }

    public KebabCaseEntityNameFormatter(IMessageNameFormatter messageNameFormatter, string prefix)
    {
        this.messageNameFormatter = messageNameFormatter;
        this.prefix = prefix;
    }

    public string FormatEntityName<T>()
    {
        var name = this.messageNameFormatter.GetMessageName(typeof(T));

        return string.IsNullOrEmpty(this.prefix) ? name.Kebaberize() : $"{this.prefix}-{name}".Kebaberize();
    }
}