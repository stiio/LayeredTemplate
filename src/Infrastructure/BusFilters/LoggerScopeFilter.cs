using MassTransit;
using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Infrastructure.BusFilters;

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
            await next.Send(context);
        }
    }

    public void Probe(ProbeContext context)
    {
    }
}