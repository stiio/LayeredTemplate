using StackExchange.Profiling;

namespace LayeredTemplate.Shared.DelegatingHandlers;

public class MiniProfilerHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var timing = MiniProfiler.Current?.CustomTiming("http", request.ToString());

        var response = await base.SendAsync(request, cancellationToken);

        return response;
    }
}