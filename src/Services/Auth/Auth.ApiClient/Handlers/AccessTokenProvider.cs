using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Auth.ApiClient.Handlers;

/// <summary>
/// In-memory cache of the client_credentials access token. Singleton — shared across all outgoing calls.
/// Handles concurrent refresh via <see cref="SemaphoreSlim"/> so only one token fetch is in flight at a time.
/// </summary>
internal sealed class AccessTokenProvider : IDisposable
{
    private readonly IHttpClientFactory httpFactory;
    private readonly IOptionsMonitor<AuthApiClientOptions> optionsMonitor;
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    private CachedToken? cache;

    public AccessTokenProvider(IHttpClientFactory httpFactory, IOptionsMonitor<AuthApiClientOptions> optionsMonitor)
    {
        this.httpFactory = httpFactory;
        this.optionsMonitor = optionsMonitor;
    }

    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        var options = this.optionsMonitor.CurrentValue;

        if (this.cache is { } fresh && fresh.ExpiresAt > DateTimeOffset.UtcNow + options.TokenRefreshBuffer)
        {
            return fresh.Token;
        }

        await this.refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check — another caller might have just refreshed while we were waiting.
            if (this.cache is { } warm && warm.ExpiresAt > DateTimeOffset.UtcNow + options.TokenRefreshBuffer)
            {
                return warm.Token;
            }

            this.cache = await this.FetchTokenAsync(options, cancellationToken);
            return this.cache.Token;
        }
        finally
        {
            this.refreshLock.Release();
        }
    }

    /// <summary>Drops the cached token — next <see cref="GetAsync"/> call will fetch a new one.</summary>
    public void Invalidate() => this.cache = null;

    public void Dispose() => this.refreshLock.Dispose();

    private async Task<CachedToken> FetchTokenAsync(AuthApiClientOptions options, CancellationToken cancellationToken)
    {
        var client = this.httpFactory.CreateClient(ServicesExtensions.AccessTokenClientName);

        using var body = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", options.ClientId),
            new KeyValuePair<string, string>("client_secret", options.ClientSecret),
            new KeyValuePair<string, string>("scope", string.Join(' ', options.Scopes))
        ]);

        var tokenUri = new Uri(new Uri(options.BaseUrl), "/connect/token");
        using var response = await client.PostAsync(tokenUri, body, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AuthApiException(
                response.StatusCode,
                $"Failed to obtain access token from Auth.Web: {response.ReasonPhrase}. Body: {payload}");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Token response was empty.");

        // Sub-second drift between client and server is normal — subtract a small safety margin so we
        // never return a token that's already technically expired on the server side.
        var expiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(token.ExpiresIn) - TimeSpan.FromSeconds(5);
        return new CachedToken(token.AccessToken, expiresAt);
    }

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string TokenType);
}
