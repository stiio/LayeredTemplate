using System.Text.Json;

namespace LayeredTemplate.Application.Common.Models;

public class QueueMessageCreateRequest
{
    private QueueMessageCreateRequest(string body)
    {
        this.Body = body;
    }

    public string Body { get; set; }

    public QueueMessageCreateRequest Create<T>(T body)
    {
        return new QueueMessageCreateRequest(JsonSerializer.Serialize(body));
    }
}