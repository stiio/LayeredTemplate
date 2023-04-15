using LayeredTemplate.Application.Common.Models;

namespace LayeredTemplate.Application.Common.Interfaces;

public interface IQueueService
{
    Task Queue(QueueMessageCreateRequest request, CancellationToken cancellationToken = default);

    Task<QueueMessage[]> Receive(CancellationToken cancellationToken = default);

    Task Dequeue(QueueMessage message, CancellationToken cancellationToken = default);
}