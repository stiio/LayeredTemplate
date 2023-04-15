using System.Text.Json;

namespace LayeredTemplate.Application.Common.Models;

public class QueueMessage
{
    private QueueMessage(string messageId, string body)
    {
        this.MessageId = messageId;
        this.Body = body;
    }

    public string MessageId { get; set; }

    public string Body { get; set; }

    public static QueueMessage Create(string messageId, string body)
    {
        return new QueueMessage(messageId, body);
    }

    public T? ParseBody<T>()
    {
        return JsonSerializer.Deserialize<T>(this.Body);
    }
}