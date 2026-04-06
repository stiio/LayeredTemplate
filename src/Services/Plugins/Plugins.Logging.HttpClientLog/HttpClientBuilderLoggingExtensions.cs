using LayeredTemplate.Plugins.Logging.HttpClientLog.Handlers;
using LayeredTemplate.Plugins.Logging.HttpClientLog.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Plugins.Logging.HttpClientLog;

public static class HttpClientBuilderLoggingExtensions
{
    /// <summary>
    /// Adds the configurable <see cref="LoggingHandler"/> to the HTTP client pipeline.
    /// Options are stored under the client's name so that each named/typed HttpClient
    /// gets its own independent configuration.
    /// </summary>
    /// <param name="builder">The <see cref="IHttpClientBuilder"/> to configure.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="HttpClientLoggingOptions"/>.
    /// When <c>null</c>, the default options are used.
    /// </param>
    /// <returns>IHttpClientBuilder</returns>
    public static IHttpClientBuilder AddRequestLogging(
        this IHttpClientBuilder builder,
        Action<HttpClientLoggingOptions>? configure = null)
    {
        string clientName = builder.Name;

        if (configure != null)
        {
            builder.Services.Configure(clientName, configure);
        }

        builder.AddHttpMessageHandler(sp =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<HttpClientLoggingOptions>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger($"LayeredTemplate.Plugins.Logging.HttpClientLog.{clientName}");

            return new LoggingHandler(optionsMonitor.Get(clientName), logger);
        });

        return builder;
    }
}
