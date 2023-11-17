using System.Collections.Concurrent;
using System.Text;
using MassTransit.Transports;

namespace LayeredTemplate.Messaging.Formatters;

public class CustomAmazonSqsMessageNameFormatter :
    IMessageNameFormatter
{
    private readonly ConcurrentDictionary<Type, string> cache;
    private readonly string genericArgumentSeparator;
    private readonly string genericTypeSeparator;
    private readonly string nestedTypeSeparator;

    public CustomAmazonSqsMessageNameFormatter(
        string? genericArgumentSeparator = null,
        string? genericTypeSeparator = null,
        string? nestedTypeSeparator = null)
    {
        this.genericArgumentSeparator = genericArgumentSeparator ?? "-";
        this.genericTypeSeparator = genericTypeSeparator ?? "-";
        this.nestedTypeSeparator = nestedTypeSeparator ?? "-";

        this.cache = new ConcurrentDictionary<Type, string>();
    }

    public string GetMessageName(Type type)
    {
        return this.cache.GetOrAdd(type, this.CreateMessageName);
    }

    private string CreateMessageName(Type type)
    {
        if (type.IsGenericTypeDefinition)
        {
            throw new ArgumentException("An open generic type cannot be used as a message name");
        }

        var sb = new StringBuilder(string.Empty);

        return this.GetMessageName(sb, type);
    }

    private string GetMessageName(StringBuilder sb, Type type)
    {
        if (type.IsGenericParameter)
        {
            return string.Empty;
        }

        if (type.IsNested)
        {
            this.GetMessageName(sb, type.DeclaringType!);
            sb.Append(this.nestedTypeSeparator);
        }

        if (type.IsGenericType)
        {
            var name = type.GetGenericTypeDefinition().Name;

            // remove `1
            var index = name.IndexOf('`');
            if (index > 0)
            {
                name = name.Remove(index);
            }

            sb.Append(name);
            sb.Append(this.genericTypeSeparator);

            Type[] arguments = type.GetGenericArguments();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(this.genericArgumentSeparator);
                }

                this.GetMessageName(sb, arguments[i]);
            }

            sb.Append(this.genericTypeSeparator);
        }
        else
        {
            sb.Append(type.Name);
        }

        return sb.ToString();
    }
}