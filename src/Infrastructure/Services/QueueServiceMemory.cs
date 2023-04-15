using System.Text.Json;
using System.Threading.Channels;
using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Infrastructure.Services;

internal class QueueServiceMemory : IQueueService
{
    private readonly Channel<QueueMessage> queue;
    private readonly ILogger<QueueServiceMemory> logger;

    public QueueServiceMemory(ILogger<QueueServiceMemory> logger)
    {
        this.logger = logger;

        BoundedChannelOptions options = new(50)
        {
            FullMode = BoundedChannelFullMode.Wait,
        };

        this.queue = Channel.CreateBounded<QueueMessage>(options);
    }

    public async Task Queue(QueueMessageCreateRequest request, CancellationToken cancellationToken = default)
    {
        var message = QueueMessage.Create(Guid.NewGuid().ToString(), JsonSerializer.Serialize(request.Body));

        await this.queue.Writer.WriteAsync(message, cancellationToken);

        this.logger.LogInformation("Add item to queue: {@message}", message);
    }

    public async Task<QueueMessage[]> Receive(CancellationToken cancellationToken = default)
    {
        var message = await this.queue.Reader.ReadAsync(cancellationToken);

        return new[] { message };
    }

    public Task Dequeue(QueueMessage message, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Remove message from queue: {@message}", message);
        return Task.CompletedTask;
    }
}