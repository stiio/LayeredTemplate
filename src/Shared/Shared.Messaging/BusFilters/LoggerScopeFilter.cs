using MassTransit;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Shared.Messaging.BusFilters;

internal class LoggerScopeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly ILogger<LoggerScopeFilter<T>> logger;

    public LoggerScopeFilter(ILogger<LoggerScopeFilter<T>> logger)
    {
        this.logger = logger;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        using (this.logger.BeginScope("Consumer process scope: {corId}", Guid.NewGuid()))
        {
            try
            {
                this.logger.LogInformation(
                    "Start message event: {destinationAddress} | {@payload}",
                    context.DestinationAddress?.ToString(),
                    context.Message);

                await next.Send(context);

                this.logger.LogInformation("Complete message event: {destinationAddress}", context.DestinationAddress?.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Consumer exception");
                throw;
            }
        }
    }

    public void Probe(ProbeContext context)
    {
    }
}