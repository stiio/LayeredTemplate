using Microsoft.Extensions.Logging;

namespace LayeredTemplate.Shared.DelegatingHandlers;

public class LoggingHandler : DelegatingHandler
{
    private readonly ILogger logger;

    public LoggingHandler(ILogger logger, bool includeHeaders = false)
    {
        this.logger = logger;
        this.IncludeHeaders = includeHeaders;
    }

    public bool IncludeHeaders { get; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? requestContent = null;
        if (request.Content != null)
        {
            requestContent = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        var response = await base.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        var logTemplate = "Http Client Processing\n" +
                          "Request:\n" +
                          "{Request}\n" +
                          "Request Content:\n" +
                          "{RequestContent}\n" +
                          "Response:\n" +
                          "{Response}\n" +
                          "Response Content\n" +
                          "{ResponseContent}";

        this.logger.LogInformation(
            logTemplate,
            this.IncludeHeaders ? request.ToString() : $"Method: {request.Method}, RequestUri: {request.RequestUri}",
            requestContent,
            response,
            responseContent);

        return response;
    }
}