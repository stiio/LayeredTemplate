using StackExchange.Profiling;

namespace LayeredTemplate.Shared.DelegatingHandlers;

public class MiniProfilerHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (MiniProfiler.Current == null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        string? requestContent = null;
        if (request.Content != null)
        {
            requestContent = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        if (!string.IsNullOrEmpty(requestContent))
        {
            requestContent = $"\nRequest Body:\n{requestContent}";
        }

        using var timing = MiniProfiler.Current?.CustomTiming("http", $"{request}{requestContent}");

        var response = await base.SendAsync(request, cancellationToken);

        return response;
    }
}