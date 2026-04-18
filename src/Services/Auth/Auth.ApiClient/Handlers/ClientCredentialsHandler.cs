using System.Net;
using System.Net.Http.Headers;

namespace LayeredTemplate.Auth.ApiClient.Handlers;

/// <summary>
/// Attaches a Bearer token from <see cref="AccessTokenProvider"/> to every outgoing request.
/// On <see cref="HttpStatusCode.Unauthorized"/>, invalidates the cache and retries once —
/// handles token revocation / clock skew gracefully.
/// </summary>
internal sealed class ClientCredentialsHandler : DelegatingHandler
{
    private readonly AccessTokenProvider tokens;

    public ClientCredentialsHandler(AccessTokenProvider tokens)
    {
        this.tokens = tokens;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // First attempt with the cached (or freshly-fetched) token.
        var token = await this.tokens.GetAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // The request body (if any) will be consumed on send. Buffer it up front so we can retry
        // without the caller having to re-create the request.
        var retryBody = await BufferContentAsync(request.Content, cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        this.tokens.Invalidate();

        // Rebuild the request — HttpRequestMessage can be sent only once.
        using var retry = CloneRequest(request, retryBody);
        var refreshed = await this.tokens.GetAsync(cancellationToken);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);

        return await base.SendAsync(retry, cancellationToken);
    }

    private static async Task<byte[]?> BufferContentAsync(HttpContent? content, CancellationToken ct)
    {
        if (content is null)
        {
            return null;
        }

        return await content.ReadAsByteArrayAsync(ct);
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage original, byte[]? bufferedBody)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy,
        };

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (bufferedBody is not null && original.Content is not null)
        {
            clone.Content = new ByteArrayContent(bufferedBody);
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
