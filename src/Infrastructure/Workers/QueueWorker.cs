using LayeredTemplate.Application.Common.Interfaces;
using LayeredTemplate.Application.Common.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Infrastructure.Workers;

/// <summary>
/// For start worker: services.AddHostedService&lt;QueueWorker&gt;
/// </summary>
internal class QueueWorker : BackgroundService
{
    private readonly IQueueService queueService;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<QueueWorker> logger;

    public QueueWorker(IQueueService queueService, IServiceProvider serviceProvider, ILogger<QueueWorker> logger)
    {
        this.queueService = queueService;
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await this.queueService.Receive(stoppingToken);
                if (!result.Any())
                {
                    continue;
                }

                foreach (var message in result)
                {
                    await this.ProcessingMessage(message);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Queue worker exception.");
            }
        }
    }

    private async Task ProcessingMessage(QueueMessage message)
    {
        using var loggerScope = this.logger.BeginScope("Queue worker message processing message scope: {corId}", Guid.NewGuid());

        try
        {
            this.logger.LogInformation("Queue worker processing message: {message}", message);

            // Add handle message this
            // using var scope = this.serviceProvider.CreateScope();
            // var messageBody = message.ParseBody<SomeBodyType>();
            // var messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
            // messageHandler.Handle(messageBody);
            await this.queueService.Dequeue(message);
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "Notifications processing message exception");
        }
    }
}